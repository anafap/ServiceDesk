using System.Diagnostics;
using System.Reflection;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Application.Services;
using ServiceDesk.Infrastructure.Photos;
namespace ServiceDesk.Api.Photos;
public sealed class PhotoWorker(IServiceScopeFactory scopes, ILogger<PhotoWorker> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                foreach (var abandoned in Directory.EnumerateDirectories(Path.GetTempPath(), "servicedesk-photo-*"))
                    if (Directory.GetLastWriteTimeUtc(abandoned) < DateTime.UtcNow.AddHours(-24)) Directory.Delete(abandoned, true);
                using var scope = scopes.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IAttachmentRepository>();
                var storage = scope.ServiceProvider.GetRequiredService<IPhotoStorage>();
                await repository.ExpireAsync(); storage.Cleanup(DateTime.UtcNow.AddHours(-24), await repository.ReadyKeysAsync());
                var item = await repository.ClaimAsync();
                if (item is not null)
                {
                    var lease = item.LeaseId!.Value;
                    var key = $"{item.Id:N}-{lease:N}";
                    var temp = Path.Combine(Path.GetTempPath(), "servicedesk-photo-" + Guid.NewGuid().ToString("N"));
                    if (OperatingSystem.IsWindows()) Directory.CreateDirectory(temp);
                    else Directory.CreateDirectory(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                    var published = false;
                    try
                    {
                        var scanner = scope.ServiceProvider.GetRequiredService<IPhotoScanner>();
                        await using (var source = storage.OpenOriginal(item.Id)) await scanner.ScanAsync(source, cancellationToken);
                        var input = Path.Combine(temp, "input.bin"); var output = Path.Combine(temp, "output.png");
                        await using (var source = storage.OpenOriginal(item.Id))
                        await using (var target = File.Create(input)) await source.CopyToAsync(target, cancellationToken);
                        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
                        if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet", StringComparison.OrdinalIgnoreCase))
                            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
                        start.ArgumentList.Add("--sanitize-photo"); start.ArgumentList.Add(input); start.ArgumentList.Add(output);
                        start.Environment.Clear(); start.Environment["TMPDIR"] = temp; start.Environment["DOTNET_EnableDiagnostics"] = "0";
                        using var child = Process.Start(start) ?? throw new IOException("Photo processor could not start.");
                        var stdout = child.StandardOutput.ReadToEndAsync(cancellationToken);
                        var stderr = child.StandardError.ReadToEndAsync(cancellationToken);
                        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); deadline.CancelAfter(TimeSpan.FromSeconds(30));
                        try { await child.WaitForExitAsync(deadline.Token); }
                        catch { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(CancellationToken.None); throw; }
                        await Task.WhenAll(stdout, stderr);
                        if (child.ExitCode != 0)
                        {
                            logger.LogWarning("Photo processor exited with code {ExitCode}", child.ExitCode);
                            throw new InvalidDataException("Photo processing was rejected.");
                        }
                        if (new FileInfo(output).Length > 100 * 1024 * 1024) throw new InvalidDataException("Processed photo exceeds storage limits.");
                        await using (var ready = File.OpenRead(output)) await scanner.ScanAsync(ready, cancellationToken);
                        await using (var ready = File.OpenRead(output)) await storage.PutReadyAsync(key, ready, cancellationToken);
                        await scope.ServiceProvider.GetRequiredService<AttachmentService>().CompleteAsync(item.Id, lease, key);
                        published = true;
                        storage.DeleteOriginal(item.Id);
                    }
                    catch (Exception error) when (!cancellationToken.IsCancellationRequested)
                    {
                        // Only the current lease owner may reject; a later worker may already own the job.
                        using var recovery = scopes.CreateScope();
                        var recoverRepository = recovery.ServiceProvider.GetRequiredService<IAttachmentRepository>();
                        var current = await recoverRepository.GetAsync(item.Id);
                        if (!published && current?.ContentKey != key) storage.DeleteReady(key);
                        if (current?.State == "processing" && current.LeaseId == lease && (error is InvalidDataException || current.Attempts >= 3))
                        { current.Reject(); await recoverRepository.SaveAsync(); storage.DeleteOriginal(item.Id); }
                        logger.LogWarning("Photo processing incomplete for attachment {AttachmentId}; failure type {FailureType}", item.Id, error.GetType().Name);
                    }
                    finally { Directory.Delete(temp, recursive: true); }
                }
            }
            catch (Exception error) when (!cancellationToken.IsCancellationRequested)
            { logger.LogError("Photo worker iteration failed: {FailureType}", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }
}

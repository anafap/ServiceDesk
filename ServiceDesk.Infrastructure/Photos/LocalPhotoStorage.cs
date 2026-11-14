using System.Text.RegularExpressions;
using ServiceDesk.Application.Interfaces;
namespace ServiceDesk.Infrastructure.Photos;
public sealed class LocalPhotoStorage : IPhotoStorage
{
    private readonly string root;
    public LocalPhotoStorage(string root)
    {
        this.root = Path.GetFullPath(root);
        Directory.CreateDirectory(Path.Combine(this.root, "quarantine"));
        Directory.CreateDirectory(Path.Combine(this.root, "ready"));
        if (!OperatingSystem.IsWindows())
        {
            var ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            File.SetUnixFileMode(this.root, ownerOnly);
            File.SetUnixFileMode(Path.Combine(this.root, "quarantine"), ownerOnly);
            File.SetUnixFileMode(Path.Combine(this.root, "ready"), ownerOnly);
        }
    }
    private string Original(Guid id) => Path.Combine(root, "quarantine", id.ToString("N") + ".bin");
    private string Ready(string key)
    {
        if (!Regex.IsMatch(key, "^[a-f0-9]{32}-[a-f0-9]{32}$")) throw new ArgumentException("Invalid photo key.");
        return Path.Combine(root, "ready", key + ".png");
    }
    public async Task PutOriginalAsync(Guid id, Stream source, CancellationToken cancellationToken)
    {
        var path = Original(id);
        try
        {
            await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920]; long total = 0; int count;
                while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += count;
                    if (total > 5 * 1024 * 1024) throw new ArgumentException("Photos must be no larger than 5 MiB.");
                    await file.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }
                if (total == 0) throw new ArgumentException("Select a JPEG or PNG photo.");
            }
            var header = new byte[8];
            await using var read = File.OpenRead(path);
            var length = await read.ReadAsync(header, cancellationToken);
            var jpeg = length >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff;
            var png = length == 8 && header.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            if (!jpeg && !png) throw new ArgumentException("Only JPEG and PNG photos are accepted.");
        }
        catch { File.Delete(path); throw; }
    }
    public Stream OpenOriginal(Guid id) => File.OpenRead(Original(id));
    public async Task PutReadyAsync(string key, Stream source, CancellationToken cancellationToken)
    {
        var target = Ready(key);
        await using var file = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(file, cancellationToken);
    }
    public Stream OpenReady(string key) => File.OpenRead(Ready(key));
    public void DeleteOriginal(Guid id) => File.Delete(Original(id));
    public void DeleteReady(string key) => File.Delete(Ready(key));
    public void Cleanup(DateTime before, IReadOnlySet<string> referencedKeys)
    {
        foreach (var path in Directory.EnumerateFiles(Path.Combine(root, "ready"), "*.png"))
            if (File.GetLastWriteTimeUtc(path) < before && !referencedKeys.Contains(Path.GetFileNameWithoutExtension(path))) File.Delete(path);
        foreach (var path in Directory.EnumerateFiles(Path.Combine(root, "quarantine")))
            if (File.GetLastWriteTimeUtc(path) < before) File.Delete(path);
    }
}

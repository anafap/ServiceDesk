using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Globalization;
using ImageMagick;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Exceptions;
using ServiceDesk.Infrastructure.Photos;
namespace ServiceDesk.Tests;
public class PhotoTests
{
    [Fact]
    public async Task Storage_rejects_spoofed_empty_and_oversized_uploads_and_path_traversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "photo-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalPhotoStorage(root);
            foreach (var bytes in new[] { Array.Empty<byte>(), Encoding.UTF8.GetBytes("<svg/>"), new byte[5 * 1024 * 1024 + 1] })
                await Assert.ThrowsAsync<ArgumentException>(() => storage.PutOriginalAsync(Guid.NewGuid(), new MemoryStream(bytes), default));
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "quarantine")));
            Assert.Throws<ArgumentException>(() => storage.OpenReady("../../outside"));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void Sanitizer_reencodes_valid_photos_removes_metadata_and_rejects_other_formats()
    {
        var root = Path.Combine(Path.GetTempPath(), "sanitize-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "input"); var output = Path.Combine(root, "output.png");
            using (var image = new MagickImage(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEElEQVR4nGP4z8AARAwQCgAf7gP9i18U1AAAAABJRU5ErkJggg==")))
            {
                image.SetAttribute("comment", "Private location metadata");
                var exif = new ExifProfile(); exif.SetValue(ExifTag.ImageDescription, "Private location metadata"); image.SetProfile(exif);
                image.Write(input, MagickFormat.Png);
            }
            PhotoSanitizer.Process(input, output);
            using var result = new MagickImage(output);
            Assert.Equal(MagickFormat.Png, result.Format); Assert.Equal(2u, result.Width);
            Assert.Null(result.GetExifProfile()); Assert.Null(result.GetAttribute("comment"));
            File.WriteAllText(input, "<svg xmlns='http://www.w3.org/2000/svg'><rect width='1' height='1'/></svg>");
            Assert.ThrowsAny<Exception>(() => PhotoSanitizer.Process(input, output));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void Canonical_png_fixture_can_be_sanitized()
    {
        var root = Path.Combine(Path.GetTempPath(), "fixture-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "input.png"); var output = Path.Combine(root, "output.png");
            File.WriteAllBytes(input, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEElEQVR4nGP4z8AARAwQCgAf7gP9i18U1AAAAABJRU5ErkJggg=="));
            PhotoSanitizer.Process(input, output);
            Assert.True(File.Exists(output));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task Cleanup_removes_abandoned_objects_but_preserves_referenced_ready_photos()
    {
        var root = Path.Combine(Path.GetTempPath(), "cleanup-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalPhotoStorage(root); var id = Guid.NewGuid();
            await storage.PutOriginalAsync(id, new MemoryStream(new byte[] { 137,80,78,71,13,10,26,10 }), default);
            var kept = $"{Guid.NewGuid():N}-{Guid.NewGuid():N}"; var orphan = $"{Guid.NewGuid():N}-{Guid.NewGuid():N}";
            await storage.PutReadyAsync(kept, new MemoryStream([1]), default); await storage.PutReadyAsync(orphan, new MemoryStream([1]), default);
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-2));
            storage.Cleanup(DateTime.UtcNow.AddHours(-24), new HashSet<string> { kept });
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "quarantine")));
            Assert.Single(Directory.GetFiles(Path.Combine(root, "ready")));
            using var readable = storage.OpenReady(kept); Assert.Equal(1, readable.ReadByte());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Scanner_requires_fresh_signatures_and_confirmed_clean_result(bool infected, bool stale)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            for (var step = 0; step < (stale ? 1 : 2); step++)
            {
                using var client = await listener.AcceptTcpClientAsync(); await using var stream = client.GetStream();
                var command = new List<byte>(); var one = new byte[1];
                while (await stream.ReadAsync(one) != 0 && one[0] != 0) command.Add(one[0]);
                if (step == 0)
                {
                    var date = DateTime.UtcNow.AddDays(stale ? -4 : 0).ToString("ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture);
                    await stream.WriteAsync(Encoding.ASCII.GetBytes("ClamAV 1.4.3/123/" + date + "\0"));
                }
                else
                {
                    var header = new byte[4];
                    while (true)
                    {
                        await stream.ReadExactlyAsync(header); var count = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header);
                        if (count == 0) break;
                        await stream.ReadExactlyAsync(new byte[count]);
                    }
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(infected ? "stream: Eicar-Test-Signature FOUND\0" : "stream: OK\0"));
                }
            }
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var scan = new ClamAvScanner("127.0.0.1", port).ScanAsync(new MemoryStream(Encoding.ASCII.GetBytes("harmless test data")), timeout.Token);
        if (stale) await Assert.ThrowsAsync<IOException>(() => scan);
        else if (infected) await Assert.ThrowsAsync<InvalidDataException>(() => scan);
        else await scan;
        await server.WaitAsync(timeout.Token);
    }
    [Fact]
    public void Comment_validation_preserves_plain_text_and_terminal_ticket_guard()
    {
        Assert.Throws<ArgumentException>(() => new TicketComment(1, 1, " "));
        Assert.Throws<ArgumentException>(() => new TicketComment(1, 1, new string('a', 2001)));
        Assert.Equal(2000, new TicketComment(1, 1, new string('a', 2000)).Text.Length);
        var ticket = new SupportTicket("Printer", "Broken", 1, 1, 1, ServiceDesk.Domain.Enums.TicketPriority.High);
        ticket.Reject(); Assert.Throws<StateConflictException>(() => ticket.RecordDiscussionActivity());
    }
}

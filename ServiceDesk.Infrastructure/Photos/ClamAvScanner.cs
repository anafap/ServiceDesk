using System.Buffers.Binary;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using ServiceDesk.Application.Interfaces;
namespace ServiceDesk.Infrastructure.Photos;
public sealed class ClamAvScanner(string host, int port) : IPhotoScanner
{
    private async Task<string> CommandAsync(string command, CancellationToken cancellationToken)
    {
        using var client = new TcpClient(); await client.ConnectAsync(host, port, cancellationToken);
        await using var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes("z" + command + "\0"), cancellationToken);
        return await ResponseAsync(stream, cancellationToken);
    }
    private static async Task<string> ResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096]; var length = 0;
        while (length < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(length, 1), cancellationToken);
            if (count == 0 || buffer[length] == 0) return Encoding.ASCII.GetString(buffer, 0, length);
            length++;
        }
        throw new IOException("Invalid scanner response.");
    }
    public async Task ScanAsync(Stream source, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30)); var ct = deadline.Token;
        var version = await CommandAsync("VERSION", ct);
        var parts = version.Split('/');
        if (parts.Length < 3 || !DateTime.TryParseExact(parts[2].Trim(), ["ddd MMM dd HH:mm:ss yyyy", "ddd MMM d HH:mm:ss yyyy"], CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out var updated) || updated.ToUniversalTime() < DateTime.UtcNow.AddDays(-2)
            || updated.ToUniversalTime() > DateTime.UtcNow.AddHours(1)) throw new IOException("Scanner signatures are unavailable or stale.");
        using var client = new TcpClient(); await client.ConnectAsync(host, port, ct);
        await using var stream = client.GetStream(); await stream.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), ct);
        var buffer = new byte[81920]; var header = new byte[4]; int count;
        while ((count = await source.ReadAsync(buffer, ct)) > 0)
        {
            BinaryPrimitives.WriteInt32BigEndian(header, count); await stream.WriteAsync(header, ct);
            await stream.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        await stream.WriteAsync(new byte[4], ct);
        var response = await ResponseAsync(stream, ct);
        if (response.Contains("FOUND", StringComparison.Ordinal)) throw new InvalidDataException("The scanner rejected the photo.");
        if (!response.EndsWith(": OK", StringComparison.Ordinal)) throw new IOException("Photo scan could not be confirmed.");
    }
}

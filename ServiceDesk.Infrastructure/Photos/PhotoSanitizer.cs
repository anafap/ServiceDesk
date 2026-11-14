using ImageMagick;
namespace ServiceDesk.Infrastructure.Photos;
public static class PhotoSanitizer
{
    public static void Process(string input, string output)
    {
        var policy = """
        <policymap>
          <policy domain="delegate" rights="none" pattern="*" />
          <policy domain="filter" rights="none" pattern="*" />
          <policy domain="path" rights="none" pattern="@*" />
          <policy domain="coder" rights="none" pattern="*" />
          <policy domain="coder" rights="read|write" pattern="{JPEG,PNG}" />
          <policy domain="resource" name="memory" value="256MiB" />
          <policy domain="resource" name="map" value="0" />
          <policy domain="resource" name="disk" value="0" />
          <policy domain="resource" name="width" value="20000" />
          <policy domain="resource" name="height" value="20000" />
          <policy domain="resource" name="list-length" value="4" />
          <policy domain="resource" name="time" value="20" />
          <policy domain="resource" name="thread" value="1" />
        </policymap>
        """;
        var config = ImageMagick.Configuration.ConfigurationFiles.Default;
        config.Policy.Data = policy;
        MagickNET.Initialize(config);
        var bytes = File.ReadAllBytes(input);
        if (bytes.Length > 5 * 1024 * 1024) throw new InvalidDataException("Oversized photo.");
        var settings = new MagickReadSettings { FrameCount = 1 };
        using (var probe = new MagickImage())
        {
            probe.Ping(bytes, settings);
            if (probe.Format is not (MagickFormat.Jpeg or MagickFormat.Png) || (ulong)probe.Width * probe.Height > 20_000_000)
                throw new InvalidDataException("Unsupported photo dimensions or format.");
        }
        using var image = new MagickImage(bytes, settings);
        image.AutoOrient(); image.Strip(); image.Write(output, MagickFormat.Png);
    }
}

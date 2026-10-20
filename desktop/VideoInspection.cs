using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ImageMetadataCleaner;

internal sealed class VideoInspectionReport
{
    public string SchemaVersion { get; set; } = "1.0";
    public VideoFileInfo File { get; set; } = new();
    public InspectionStatus Inspection { get; set; } = new();
    public VideoTechnicalMetadata TechnicalMetadata { get; set; } = new();
    public List<VideoSoftwareEvidence> SoftwareMetadata { get; set; } = new();
    public List<VideoAiIndicator> AiIndicators { get; set; } = new();
    public VideoC2paInfo C2pa { get; set; } = new();
    public Dictionary<string, string> OtherMetadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class VideoFileInfo
{
    public string Name { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Extension { get; set; } = "";
    public string DetectedContainer { get; set; } = "unknown";
    public DateTime LastWriteTimeUtc { get; set; }
}

internal sealed class InspectionStatus
{
    public string Status { get; set; } = "error";
    public List<string> Warnings { get; set; } = new();
}

internal sealed class VideoTechnicalMetadata
{
    public string Format { get; set; } = "unknown";
    public string? FormatDescription { get; set; }
    public double? DurationSeconds { get; set; }
    public long? ContainerBitrate { get; set; }
    public int VideoStreamCount { get; set; }
    public int AudioStreamCount { get; set; }
    public int SubtitleStreamCount { get; set; }
    public int OtherStreamCount { get; set; }
    public List<VideoStreamInfo> VideoStreams { get; set; } = new();
    public List<AudioStreamInfo> AudioStreams { get; set; } = new();
}

internal sealed class VideoStreamInfo
{
    public int Index { get; set; }
    public string? Codec { get; set; }
    public string? CodecLongName { get; set; }
    public string? Profile { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? AspectRatio { get; set; }
    public string? FrameRate { get; set; }
    public string? AverageFrameRate { get; set; }
    public long? Bitrate { get; set; }
    public string? PixelFormat { get; set; }
    public string? ColorSpace { get; set; }
    public string? ColorPrimaries { get; set; }
    public string? TransferCharacteristics { get; set; }
    public bool Hdr { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class AudioStreamInfo
{
    public int Index { get; set; }
    public string? Codec { get; set; }
    public string? CodecLongName { get; set; }
    public int? SampleRate { get; set; }
    public int? Channels { get; set; }
    public string? ChannelLayout { get; set; }
    public long? Bitrate { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class VideoSoftwareEvidence
{
    public string Application { get; set; } = "";
    public string Field { get; set; } = "";
    public string Value { get; set; } = "";
    public string Interpretation { get; set; } = "Explicit metadata evidence; it does not prove every stage of the workflow.";
}

internal sealed class VideoAiIndicator
{
    public string Field { get; set; } = "";
    public string Value { get; set; } = "";
    public string Interpretation { get; set; } = "Explicit provenance or application metadata; absence does not prove that AI was not used.";
}

internal sealed class VideoC2paInfo
{
    public string Status { get; set; } = "not_detected";
    public string? ValidationDetails { get; set; }
    public List<string> Evidence { get; set; } = new();
}

internal static class VideoInspectionService
{
    private static readonly string[] SupportedExtensions = [".mp4", ".mov", ".avi", ".m4v", ".webm", ".mkv"];

    public static bool IsInspectable(string path) => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static async Task<VideoInspectionReport> InspectAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("The selected video could not be found.", path);
        var info = new FileInfo(path);
        var report = new VideoInspectionReport
        {
            File = new VideoFileInfo { Name = info.Name, SizeBytes = info.Length, Extension = info.Extension, LastWriteTimeUtc = info.LastWriteTimeUtc, DetectedContainer = await DetectContainerAsync(path, cancellationToken) }
        };
        if (!IsInspectable(path)) report.Inspection.Warnings.Add("The file extension is not one of the supported inspection formats.");
        if (report.File.DetectedContainer == "unknown") report.Inspection.Warnings.Add("The container signature was not recognized; the extension may be misleading or the file may be malformed.");
        if (report.File.DetectedContainer == "avi" && !string.Equals(info.Extension, ".avi", StringComparison.OrdinalIgnoreCase)) report.Inspection.Warnings.Add("The detected AVI container does not match the file extension.");
        if (report.File.DetectedContainer == "matroska/webm" && !string.Equals(info.Extension, ".mkv", StringComparison.OrdinalIgnoreCase) && !string.Equals(info.Extension, ".webm", StringComparison.OrdinalIgnoreCase)) report.Inspection.Warnings.Add("The detected Matroska/WebM container does not match the file extension.");
        if (report.File.DetectedContainer is "iso-base-media" or "quicktime" && !new[] { ".mp4", ".mov", ".m4v" }.Contains(info.Extension, StringComparer.OrdinalIgnoreCase)) report.Inspection.Warnings.Add("The detected ISO base media container does not match the file extension.");
        var ffprobe = FindTool("ffprobe.exe");
        if (ffprobe == null)
        {
            report.Inspection.Status = "error";
            report.Inspection.Warnings.Add("ffprobe was not found. Install FFmpeg or place ffprobe.exe beside the app to inspect technical streams.");
            return report;
        }

        var result = await RunAsync(ffprobe, ["-v", "error", "-print_format", "json", "-show_format", "-show_streams", path], cancellationToken);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            report.Inspection.Status = "error";
            report.Inspection.Warnings.Add(string.IsNullOrWhiteSpace(result.StandardError) ? "ffprobe could not read this file." : result.StandardError.Trim());
            return report;
        }
        try { ParseProbe(report, JsonDocument.Parse(result.StandardOutput).RootElement); }
        catch (JsonException ex) { report.Inspection.Status = "error"; report.Inspection.Warnings.Add("ffprobe returned invalid JSON: " + ex.Message); return report; }
        await AddExifToolMetadataAsync(report, path, cancellationToken);
        report.Inspection.Status = report.Inspection.Warnings.Count == 0 ? "complete" : "complete_with_warnings";
        return report;
    }

    private static void ParseProbe(VideoInspectionReport report, JsonElement root)
    {
        if (root.TryGetProperty("format", out var format))
        {
            report.TechnicalMetadata.Format = GetString(format, "format_name") ?? report.File.DetectedContainer;
            report.TechnicalMetadata.FormatDescription = GetString(format, "format_long_name");
            report.TechnicalMetadata.DurationSeconds = GetDouble(format, "duration");
            report.TechnicalMetadata.ContainerBitrate = GetLong(format, "bit_rate");
            AddTags(report, format, "format.tags");
        }
        if (!root.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array) return;
        foreach (var stream in streams.EnumerateArray())
        {
            var type = GetString(stream, "codec_type") ?? "other";
            if (type == "video") { report.TechnicalMetadata.VideoStreamCount++; report.TechnicalMetadata.VideoStreams.Add(ParseVideo(stream)); }
            else if (type == "audio") { report.TechnicalMetadata.AudioStreamCount++; report.TechnicalMetadata.AudioStreams.Add(ParseAudio(stream)); }
            else if (type == "subtitle") report.TechnicalMetadata.SubtitleStreamCount++;
            else report.TechnicalMetadata.OtherStreamCount++;
        }
    }

    private static VideoStreamInfo ParseVideo(JsonElement stream)
    {
        var colorTransfer = GetString(stream, "color_transfer");
        var colorPrimaries = GetString(stream, "color_primaries");
        var colorSpace = GetString(stream, "colorspace");
        var tags = ReadTags(stream);
        return new VideoStreamInfo { Index = GetInt(stream, "index") ?? 0, Codec = GetString(stream, "codec_name"), CodecLongName = GetString(stream, "codec_long_name"), Profile = GetString(stream, "profile"), Width = GetInt(stream, "width"), Height = GetInt(stream, "height"), AspectRatio = GetString(stream, "display_aspect_ratio"), FrameRate = GetString(stream, "r_frame_rate"), AverageFrameRate = GetString(stream, "avg_frame_rate"), Bitrate = GetLong(stream, "bit_rate"), PixelFormat = GetString(stream, "pix_fmt"), ColorSpace = colorSpace, ColorPrimaries = colorPrimaries, TransferCharacteristics = colorTransfer, Hdr = (colorTransfer?.Contains("2084", StringComparison.OrdinalIgnoreCase) == true) || (colorTransfer?.Contains("arib", StringComparison.OrdinalIgnoreCase) == true) || tags.Keys.Any(x => x.Contains("hdr", StringComparison.OrdinalIgnoreCase)), Tags = tags };
    }

    private static AudioStreamInfo ParseAudio(JsonElement stream) => new() { Index = GetInt(stream, "index") ?? 0, Codec = GetString(stream, "codec_name"), CodecLongName = GetString(stream, "codec_long_name"), SampleRate = GetInt(stream, "sample_rate"), Channels = GetInt(stream, "channels"), ChannelLayout = GetString(stream, "channel_layout"), Bitrate = GetLong(stream, "bit_rate"), Tags = ReadTags(stream) };

    private static void AddTags(VideoInspectionReport report, JsonElement element, string prefix)
    {
        foreach (var pair in ReadTags(element))
        {
            var field = prefix + "." + pair.Key;
            report.OtherMetadata[field] = pair.Value;
            ClassifyEvidence(report, field, pair.Value);
        }
    }

    private static async Task AddExifToolMetadataAsync(VideoInspectionReport report, string path, CancellationToken cancellationToken)
    {
        var exiftool = FindTool("exiftool.exe");
        if (exiftool == null) { report.Inspection.Warnings.Add("ExifTool was not found; XMP/IPTC and some application-specific tags were not checked."); return; }
        var result = await RunAsync(exiftool, ["-j", "-G1", "-a", "-s", path], cancellationToken);
        if (result.ExitCode != 0) { report.Inspection.Warnings.Add("ExifTool could not read the file: " + result.StandardError.Trim()); return; }
        try
        {
            using var json = JsonDocument.Parse(result.StandardOutput);
            var item = json.RootElement.ValueKind == JsonValueKind.Array ? json.RootElement.EnumerateArray().FirstOrDefault() : default;
            if (item.ValueKind != JsonValueKind.Object) { report.Inspection.Warnings.Add("ExifTool returned no file metadata."); return; }
            foreach (var property in item.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) continue;
                var value = property.Value.ToString();
                report.OtherMetadata["exiftool." + property.Name] = value;
                ClassifyEvidence(report, "exiftool." + property.Name, value);
            }
        }
        catch (JsonException) { report.Inspection.Warnings.Add("ExifTool returned invalid JSON; its metadata was not included."); }
    }

    private static void ClassifyEvidence(VideoInspectionReport report, string field, string value)
    {
        var text = (field + " " + value).ToLowerInvariant();
        var software = new[] { ("Adobe Premiere Pro", "premiere"), ("Adobe Media Encoder", "media encoder"), ("Adobe After Effects", "after effects"), ("DaVinci Resolve", "davinci"), ("Final Cut Pro", "final cut"), ("CapCut", "capcut"), ("Adobe Photoshop", "photoshop") };
        foreach (var item in software.Where(x => text.Contains(x.Item2)))
            if (!report.SoftwareMetadata.Any(x => x.Application == item.Item1 && x.Field == field && x.Value == value)) report.SoftwareMetadata.Add(new VideoSoftwareEvidence { Application = item.Item1, Field = field, Value = value });
        if (text.Contains("c2pa") || text.Contains("content credential") || text.Contains("contentauth")) { report.C2pa.Status = "detected_not_validated"; report.C2pa.Evidence.Add(field + " = " + value); }
        if (text.Contains("generative") || text.Contains("ai-generated") || text.Contains("ai generated") || text.Contains("generativeai") || text.Contains("synthetic")) report.AiIndicators.Add(new VideoAiIndicator { Field = field, Value = value });
    }

    private static async Task<string> DetectContainerAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = new byte[16];
        await using var input = File.OpenRead(path);
        _ = await input.ReadAsync(bytes, cancellationToken);
        if (bytes.Length >= 12 && Encoding.ASCII.GetString(bytes, 4, 4) == "ftyp") return Encoding.ASCII.GetString(bytes, 8, 4) is "qt  " ? "quicktime" : "iso-base-media";
        if (bytes.Length >= 12 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "AVI ") return "avi";
        if (bytes.Length >= 4 && bytes[0] == 0x1A && bytes[1] == 0x45 && bytes[2] == 0xDF && bytes[3] == 0xA3) return "matroska/webm";
        return "unknown";
    }

    private static Dictionary<string, string> ReadTags(JsonElement element)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (element.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object)
            foreach (var property in tags.EnumerateObject()) result[property.Name] = property.Value.ToString();
        return result;
    }

    private static string? GetString(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : null;
    private static int? GetInt(JsonElement element, string name) => int.TryParse(GetString(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static long? GetLong(JsonElement element, string name) => long.TryParse(GetString(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static double? GetDouble(JsonElement element, string name) => double.TryParse(GetString(element, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static string? FindTool(string name)
    {
        var local = Path.Combine(AppContext.BaseDirectory, name);
        if (File.Exists(local)) return local;
        var path = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator).FirstOrDefault(folder => File.Exists(Path.Combine(folder, name)));
        return path;
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAsync(string executable, IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(cancellationToken));
        return (process.ExitCode, await stdout, await stderr);
    }
}

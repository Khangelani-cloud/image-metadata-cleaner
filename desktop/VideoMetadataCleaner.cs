using System.Diagnostics;
using System.Text;

namespace ImageMetadataCleaner;

internal sealed record VideoCleanResult(string OutputPath, List<string> Removed, List<string> Metadata, string? Warning);

internal static class VideoMetadataCleaner
{
    private static readonly string[] SupportedExtensions = [".mp4", ".mov", ".avi", ".m4v", ".m4a", ".3gp", ".3g2", ".webm", ".mkv"];

    public static bool IsSupported(string path) => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static async Task<VideoCleanResult> CleanAsync(string source, string destination, CancellationToken cancellationToken = default)
    {
        var ffmpeg = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (!File.Exists(ffmpeg)) throw new InvalidOperationException("The video engine is missing. Please reinstall the app.");

        var metadata = await ReadMetadataAsync(ffmpeg, source, cancellationToken);
        var removed = metadata.ToList();
        var args = $"-hide_banner -loglevel error -y -i {Quote(source)} -map 0 -map_metadata -1 -map_chapters -1 -c copy {Quote(destination)}";
        var output = await RunAsync(ffmpeg, args, cancellationToken);
        if (output.ExitCode != 0)
        {
            TryDelete(destination);
            throw new InvalidDataException(string.IsNullOrWhiteSpace(output.Error) ? "FFmpeg could not process this video." : output.Error.Trim());
        }

        return new VideoCleanResult(destination, removed, metadata, "Standard container metadata was removed without re-encoding the video. Vendor-specific or signed provenance boxes may require separate verification.");
    }

    private static async Task<List<string>> ReadMetadataAsync(string ffmpeg, string source, CancellationToken cancellationToken)
    {
        var result = await RunAsync(ffmpeg, $"-hide_banner -i {Quote(source)}", cancellationToken);
        var lines = result.Error.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        return lines.Where(x => x.TrimStart().StartsWith("Metadata:", StringComparison.OrdinalIgnoreCase) || x.StartsWith("    "))
            .Select(x => x.Trim()).Where(x => x.Length > 0 && !x.Equals("Metadata:", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task<(int ExitCode, string Error)> RunAsync(string executable, string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 } };
        process.Start();
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, error);
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}

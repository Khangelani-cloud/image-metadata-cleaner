using System.Text.RegularExpressions;

namespace ImageMetadataCleaner;

internal enum FileNamingFormat
{
    CurrentStyle,
    CustomNameWithPosition,
    CustomNameWithOriginalNumber,
    CustomNameWithSequence,
    CustomName,
    DateAndFilename,
    FilenameWithSequence,
    Filename
}

internal static class FileNaming
{
    public static IReadOnlyList<string> FormatNames { get; } =
    [
        "Current style",
        "Custom name (item x of y)",
        "Custom name + original number",
        "Custom name + sequence",
        "Custom name",
        "Date + filename",
        "Filename + sequence",
        "Filename"
    ];

    public static bool RequiresCustomName(FileNamingFormat format) =>
        format is FileNamingFormat.CustomNameWithPosition
            or FileNamingFormat.CustomNameWithOriginalNumber
            or FileNamingFormat.CustomNameWithSequence
            or FileNamingFormat.CustomName;

    public static bool UsesSequence(FileNamingFormat format) =>
        format is FileNamingFormat.CustomNameWithSequence or FileNamingFormat.FilenameWithSequence;

    public static string CreateFileName(
        string sourcePath,
        int ordinal,
        int total,
        int startingNumber,
        FileNamingFormat format,
        string customName,
        string extension)
    {
        var sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        var safeSourceName = Sanitize(sourceName, "media");
        var safeCustomName = Sanitize(customName, "Cleaned");
        var sequence = ((long)startingNumber + ordinal - 1).ToString("D4");
        var originalNumber = Regex.Match(sourceName, @"\d+$").Value;
        if (string.IsNullOrEmpty(originalNumber)) originalNumber = ordinal.ToString("D4");
        if (originalNumber.Length > 100) originalNumber = originalNumber[^100..];

        var stem = format switch
        {
            FileNamingFormat.CurrentStyle => $"{ordinal:0000}_{safeSourceName}_cleaned",
            FileNamingFormat.CustomNameWithPosition => $"{safeCustomName}_{ordinal:0000}-of-{total:0000}",
            FileNamingFormat.CustomNameWithOriginalNumber => $"{safeCustomName}_{originalNumber}",
            FileNamingFormat.CustomNameWithSequence => $"{safeCustomName}_{sequence}",
            FileNamingFormat.CustomName => safeCustomName,
            FileNamingFormat.DateAndFilename => $"{(File.Exists(sourcePath) ? File.GetLastWriteTime(sourcePath) : DateTime.Today):yyyy-MM-dd}_{safeSourceName}",
            FileNamingFormat.FilenameWithSequence => $"{safeSourceName}_{sequence}",
            FileNamingFormat.Filename => safeSourceName,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown file naming format.")
        };

        return stem + extension;
    }

    public static string MakeUniqueName(string folder, string fileName)
    {
        var candidate = fileName;
        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var suffix = 1;
        while (File.Exists(Path.Combine(folder, candidate)))
            candidate = $"{stem}_{suffix++}{extension}";
        return candidate;
    }

    private static string Sanitize(string value, string fallback)
    {
        var safe = Regex.Replace(value, @"[^\p{L}\p{Nd}._ -]", "_").Trim(' ', '.');
        if (safe.Length > 100) safe = safe[..100];
        return string.IsNullOrWhiteSpace(safe) ? fallback : safe;
    }
}

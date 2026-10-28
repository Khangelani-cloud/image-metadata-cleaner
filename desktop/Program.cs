using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ImageMetadataCleaner;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var updateLock = new Mutex(true, @"Local\ImageMetadataCleanerRunning", out _);
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed partial class MainForm : Form
{
    private readonly ListBox _files = new() { Dock = DockStyle.Fill, SelectionMode = SelectionMode.MultiExtended, AccessibleName = "Selected JPEG images" };
    private readonly BufferedDataGridView _results = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, BackgroundColor = UiTheme.Surface, AccessibleName = "Batch results" };
    private readonly List<string> _paths = new();
    private bool _busy;

    public MainForm()
    {
        Text = "Image Metadata Cleaner";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        MinimumSize = new Size(900, 1100);
        Size = new Size(1180, 1100);
        BackColor = UiTheme.Window;
        ForeColor = UiTheme.Text;
        Font = UiTheme.GetFont(UiTheme.BodyFont);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SuspendLayout();

        var outer = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, BackColor = UiTheme.Window, Padding = new Padding(UiTheme.Scale(this, UiTheme.Space32), UiTheme.Scale(this, UiTheme.Space16), UiTheme.Scale(this, UiTheme.Space32), UiTheme.Scale(this, UiTheme.Space16)), ColumnCount = 1, RowCount = 5 };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(outer);

        outer.Controls.Add(BuildHeader(), 0, 0);
        outer.Controls.Add(BuildImagesCard(), 0, 1);
        outer.Controls.Add(BuildOptionsCard(), 0, 2);
        outer.Controls.Add(BuildReportCard(), 0, 3);
        outer.Controls.Add(BuildFooter(), 0, 4);
        ResumeLayout(true);

        _files.SelectedIndexChanged += (_, _) =>
        {
            _process.Enabled = _paths.Count > 0 && !_busy;
            _removeSelectedButton.Enabled = _files.SelectedItems.Count > 0;
        };
        _process.Click += async (_, _) => await ProcessBatchAsync();
        Shown += async (_, _) => await UpdateService.CheckForUpdatesAsync(this, quiet: true);
    }

    private static bool IsJpeg(string path) => MediaInspector.IsJpeg(path);
    private static bool IsVideo(string path) => MediaInspector.IsVideo(path);

    private void AddPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(MediaInspector.IsSupported).Where(File.Exists))
            if (!_paths.Contains(path, StringComparer.OrdinalIgnoreCase)) _paths.Add(path);
        RefreshFileList();
    }

    private void RefreshFileList()
    {
        _files.Items.Clear();
        foreach (var path in _paths) _files.Items.Add(path);
        _process.Enabled = _paths.Count > 0 && !_busy;
        _removeSelectedButton.Enabled = _files.SelectedItems.Count > 0;
        _clearListButton.Enabled = _paths.Count > 0;
        UpdateDropZone();
    }

    private async Task ProcessBatchAsync()
    {
        if (_busy || _paths.Count == 0) return;
        var selected = _paths.ToArray();
        var target = _output.Text.Trim();
        if (string.IsNullOrWhiteSpace(target)) { MessageBox.Show(this, "Choose a folder for the cleaned copies.", "Choose a save folder", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        try { Directory.CreateDirectory(target); }
        catch (Exception ex) { MessageBox.Show(this, $"That folder could not be used.\n\n{ex.Message}", "Cannot save there", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

        target = Path.Combine(target, "Cleaned media " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"));
        try { Directory.CreateDirectory(target); }
        catch (Exception ex) { MessageBox.Show(this, $"A new batch folder could not be created.\n\n{ex.Message}", "Cannot save there", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

        _busy = true;
        _process.Enabled = false;
        _results.Rows.Clear();
        SetCompletedFolder(null);
        _progress.Value = 0;
        _progressCaption.Text = $"Processing 0 of {selected.Length:N0} files";
        var removeXmp = _allOption.SelectedState;
        var records = new List<ReportRow>();
        var cleanedCount = 0;
        var skippedCount = 0;
        for (var index = 0; index < selected.Length; index++)
        {
            var path = selected[index];
            string? partialOutputPath = null;
            var outputCreated = false;
            _progressCaption.Text = $"Inspecting {index + 1:N0} of {selected.Length:N0}: {Path.GetFileName(path)}";
            try
            {
                if (IsVideo(path))
                {
                    var result = await MediaInspector.InspectAsync(path);
                    var videoRow = new ReportRow { File = path, MediaKind = result.MediaKind, Status = result.Status, Error = result.Error, Findings = result.Findings, Metadata = result.ImageMetadata, VideoMetadata = result.VideoMetadata };
                    if (result.IsSupported)
                    {
                        var outputName = MakeUniqueName(target, Path.GetFileNameWithoutExtension(path), index + 1, Path.GetExtension(path));
                        partialOutputPath = Path.Combine(target, outputName);
                        var removed = await Task.Run(() =>
                        {
                            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                            using var output = new FileStream(partialOutputPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                            outputCreated = true;
                            return IsoBmffInspector.Clean(input, output);
                        });
                        videoRow.Status = "Cleaned";
                        videoRow.Output = outputName;
                        videoRow.Removed = removed;
                        videoRow.BytesIn = new FileInfo(path).Length;
                        videoRow.BytesOut = new FileInfo(partialOutputPath).Length;
                        videoRow.Mode = "Metadata boxes cleared; media payload preserved";
                        cleanedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                    records.Add(videoRow);
                    AddResultRow(videoRow);
                }
                else
                {
                    var data = await File.ReadAllBytesAsync(path);
                    var info = JpegMetadata.Inspect(data);
                    var (cleaned, removed) = JpegMetadata.Clean(data, removeXmp);
                    var fileBase = Path.GetFileNameWithoutExtension(path);
                    var outputName = MakeUniqueName(target, fileBase, index + 1);
                    await File.WriteAllBytesAsync(Path.Combine(target, outputName), cleaned);
                    var photoRow = new ReportRow { File = path, MediaKind = MediaKind.Image, Status = "Cleaned", Output = outputName, Metadata = info, Removed = removed, BytesIn = data.Length, BytesOut = cleaned.Length, Mode = removeXmp ? "C2PA + all XMP" : "C2PA only" };
                    records.Add(photoRow); cleanedCount++;
                    AddResultRow(photoRow);
                }
            }
            catch (Exception ex)
            {
                if (outputCreated && partialOutputPath is not null && File.Exists(partialOutputPath)) File.Delete(partialOutputPath);
                var errorRow = new ReportRow { File = path, MediaKind = IsVideo(path) ? MediaKind.Video : MediaKind.Image, Status = IsVideo(path) ? "Failed" : "Skipped", Error = ex.Message };
                records.Add(errorRow); skippedCount++;
                AddResultRow(errorRow);
            }
            _progress.Value = (int)Math.Round((index + 1d) / selected.Length * 100);
            _progressCaption.Text = $"Processing {index + 1:N0} of {selected.Length:N0} files";
            await Task.Yield();
        }
        try
        {
            await File.WriteAllTextAsync(Path.Combine(target, "metadata-report.json"), JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
            var csv = BuildCsv(records);
            await File.WriteAllTextAsync(Path.Combine(target, "metadata-report.csv"), "\uFEFF" + csv, Encoding.UTF8);
            await File.WriteAllTextAsync(Path.Combine(target, "README - metadata report.txt"), "Metadata Cleaner batch\r\n\r\nOriginal files were not changed. Cleaned copies are saved in this folder.\r\nVideo copies are made for MP4, M4V, and MOV files by clearing supported QuickTime/ISO-BMFF metadata boxes and creation/modification timestamps. Encoded audio/video payloads are preserved without recompression. This can also remove chapters or other data stored inside user-metadata boxes.\r\nC2PA details in the report are embedded claims only. Signatures, certificates, and trust were not verified.\r\nThe ‘C2PA and all XMP’ option removes complete XMP packets from JPEG images. XMP can include ordinary descriptions, dates, copyright, software information, and edit history.\r\nJPEG copies preserve the compressed image scan without recompression and keep EXIF, IPTC, ICC, and JFIF data.\r\n");
        }
        catch (Exception ex) { MessageBox.Show(this, $"The images were processed, but the reports could not be saved.\n\n{ex.Message}", "Report save issue", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        _busy = false;
        _process.Enabled = _paths.Count > 0;
        SetCompletedFolder(target);
        _progressCaption.Text = $"Finished · {selected.Length:N0} of {selected.Length:N0} processed";
        MessageBox.Show(this, $"Finished processing {selected.Length:N0} item(s).\n\nCleaned copies: {cleanedCount:N0}\nSkipped: {skippedCount:N0}\n\nThe originals were not changed.\n\nSave folder:\n{target}", "Batch complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void AddResultRow(ReportRow row)
    {
        var meta = row.Metadata;
        var found = row.Findings is { Count: > 0 } ? row.Findings.ToList() : new List<string>();
        if (row.MediaKind == MediaKind.Image && meta != null)
        {
            if (meta.Exif.Present) found.Add("EXIF");
            if (meta.Iptc.Present) found.Add("IPTC");
            if (meta.Xmp.Present) found.Add("XMP");
            if (meta.C2pa.Present) found.Add("C2PA");
        }
        found = found.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var claims = row.MediaKind == MediaKind.Image && meta?.C2pa.Claims.Count > 0 ? string.Join(", ", meta.C2pa.Claims) : row.VideoMetadata?.C2PaClaims.Count > 0 ? string.Join(", ", row.VideoMetadata.C2PaClaims) : "—";
        var displayStatus = row.Status;
        if (string.IsNullOrWhiteSpace(displayStatus)) displayStatus = row.Error != null ? "Failed" : row.Removed.Count == 0 ? "Nothing found" : "Cleaned";
        var typeText = row.MediaKind == MediaKind.Video ? "Video" : "Photo";
        var metadataText = row.Error is not null
            ? "Failed: " + (row.Error.Length > 280 ? row.Error[..280] + "…" : row.Error)
            : found.Count == 0 ? "No target metadata" : string.Join(", ", found);
        var index = _results.Rows.Add(Path.GetFileName(row.File), displayStatus, typeText, metadataText, claims);
        _results.Rows[index].Cells[0].ToolTipText = row.File;
        _results.Rows[index].Cells[1].ToolTipText = row.Error ?? displayStatus;
        _results.Rows[index].Cells[3].ToolTipText = row.Error ?? (found.Count == 0 ? "No target metadata found." : string.Join(", ", found));
        _results.Rows[index].Cells[4].ToolTipText = claims;
        _results.Invalidate();
    }

    private static string MakeUniqueName(string folder, string stem, int ordinal, string extension = ".jpg")
    {
        var safe = Regex.Replace(stem, @"[^\p{L}\p{Nd}._ -]", "_").Trim(' ', '.');
        if (safe.Length > 100) safe = safe[..100];
        if (string.IsNullOrWhiteSpace(safe)) safe = "media";
        var candidate = $"{ordinal:0000}_{safe}_cleaned{extension}";
        var suffix = 1;
        while (File.Exists(Path.Combine(folder, candidate))) candidate = $"{ordinal:0000}_{safe}_cleaned_{suffix++}{extension}";
        return candidate;
    }

    private static string BuildCsv(IEnumerable<ReportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("File,MediaKind,Status,Output,Mode,Removed,EXIF,IPTC,XMP,C2PA,ICC profile,JFIF,C2PA claim hints,Error");
        foreach (var r in rows)
        {
            var m = r.Metadata;
            var values = new[] { r.File, r.MediaKind.ToString(), r.Status, r.Output ?? "", r.Mode ?? "", string.Join("; ", r.Removed), Bool(m?.Exif.Present), Bool(m?.Iptc.Present), Bool(m?.Xmp.Present), Bool(m?.C2pa.Present), Bool(m?.IccProfile), Bool(m?.Jfif), string.Join("; ", m?.C2pa.Claims ?? new()), r.Error ?? "" };
            sb.AppendLine(string.Join(",", values.Select(CsvEscape)));
        }
        return sb.ToString();
    }

    private static string Bool(bool? value) => value.HasValue ? value.Value.ToString() : "";
    private static string CsvEscape(string value) => "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
}

internal sealed class ReportRow
{
    public string File { get; set; } = "";
    public MediaKind MediaKind { get; set; } = MediaKind.Image;
    public string Status { get; set; } = "";
    public string? Output { get; set; }
    public string? Mode { get; set; }
    public List<string> Removed { get; set; } = new();
    public MetadataInfo? Metadata { get; set; }
    public VideoMetadataInfo? VideoMetadata { get; set; }
    public List<string> Findings { get; set; } = new();
    public long BytesIn { get; set; }
    public long BytesOut { get; set; }
    public string? Error { get; set; }
}

internal sealed class MetadataInfo
{
    public bool ValidJpeg { get; set; } = true;
    public ExifInfo Exif { get; set; } = new();
    public IptcInfo Iptc { get; set; } = new();
    public XmpInfo Xmp { get; set; } = new();
    public C2paInfo C2pa { get; set; } = new();
    public bool IccProfile { get; set; }
    public bool Jfif { get; set; }
}

internal sealed class ExifInfo { public bool Present { get; set; } public Dictionary<string, string> Fields { get; set; } = new(); public bool GpsIfdPresent { get; set; } }
internal sealed class IptcInfo { public bool Present { get; set; } public List<string> Fields { get; set; } = new(); public List<string> PhotoshopResourceIds { get; set; } = new(); }
internal sealed class XmpInfo { public bool Present { get; set; } public Dictionary<string, string> Fields { get; set; } = new(); public bool C2paTerms { get; set; } }
internal sealed class C2paInfo { public bool Present { get; set; } public List<string> Claims { get; set; } = new(); public bool SignatureVerified { get; set; } }

internal static class JpegMetadata
{
    private sealed record Segment(int Marker, byte[] Payload, byte[] Raw);

    public static MetadataInfo Inspect(byte[] data)
    {
        var segs = Parse(data);
        var payloads = segs.Select(s => s.Payload).ToList();
        var photoshop = payloads.Where(p => Starts(p, "Photoshop 3.0\0"u8)).ToList();
        var resources = photoshop.SelectMany(Resources).ToList();
        var xmp = payloads.Where(IsXmp).ToList();
        return new MetadataInfo
        {
            Exif = ReadExif(payloads),
            Iptc = new IptcInfo { Present = resources.Any(r => r.Id == 0x0404), Fields = ReadIptc(resources), PhotoshopResourceIds = resources.Select(r => $"0x{r.Id:x4}").Distinct().ToList() },
            Xmp = ReadXmp(xmp),
            C2pa = ReadC2pa(segs),
            IccProfile = payloads.Any(p => Starts(p, "ICC_PROFILE\0"u8)),
            Jfif = payloads.Any(p => Starts(p, "JFIF\0"u8)),
        };
    }

    public static (byte[] Data, List<string> Removed) Clean(byte[] data, bool removeXmp)
    {
        var segs = Parse(data);
        var c2paIndices = new HashSet<int>();
        var i = 0;
        while (i < segs.Count)
        {
            if (segs[i].Marker != 0xEB) { i++; continue; }
            var start = i;
            while (i < segs.Count && segs[i].Marker == 0xEB) i++;
            var group = segs.Skip(start).Take(i - start).ToList();
            if (group.Any(s => Contains(s.Payload, "jumb") && Contains(s.Payload, "c2pa")))
                for (var j = start; j < i; j++) c2paIndices.Add(j);
        }
        using var output = new MemoryStream(data.Length);
        output.Write(data, 0, 2);
        var removed = new List<string>();
        for (var index = 0; index < segs.Count; index++)
        {
            var seg = segs[index];
            if (c2paIndices.Contains(index)) { removed.Add("C2PA/JUMBF APP11"); continue; }
            if (removeXmp && seg.Marker == 0xE1 && IsXmp(seg.Payload)) { removed.Add("XMP APP1"); continue; }
            output.Write(seg.Raw);
        }
        return (output.ToArray(), removed.Distinct().ToList());
    }

    private static List<Segment> Parse(byte[] data)
    {
        if (data.Length < 4 || data[0] != 0xff || data[1] != 0xd8) throw new InvalidDataException("This file is not a valid JPEG image.");
        var result = new List<Segment>();
        var i = 2;
        while (i < data.Length)
        {
            var start = i;
            if (data[i] != 0xff) throw new InvalidDataException("The JPEG marker sequence is damaged.");
            while (i < data.Length && data[i] == 0xff) i++;
            if (i >= data.Length) throw new InvalidDataException("The JPEG ended unexpectedly.");
            var marker = data[i++];
            if (marker == 0xda) { result.Add(new Segment(marker, Array.Empty<byte>(), data[start..])); return result; }
            if (marker == 0xd9) { result.Add(new Segment(marker, Array.Empty<byte>(), data[start..i])); return result; }
            if (marker == 0x01 || marker is >= 0xd0 and <= 0xd7) { result.Add(new Segment(marker, Array.Empty<byte>(), data[start..i])); continue; }
            if (i + 2 > data.Length) throw new InvalidDataException("The JPEG ended before a segment length.");
            var length = (data[i] << 8) | data[i + 1];
            var end = i + length;
            if (length < 2 || end > data.Length) throw new InvalidDataException("The JPEG has an invalid metadata segment.");
            result.Add(new Segment(marker, data[(i + 2)..end], data[start..end]));
            i = end;
        }
        throw new InvalidDataException("The JPEG image scan could not be found.");
    }

    private static ExifInfo ReadExif(List<byte[]> payloads)
    {
        var p = payloads.FirstOrDefault(x => Starts(x, "Exif\0\0"u8));
        if (p == null) return new ExifInfo();
        var t = p[6..];
        if (t.Length < 8) return new ExifInfo { Present = true };
        var little = t[0] == (byte)'I' && t[1] == (byte)'I';
        if (!little && !(t[0] == (byte)'M' && t[1] == (byte)'M')) return new ExifInfo { Present = true };
        ushort U16(int o) => o + 2 <= t.Length ? (little ? (ushort)(t[o] | t[o + 1] << 8) : (ushort)(t[o] << 8 | t[o + 1])) : (ushort)0;
        uint U32(int o) => o + 4 <= t.Length ? (little ? (uint)(t[o] | t[o + 1] << 8 | t[o + 2] << 16 | t[o + 3] << 24) : (uint)(t[o] << 24 | t[o + 1] << 16 | t[o + 2] << 8 | t[o + 3])) : 0;
        var info = new ExifInfo { Present = true };
        var wanted = new Dictionary<int, string> { [0x010f] = "Make", [0x0110] = "Model", [0x0131] = "Software", [0x0132] = "DateTime", [0x9003] = "DateTimeOriginal" };
        var queue = new Queue<uint>(); queue.Enqueue(U32(4));
        var seen = new HashSet<uint>();
        while (queue.Count > 0 && seen.Count < 16)
        {
            var offset = queue.Dequeue();
            if (offset == 0 || !seen.Add(offset) || offset > int.MaxValue) continue;
            var o = (int)offset;
            var count = Math.Min(U16(o), (ushort)512);
            for (var j = 0; j < count; j++)
            {
                var pos = o + 2 + j * 12;
                if (pos + 12 > t.Length) break;
                var tag = U16(pos); var type = U16(pos + 2); var n = U32(pos + 4);
                if (tag == 0x8825) info.GpsIfdPresent = true;
                if ((tag is 0x8769 or 0xa005) && type == 4 && n == 1) queue.Enqueue(U32(pos + 8));
                if (!wanted.TryGetValue(tag, out var name) || type is not (2 or 7) || n == 0 || n > 2048) continue;
                var valueOffset = n <= 4 ? (uint)(pos + 8) : U32(pos + 8);
                if (valueOffset >= t.Length) continue;
                var size = (int)Math.Min(n, (uint)(t.Length - valueOffset));
                var bytes = t.AsSpan((int)valueOffset, size);
                var nul = bytes.IndexOf((byte)0); if (nul >= 0) bytes = bytes[..nul];
                var value = Encoding.UTF8.GetString(bytes).Trim(); if (value.Length > 0) info.Fields[name] = value;
            }
            var next = U32(o + 2 + count * 12); if (next != 0) queue.Enqueue(next);
        }
        return info;
    }

    private sealed record Resource(int Id, byte[] Data);
    private static List<Resource> Resources(byte[] p)
    {
        var result = new List<Resource>();
        var prefix = "Photoshop 3.0\0"u8;
        if (!Starts(p, prefix)) return result;
        var i = prefix.Length;
        while (i + 7 <= p.Length && p.AsSpan(i, 4).SequenceEqual("8BIM"u8))
        {
            var id = (p[i + 4] << 8) | p[i + 5]; i += 6;
            var nameLen = p[i++]; i += nameLen;
            if ((1 + nameLen) % 2 != 0) i++;
            if (i + 4 > p.Length) break;
            var size = (p[i] << 24) | (p[i + 1] << 16) | (p[i + 2] << 8) | p[i + 3]; i += 4;
            if (size < 0 || i + size > p.Length) break;
            result.Add(new Resource(id, p.AsSpan(i, size).ToArray()));
            i += size + size % 2;
        }
        return result;
    }

    private static List<string> ReadIptc(List<Resource> resources)
    {
        var names = new Dictionary<int, string> { [5] = "Object name", [25] = "Keywords", [80] = "Byline", [116] = "Copyright", [120] = "Caption" };
        var fields = new List<string>();
        foreach (var resource in resources.Where(r => r.Id == 0x0404))
        {
            var p = resource.Data; var i = 0;
            while (i + 5 <= p.Length)
            {
                if (p[i] != 0x1c) { i++; continue; }
                var record = p[i + 1]; var dataset = p[i + 2]; var size = (p[i + 3] << 8) | p[i + 4]; i += 5;
                if ((size & 0x8000) != 0)
                {
                    var count = size & 0x7fff; if (count is < 1 or > 4 || i + count > p.Length) break;
                    size = 0; for (var k = 0; k < count; k++) size = (size << 8) | p[i++];
                }
                if (size < 0 || i + size > p.Length) break;
                if (record == 2)
                {
                    var value = Encoding.UTF8.GetString(p, i, size).Trim('\0', ' ');
                    if (value.Length > 0) fields.Add($"{names.GetValueOrDefault(dataset, $"Dataset {dataset}")}: {value[..Math.Min(300, value.Length)]}");
                }
                i += size;
            }
        }
        return fields;
    }

    private static bool IsXmp(byte[] p) => Starts(p, "http://ns.adobe.com/xap/1.0/\0"u8) || Starts(p, "http://ns.adobe.com/xmp/extension/\0"u8);
    private static XmpInfo ReadXmp(List<byte[]> packets)
    {
        var info = new XmpInfo { Present = packets.Count > 0 };
        var text = string.Join("\n", packets.Where(p => Starts(p, "http://ns.adobe.com/xap/1.0/\0"u8)).Select(p => Encoding.UTF8.GetString(p.AsSpan("http://ns.adobe.com/xap/1.0/\0"u8.Length))));
        foreach (var key in new[] { "CreatorTool", "CreateDate", "ModifyDate", "MetadataDate", "RenditionClass" })
        {
            var match = Regex.Match(text, $"<{(key == "RenditionClass" ? "xmpMM" : "xmp")}:{key}[^>]*>(.*?)</{(key == "RenditionClass" ? "xmpMM" : "xmp")}:{key}>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success) info.Fields[key] = System.Net.WebUtility.HtmlDecode(Regex.Replace(match.Groups[1].Value, @"\s+", " ")).Trim();
        }
        info.C2paTerms = Regex.IsMatch(text, "c2pa|contentauth", RegexOptions.IgnoreCase);
        return info;
    }

    private static C2paInfo ReadC2pa(List<Segment> segments)
    {
        var info = new C2paInfo();
        var i = 0;
        while (i < segments.Count)
        {
            if (segments[i].Marker != 0xeb) { i++; continue; }
            var start = i; using var buffer = new MemoryStream();
            var found = false;
            while (i < segments.Count && segments[i].Marker == 0xeb)
            {
                var p = segments[i].Payload;
                if (Contains(p, "jumb") && Contains(p, "c2pa")) found = true;
                buffer.Write(p); i++;
            }
            if (!found) continue;
            info.Present = true;
            var raw = Encoding.Latin1.GetString(buffer.ToArray());
            var patterns = new[] { "OpenAI Media Service API", @"igpt-image.{0,30}c2\.0", @"trainedAlgorithmicMedia|compositeWithTrainedAlgorithmicMedia|algorithmicMedia|digitalCaptureOfAnalogSource|digitalCapture|compositeSynthetic|dataDrivenMedia|virtualRecording|screenCapture|negativeFilm|positiveFilm|print|trainingData", @"c2pa\.[A-Za-z0-9._-]+" };
            foreach (var pattern in patterns)
                foreach (Match m in Regex.Matches(raw, pattern, RegexOptions.IgnoreCase))
                    if (!info.Claims.Contains(m.Value, StringComparer.OrdinalIgnoreCase)) info.Claims.Add(m.Value[..Math.Min(160, m.Value.Length)]);
        }
        return info;
    }

    private static bool Starts(byte[] data, ReadOnlySpan<byte> prefix) => data.AsSpan().StartsWith(prefix);
    private static bool Contains(byte[] data, string ascii) => Encoding.Latin1.GetString(data).Contains(ascii, StringComparison.OrdinalIgnoreCase);
}

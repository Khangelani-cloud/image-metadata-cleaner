using System.Buffers.Binary;
using System.Text;

namespace ImageMetadataCleaner;

internal enum MediaKind
{
    Image,
    Video
}

internal sealed class MediaInspectionResult
{
    public MediaKind MediaKind { get; set; }
    public string Status { get; set; } = "Inspected";
    public List<string> Findings { get; set; } = new();
    public string? Error { get; set; }
    public bool IsSupported { get; set; } = true;
    public MetadataInfo? ImageMetadata { get; set; }
    public VideoMetadataInfo? VideoMetadata { get; set; }
}

internal sealed class VideoMetadataInfo
{
    public string? CreationTime { get; set; }
    public string? ModificationTime { get; set; }
    public string? Duration { get; set; }
    public List<string> Tracks { get; } = new();
    public List<string> OtherMetadata { get; } = new();
    public List<string> Device { get; } = new();
    public List<string> Software { get; } = new();
    public List<string> Tags { get; } = new();
    public string? Location { get; set; }
    public bool XmpPresent { get; set; }
    public string? XmpSummary { get; set; }
    public bool C2PaPresent { get; set; }
    public List<string> C2PaClaims { get; } = new();
}

internal static class MediaInspector
{
    private static readonly HashSet<string> SupportedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov"
    };

    private static readonly HashSet<string> UnsupportedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".avi", ".webm", ".wmv"
    };

    public static bool IsJpeg(string path)
    {
        var ext = Path.GetExtension(path);
        return string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsVideo(string path)
    {
        var ext = Path.GetExtension(path);
        return SupportedVideoExtensions.Contains(ext) || UnsupportedVideoExtensions.Contains(ext);
    }

    public static bool IsSupported(string path)
    {
        return IsJpeg(path) || IsVideo(path);
    }

    public static bool IsKnownVideographicPath(string path)
    {
        var ext = Path.GetExtension(path);
        return SupportedVideoExtensions.Contains(ext);
    }

    public static async Task<MediaInspectionResult> InspectAsync(string path, CancellationToken cancellationToken = default)
    {
        if (IsJpeg(path))
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            var info = JpegMetadata.Inspect(bytes);
            return new MediaInspectionResult
            {
                MediaKind = MediaKind.Image,
                Status = info.C2pa.Present || info.Xmp.Present || info.Exif.Present || info.Iptc.Present ? "Inspected" : "Nothing found",
                IsSupported = true,
                ImageMetadata = info,
                Findings = BuildImageFindings(info)
            };
        }

        var extension = Path.GetExtension(path);
        if (UnsupportedVideoExtensions.Contains(extension))
        {
            return new MediaInspectionResult
            {
                MediaKind = MediaKind.Video,
                Status = "Not supported yet",
                IsSupported = false,
                Findings = new List<string> { "Not supported yet" }
            };
        }

        if (!SupportedVideoExtensions.Contains(extension))
        {
            return new MediaInspectionResult
            {
                MediaKind = MediaKind.Video,
                Status = "Not supported yet",
                IsSupported = false,
                Findings = new List<string> { "Not supported yet" }
            };
        }

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Read);
            var result = IsoBmffInspector.Inspect(stream, cancellationToken);
            result.MediaKind = MediaKind.Video;
            result.Status = result.Findings.Count == 0 ? "Nothing found" : "Inspected";
            return result;
        }
        catch (Exception ex)
        {
            return new MediaInspectionResult
            {
                MediaKind = MediaKind.Video,
                Status = "Failed",
                Error = ex.Message,
                IsSupported = true,
                Findings = new List<string> { "Failed" }
            };
        }
    }

    private static List<string> BuildImageFindings(MetadataInfo metadata)
    {
        var findings = new List<string>();
        if (metadata.Exif.Present) findings.Add("EXIF");
        if (metadata.Iptc.Present) findings.Add("IPTC");
        if (metadata.Xmp.Present) findings.Add("XMP");
        if (metadata.C2pa.Present) findings.Add("C2PA");
        if (metadata.IccProfile) findings.Add("ICC profile");
        if (metadata.Jfif) findings.Add("JFIF");
        return findings.Count == 0 ? new List<string> { "No target metadata" } : findings;
    }
}

internal static class IsoBmffInspector
{
    private const int MaxDepth = 32;
    private const long MaxReadBytesPerBox = 4L * 1024 * 1024;

    private static readonly Guid XmpUuid = Guid.Parse("BE7ACFC0-4B4F-11E7-8F0E-0002A5D5C51B");
    private static readonly Guid C2PaUuid = Guid.Parse("EDBA941D-48D5-4A5C-93A6-3816B1D1D4E8");

    public static MediaInspectionResult Inspect(Stream stream, CancellationToken cancellationToken)
    {
        var result = new MediaInspectionResult { MediaKind = MediaKind.Video, Status = "Inspected" };
        var video = new VideoMetadataInfo();
        var boxes = new List<BoxInfo>();
        var rootEnd = stream.Length;
        WalkBoxes(stream, 0, rootEnd, 0, "file", boxes, cancellationToken);

        foreach (var box in boxes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(box.Type, "moov", StringComparison.Ordinal) || string.Equals(box.Type, "trak", StringComparison.Ordinal) || string.Equals(box.Type, "mdia", StringComparison.Ordinal))
            {
                result.Findings.Add(box.Type.ToUpperInvariant());
            }

            if (box.Type == "uuid" && box.Uuid is not null)
            {
                var uuid = new Guid(box.Uuid, bigEndian: true);
                if (uuid == XmpUuid)
                {
                    video.XmpPresent = true;
                    video.XmpSummary = "XMP present";
                    result.Findings.Add("XMP");
                }
                if (uuid == C2PaUuid)
                {
                    video.C2PaPresent = true;
                    video.C2PaClaims.Add("C2PA manifest present");
                    result.Findings.Add("C2PA");
                }
            }

            if (box.Type == "XMP ")
            {
                video.XmpPresent = true;
                video.XmpSummary = "XMP present";
                result.Findings.Add("XMP");
            }

            if (box.Type == "©xyz" || box.Type == "loci")
            {
                var location = ReadLocation(box.Payload);
                if (!string.IsNullOrWhiteSpace(location))
                {
                    video.Location = location;
                    result.Findings.Add("GPS");
                }
            }

            if (box.Type == "keys")
            {
                var text = ReadMetadataSummary(box.Payload);
                if (!string.IsNullOrWhiteSpace(text) &&
                    (text.Contains("com.apple.quicktime.location.ISO6709", StringComparison.OrdinalIgnoreCase) ||
                     text.Contains("loci", StringComparison.OrdinalIgnoreCase) ||
                     text.Contains("©xyz", StringComparison.OrdinalIgnoreCase)))
                {
                    video.Location ??= text;
                    result.Findings.Add("GPS");
                }
            }

            if (box.Type == "©nam" || box.Type == "©ART" || box.Type == "©cmt" || box.Type == "©cprt")
            {
                var text = ReadText(box.Payload);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    video.Tags.Add(box.Type + ": " + text);
                    result.Findings.Add("Tags");
                }
            }

            if (box.Type == "©mak" || box.Type == "©mod")
            {
                var text = ReadText(box.Payload);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    video.Device.Add(text);
                    result.Findings.Add("Device");
                }
            }

            if (box.Type == "©too" || box.Type == "©swr")
            {
                var text = ReadText(box.Payload);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    video.Software.Add(text);
                    result.Findings.Add("Software");
                }
            }

            if (box.Type == "mvhd" || box.Type == "tkhd" || box.Type == "mdhd")
            {
                var value = ReadDate(box.Payload, box.Type);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (box.Type == "mvhd") video.CreationTime ??= value;
                    if (box.Type == "mdhd") video.ModificationTime ??= value;
                    result.Findings.Add("Date");
                }
            }

            if (box.Type == "keys")
            {
                var note = ReadMetadataSummary(box.Payload);
                if (!string.IsNullOrWhiteSpace(note))
                {
                    video.OtherMetadata.Add(note);
                    result.Findings.Add("Other metadata");
                }
            }
        }

        var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();
        foreach (var item in result.Findings)
            if (distinct.Add(item)) ordered.Add(item);
        result.Findings.Clear();
        foreach (var item in ordered) result.Findings.Add(item);

        if (video.XmpPresent || video.C2PaPresent || video.Location is not null || video.Device.Count > 0 || video.Software.Count > 0 || video.Tags.Count > 0 || video.OtherMetadata.Count > 0 || video.CreationTime is not null || video.ModificationTime is not null || video.Duration is not null)
        {
            result.VideoMetadata = video;
            if (result.Findings.Count == 0) result.Findings.Add("Metadata");
        }
        else if (result.Findings.Count == 0)
        {
            result.Status = "Nothing found";
        }

        return result;
    }

    public static List<string> Clean(Stream input, Stream output, CancellationToken cancellationToken = default)
    {
        if (ReferenceEquals(input, output)) throw new ArgumentException("Input and output streams must be different.");
        if (!input.CanRead || !input.CanSeek) throw new ArgumentException("The input stream must be readable and seekable.", nameof(input));
        if (!output.CanWrite || !output.CanSeek) throw new ArgumentException("The output stream must be writable and seekable.", nameof(output));

        var boxes = new List<BoxInfo>();
        WalkBoxes(input, 0, input.Length, 0, "file", boxes, cancellationToken);

        input.Position = 0;
        output.Position = 0;
        output.SetLength(0);
        input.CopyTo(output);
        output.SetLength(input.Length);

        var removed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var metadataRanges = new List<(long Start, long End)>();
        var coveredUntil = -1L;
        foreach (var box in boxes.Where(IsMetadataBox).OrderBy(box => box.Offset))
        {
            removed.Add(GetMetadataCategory(box));
            if (box.Offset < coveredUntil) continue;
            WriteFreeBox(output, box);
            coveredUntil = box.End;
            metadataRanges.Add((box.Offset, box.End));
        }

        var zero = new byte[8];
        foreach (var box in boxes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (box.Type is not ("mvhd" or "tkhd" or "mdhd") || metadataRanges.Any(range => box.Offset < range.End && box.End > range.Start)) continue;
            if (box.Payload.Length < 1) continue;

            var version = box.Payload[0];
            var fieldSize = version == 1 ? 8 : version == 0 ? 4 : 0;
            if (fieldSize == 0 || box.Payload.Length < 4 + fieldSize * 2) continue;
            output.Position = box.PayloadOffset + 4;
            output.Write(zero, 0, fieldSize);
            output.Write(zero, 0, fieldSize);
            removed.Add("Creation/modification timestamps");
        }

        return removed.ToList();
    }

    private static bool IsMetadataBox(BoxInfo box)
    {
        if (box.Type is "meta" or "udta" or "XMP " or "©xyz" or "loci" or "keys" or "ilst"
            or "©nam" or "©ART" or "©cmt" or "©cprt" or "©mak" or "©mod" or "©too" or "©swr") return true;
        if (box.Type != "uuid" || box.Uuid is null) return false;
        var uuid = new Guid(box.Uuid, bigEndian: true);
        return uuid == XmpUuid || uuid == C2PaUuid;
    }

    private static string GetMetadataCategory(BoxInfo box)
    {
        if (box.Type == "uuid" && box.Uuid is not null)
        {
            var uuid = new Guid(box.Uuid, bigEndian: true);
            if (uuid == XmpUuid) return "XMP";
            if (uuid == C2PaUuid) return "C2PA";
        }
        if (box.Type is "XMP ") return "XMP";
        if (box.Type is "©xyz" or "loci") return "Location";
        return "QuickTime metadata";
    }

    private static void WriteFreeBox(Stream output, BoxInfo box)
    {
        output.Position = box.Offset + 4;
        output.Write("free"u8);
        output.Position = box.Offset + box.HeaderSize;
        WriteZeros(output, (long)box.Size - (long)box.HeaderSize);
    }

    private static void WriteZeros(Stream output, long count)
    {
        var zeros = new byte[64 * 1024];
        while (count > 0)
        {
            var length = (int)Math.Min(count, zeros.Length);
            output.Write(zeros, 0, length);
            count -= length;
        }
    }

    private static void WalkBoxes(Stream stream, long parentStart, long parentEnd, int depth, string containerPath, List<BoxInfo> boxes, CancellationToken cancellationToken)
    {
        if (depth > MaxDepth) throw DamagedBox(parentStart, containerPath, "box nesting is too deep");
        if (parentEnd <= parentStart) return;
        stream.Position = parentStart;

        while (stream.Position < parentEnd)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var boxStart = stream.Position;
            if (parentEnd - boxStart < 8) throw DamagedBox(boxStart, containerPath, "fewer than 8 bytes remain for a box header");

            var header = new byte[8];
            ReadExact(stream, header, 8);
            var size = BinaryPrimitives.ReadUInt32BigEndian(header);
            var typeBytes = header.AsSpan(4, 4).ToArray();
            var type = Encoding.Latin1.GetString(typeBytes);
            ulong actualSize = size;
            if (size == 1)
            {
                if (parentEnd - stream.Position < 8) throw DamagedBox(boxStart, containerPath, $"extended-size box '{type}' has a truncated header");
                actualSize = ReadUInt64(stream);
            }
            else if (size == 0)
            {
                actualSize = (ulong)(parentEnd - boxStart);
            }

            var headerSize = size == 1 ? 16UL : 8UL;
            var boxPath = containerPath + "/" + type;
            if (actualSize < headerSize) throw DamagedBox(boxStart, boxPath, "box size is smaller than its header");
            if (type == "uuid" && actualSize < headerSize + 16) throw DamagedBox(boxStart, boxPath, "UUID box does not contain its 16-byte identifier");
            if (actualSize > long.MaxValue) throw DamagedBox(boxStart, boxPath, "box size exceeds supported file offsets");

            var boxEnd = boxStart + (long)actualSize;
            if (boxEnd > parentEnd || boxEnd < boxStart) throw DamagedBox(boxStart, boxPath, $"declared end 0x{boxEnd:X} exceeds container end 0x{parentEnd:X}");

            byte[]? uuid = null;
            long payloadStart = stream.Position;
            if (type == "uuid")
            {
                var uuidBytes = new byte[16];
                ReadExact(stream, uuidBytes, 16);
                uuid = uuidBytes;
                payloadStart = stream.Position;
            }

            var payloadLength = Math.Max(0L, boxEnd - payloadStart);
            byte[] payload = Array.Empty<byte>();
            if (ShouldReadPayload(type) && payloadLength > 0)
            {
                var readLength = (int)Math.Min(payloadLength, MaxReadBytesPerBox);
                payload = ReadPayload(stream, readLength, payloadStart, boxEnd);
            }

            boxes.Add(new BoxInfo(type, boxStart, actualSize, boxEnd, uuid, payload, payloadStart, (int)headerSize));

            if (ShouldRecurseInto(type))
            {
                if (type == "meta")
                {
                    WalkMetaChildren(stream, payloadStart, boxEnd, depth + 1, boxPath, boxes, cancellationToken);
                }
                else
                {
                    WalkBoxes(stream, payloadStart, boxEnd, depth + 1, boxPath, boxes, cancellationToken);
                }
            }

            stream.Position = boxEnd;
        }
    }

    private static void WalkMetaChildren(Stream stream, long payloadStart, long boxEnd, int depth, string boxPath, List<BoxInfo> boxes, CancellationToken cancellationToken)
    {
        if (boxEnd - payloadStart < 4)
        {
            throw DamagedBox(payloadStart, boxPath, "meta box is missing its full-box header");
        }

        var firstChildIndex = boxes.Count;
        try
        {
            WalkBoxes(stream, payloadStart + 4, boxEnd, depth, boxPath, boxes, cancellationToken);
        }
        catch (InvalidDataException fullBoxError)
        {
            boxes.RemoveRange(firstChildIndex, boxes.Count - firstChildIndex);
            try
            {
                WalkBoxes(stream, payloadStart, boxEnd, depth, boxPath, boxes, cancellationToken);
            }
            catch (InvalidDataException)
            {
                boxes.RemoveRange(firstChildIndex, boxes.Count - firstChildIndex);
                throw fullBoxError;
            }
        }
    }

    private static InvalidDataException DamagedBox(long offset, string containerPath, string reason)
    {
        return new InvalidDataException($"Damaged or truncated video metadata: {reason} at offset 0x{offset:X} in {containerPath}.");
    }

    private static bool ShouldRecurseInto(string type)
    {
        return type is "moov" or "trak" or "mdia" or "minf" or "stbl" or "edts" or "udta" or "meta" or "ilst";
    }

    private static bool ShouldReadPayload(string type)
    {
        return type is "uuid" or "XMP " or "©xyz" or "loci" or "keys"
            or "©nam" or "©ART" or "©cmt" or "©cprt" or "©mak" or "©mod" or "©too" or "©swr"
            or "mvhd" or "tkhd" or "mdhd";
    }

    private static byte[] ReadPayload(Stream stream, int payloadLength, long payloadStart, long payloadEnd)
    {
        if (payloadLength <= 0) return Array.Empty<byte>();
        if (payloadStart < 0 || payloadStart > payloadEnd || payloadStart + payloadLength > payloadEnd)
        {
            throw DamagedBox(payloadStart, "payload", $"requested {payloadLength} bytes beyond the box end 0x{payloadEnd:X}");
        }

        var payload = new byte[payloadLength];
        ReadExact(stream, payload, payloadLength);
        return payload;
    }

    private static string ReadText(byte[] payload)
    {
        if (payload.Length == 0) return string.Empty;
        var text = Encoding.UTF8.GetString(payload).Trim('\0', ' ', '\r', '\n');
        return text.Length > 0 ? text : string.Empty;
    }

    private static string? ReadLocation(byte[] payload)
    {
        if (payload.Length < 12) return null;
        try
        {
            var floats = new float[3];
            for (var i = 0; i < 3; i++)
            {
                var offset = i * 4;
                floats[i] = BitConverter.ToSingle(payload, offset);
            }
            var lat = floats[0];
            var lon = floats[1];
            var alt = floats[2];
            return $"{lat:F6}, {lon:F6}, {alt:F6}";
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadDate(byte[] payload, string boxType)
    {
        if (payload.Length < 4) return null;
        var version = payload[0];
        var fieldOffset = 4;
        ulong seconds;
        if (version == 0 && payload.Length >= fieldOffset + 4)
        {
            seconds = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(fieldOffset, 4));
        }
        else if (version == 1 && payload.Length >= fieldOffset + 8)
        {
            seconds = BinaryPrimitives.ReadUInt64BigEndian(payload.AsSpan(fieldOffset, 8));
        }
        else
        {
            return null;
        }

        if (seconds == 0) return null;
        try
        {
            var date = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
            return date.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string? ReadMetadataSummary(byte[] payload)
    {
        if (payload.Length == 0) return null;
        var text = Encoding.UTF8.GetString(payload).Replace("\0", string.Empty);
        return text.Length > 200 ? text[..200] + "…" : text;
    }

    private static ulong ReadUInt64(Stream stream)
    {
        var bytes = new byte[8];
        ReadExact(stream, bytes, 8);
        return BinaryPrimitives.ReadUInt64BigEndian(bytes);
    }

    private static void ReadExact(Stream stream, byte[] buffer, int length)
    {
        var total = 0;
        while (total < length)
        {
            var read = stream.Read(buffer, total, length - total);
            if (read == 0) throw new InvalidOperationException("Damaged or truncated video metadata.");
            total += read;
        }
    }

    private sealed class BoxInfo
    {
        public BoxInfo(string type, long offset, ulong size, long end, byte[]? uuid, byte[] payload, long payloadOffset, int headerSize)
        {
            Type = type;
            Offset = offset;
            Size = size;
            End = end;
            Uuid = uuid;
            Payload = payload;
            PayloadOffset = payloadOffset;
            HeaderSize = headerSize;
        }

        public string Type { get; }
        public long Offset { get; }
        public ulong Size { get; }
        public long End { get; }
        public byte[]? Uuid { get; }
        public byte[] Payload { get; }
        public long PayloadOffset { get; }
        public int HeaderSize { get; }
    }
}

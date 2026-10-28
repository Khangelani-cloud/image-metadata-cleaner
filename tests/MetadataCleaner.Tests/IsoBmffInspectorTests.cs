using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace ImageMetadataCleaner.Tests;

public class IsoBmffInspectorTests
{
    [Fact]
    public void Inspect_ReadsMoovBeforeMdat()
    {
        using var stream = new MemoryStream(BuildFixtureMoovBeforeMdat());
        var result = IsoBmffInspector.Inspect(stream, CancellationToken.None);

        Assert.Equal(MediaKind.Video, result.MediaKind);
        Assert.NotEmpty(result.Findings);
    }

    [Fact]
    public void Inspect_ReadsBigEndianFtypSize()
    {
        using var stream = new MemoryStream(BuildFixtureMoovBeforeMdat());

        var result = IsoBmffInspector.Inspect(stream, CancellationToken.None);

        Assert.Contains("MOOV", result.Findings, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_ReadsMoovAfterMdat()
    {
        using var stream = new MemoryStream(BuildFixtureMoovAfterMdat());
        var result = IsoBmffInspector.Inspect(stream, CancellationToken.None);

        Assert.Equal(MediaKind.Video, result.MediaKind);
        Assert.NotEmpty(result.Findings);
    }

    [Fact]
    public void Inspect_Uses64BitBoxSize()
    {
        using var stream = new MemoryStream(BuildFixtureWith64BitSize());
        var result = IsoBmffInspector.Inspect(stream, CancellationToken.None);

        Assert.Equal(MediaKind.Video, result.MediaKind);
    }

    [Fact]
    public void Inspect_HandlesZeroSizedFinalBox()
    {
        using var stream = new MemoryStream(BuildFixtureWithZeroSizedFinalBox());
        var result = IsoBmffInspector.Inspect(stream, CancellationToken.None);

        Assert.Equal(MediaKind.Video, result.MediaKind);
    }

    [Fact]
    public void Inspect_HandlesTruncatedFile()
    {
        using var stream = new MemoryStream(BuildTruncatedFixture());
        var exception = Assert.Throws<InvalidDataException>(() => IsoBmffInspector.Inspect(stream, CancellationToken.None));

        Assert.Contains("offset 0x", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_HandlesCorruptLoopingBox()
    {
        using var stream = new MemoryStream(BuildCorruptLoopFixture());
        var exception = Assert.Throws<InvalidDataException>(() => IsoBmffInspector.Inspect(stream, CancellationToken.None));

        Assert.Contains("offset 0x", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file/moov/udta", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_ReportsGpsFromQuicktimeCopyrightAtom()
    {
        using var stream = new MemoryStream(BuildGpsFixture());
        var result = IsoBmffInspector.Inspect(stream, CancellationToken.None);

        Assert.Contains("GPS", result.Findings, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_ReportsLocationFromIlstKeys()
    {
        using var stream = new MemoryStream(BuildIlstLocationFixture());
        var result = IsoBmffInspector.Inspect(stream, CancellationToken.None);

        Assert.Contains("GPS", result.Findings, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_ReportsXmpUuidBox()
    {
        using var stream = new MemoryStream(BuildXmpUuidFixture());
        var result = IsoBmffInspector.Inspect(stream, CancellationToken.None);

        Assert.Contains("XMP", result.Findings, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_ReportsC2PaUuidBox()
    {
        using var stream = new MemoryStream(BuildC2PaUuidFixture());
        var result = IsoBmffInspector.Inspect(stream, CancellationToken.None);

        Assert.Contains("C2PA", result.Findings, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_HandlesMetaFullBoxHeader()
    {
        using var stream = new MemoryStream(BuildFixtureWithMetaHeader());
        var result = IsoBmffInspector.Inspect(stream, CancellationToken.None);

        Assert.Contains("XMP", result.Findings, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_HandlesQuickTimeMetaWithoutFullBoxHeader()
    {
        using var stream = new MemoryStream(BuildFixtureWithQuickTimeMetaHeader());
        var result = IsoBmffInspector.Inspect(stream, CancellationToken.None);

        Assert.Contains("XMP", result.Findings, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_SkipsLargeSampleTablePayloadAndContinues()
    {
        using var stream = new MemoryStream(BuildFixtureWithLargeSampleTable());
        var result = IsoBmffInspector.Inspect(stream, CancellationToken.None);

        Assert.Contains("XMP", result.Findings, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MediaInspector_InspectsSupportedMp4File()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp4");
        try
        {
            await File.WriteAllBytesAsync(path, BuildFixtureWithMetaHeader());

            var result = await MediaInspector.InspectAsync(path);

            Assert.Equal("Inspected", result.Status);
            Assert.Null(result.Error);
            Assert.Contains("XMP", result.Findings, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Clean_RemovesVideoMetadataAndPreservesMediaPayload()
    {
        var source = BuildFixtureWithCleanableMetadata();
        using var sourceStream = new MemoryStream(source);
        var before = IsoBmffInspector.Inspect(sourceStream, CancellationToken.None);
        Assert.Contains("XMP", before.Findings);
        Assert.Contains("GPS", before.Findings);
        Assert.Contains("Date", before.Findings);

        using var input = new MemoryStream(source);
        using var output = new MemoryStream();

        var removed = IsoBmffInspector.Clean(input, output);
        var cleaned = output.ToArray();

        Assert.Contains("XMP", removed);
        Assert.Contains("Location", removed);
        Assert.Contains("Creation/modification timestamps", removed);
        Assert.DoesNotContain("private video description", Encoding.UTF8.GetString(cleaned));
        var movieHeaderIndex = cleaned.AsSpan().IndexOf("mvhd"u8);
        Assert.True(movieHeaderIndex >= 0);
        Assert.All(cleaned.AsSpan(movieHeaderIndex + 8, 8).ToArray(), value => Assert.Equal(0, value));
        Assert.True(cleaned.AsSpan().EndsWith(BuildBox("mdat", new byte[] { 0x31, 0x32, 0x33, 0x34 })));
        using var cleanedStream = new MemoryStream(cleaned);
        var after = IsoBmffInspector.Inspect(cleanedStream, CancellationToken.None);
        Assert.DoesNotContain("XMP", after.Findings);
        Assert.DoesNotContain("GPS", after.Findings);
        Assert.DoesNotContain("Date", after.Findings);
    }

    private static byte[] BuildFixtureWithCleanableMetadata()
    {
        using var stream = new MemoryStream();
        var movieHeader = new byte[20];
        movieHeader[7] = 1;
        movieHeader[11] = 2;
        var location = BitConverter.GetBytes(33.9f).Concat(BitConverter.GetBytes(-118.4f)).Concat(BitConverter.GetBytes(10.0f)).ToArray();
        var xmpUuid = Guid.Parse("be7acfc0-4b4f-11e7-8f0e-0002a5d5c51b");
        var xmpBox = BuildBox("uuid", BuildUuidBox(xmpUuid, Encoding.UTF8.GetBytes("private video description")));
        var metadata = BuildBox("meta", new byte[4].Concat(xmpBox).ToArray());
        var userData = BuildBox("udta", BuildBox("©xyz", location));
        WriteBox(stream, "moov", BuildBox("mvhd", movieHeader).Concat(metadata).Concat(userData).ToArray());
        WriteBox(stream, "mdat", new byte[] { 0x31, 0x32, 0x33, 0x34 });
        return stream.ToArray();
    }

    private static byte[] BuildFixtureMoovBeforeMdat()
    {
        var stream = new MemoryStream();
        WriteBox(stream, "ftyp", new byte[] { 0x00, 0x00, 0x00, 0x00 });
        WriteBox(stream, "moov", Array.Empty<byte>());
        WriteBox(stream, "mdat", new byte[] { 0x01, 0x02, 0x03, 0x04 });
        return stream.ToArray();
    }

    private static byte[] BuildFixtureMoovAfterMdat()
    {
        var stream = new MemoryStream();
        WriteBox(stream, "ftyp", new byte[] { 0x00, 0x00, 0x00, 0x00 });
        WriteBox(stream, "mdat", new byte[] { 0x01, 0x02, 0x03, 0x04 });
        WriteBox(stream, "moov", Array.Empty<byte>());
        return stream.ToArray();
    }

    private static byte[] BuildFixtureWith64BitSize()
    {
        var stream = new MemoryStream();
        WriteBox(stream, "ftyp", new byte[] { 0x00 });
        WriteBox64(stream, "free", 4, new byte[] { 0x01, 0x02, 0x03, 0x04 });
        return stream.ToArray();
    }

    private static byte[] BuildFixtureWithZeroSizedFinalBox()
    {
        var stream = new MemoryStream();
        WriteBox(stream, "ftyp", new byte[] { 0x00, 0x00 });
        WriteZeroSizedBox(stream, "free");
        return stream.ToArray();
    }

    private static byte[] BuildTruncatedFixture()
    {
        var stream = new MemoryStream();
        WriteBox(stream, "ftyp", new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });
        stream.Write(new byte[] { 0x01, 0x02, 0x03 }, 0, 3);
        return stream.ToArray();
    }

    private static byte[] BuildCorruptLoopFixture()
    {
        var stream = new MemoryStream();
        var corruptChild = new byte[]
        {
            0xFF, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0x00
        };
        WriteBox(stream, "moov", BuildBox("udta", corruptChild));
        return stream.ToArray();
    }

    private static byte[] BuildGpsFixture()
    {
        var stream = new MemoryStream();
        WriteBox(stream, "moov", BuildBox("udta", BuildBox("©xyz", BitConverter.GetBytes(1.0f).Concat(BitConverter.GetBytes(2.0f)).Concat(BitConverter.GetBytes(3.0f)).ToArray())));
        return stream.ToArray();
    }

    private static byte[] BuildIlstLocationFixture()
    {
        var stream = new MemoryStream();
        var payload = Encoding.UTF8.GetBytes("com.apple.quicktime.location.ISO6709=+33.889-118.401/");
        WriteBox(stream, "moov", BuildBox("udta", BuildBox("ilst", BuildBox("keys", payload))));
        return stream.ToArray();
    }

    private static byte[] BuildXmpUuidFixture()
    {
        var stream = new MemoryStream();
        var uuid = Guid.Parse("be7acfc0-4b4f-11e7-8f0e-0002a5d5c51b");
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        WriteBox(stream, "uuid", BuildUuidBox(uuid, data));
        return stream.ToArray();
    }

    private static byte[] BuildC2PaUuidFixture()
    {
        var stream = new MemoryStream();
        var uuid = Guid.Parse("edba941d-48d5-4a5c-93a6-3816b1d1d4e8");
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        WriteBox(stream, "uuid", BuildUuidBox(uuid, data));
        return stream.ToArray();
    }

    private static byte[] BuildFixtureWithMetaHeader()
    {
        using var stream = new MemoryStream();
        var uuid = Guid.Parse("be7acfc0-4b4f-11e7-8f0e-0002a5d5c51b");
        var metaPayload = new byte[] { 0, 0, 0, 0 }.Concat(BuildBox("uuid", BuildUuidBox(uuid, Array.Empty<byte>()))).ToArray();
        WriteBox(stream, "moov", BuildBox("meta", metaPayload));
        return stream.ToArray();
    }

    private static byte[] BuildFixtureWithQuickTimeMetaHeader()
    {
        using var stream = new MemoryStream();
        var uuid = Guid.Parse("be7acfc0-4b4f-11e7-8f0e-0002a5d5c51b");
        WriteBox(stream, "moov", BuildBox("meta", BuildBox("uuid", BuildUuidBox(uuid, Array.Empty<byte>()))));
        return stream.ToArray();
    }

    private static byte[] BuildFixtureWithLargeSampleTable()
    {
        using var stream = new MemoryStream();
        var uuid = Guid.Parse("be7acfc0-4b4f-11e7-8f0e-0002a5d5c51b");
        var sampleTable = BuildBox("stsz", new byte[4 * 1024 * 1024 + 64])
            .Concat(BuildBox("uuid", BuildUuidBox(uuid, Array.Empty<byte>())))
            .ToArray();
        WriteBox(stream, "moov", BuildBox("trak", BuildBox("mdia", BuildBox("minf", BuildBox("stbl", sampleTable)))));
        return stream.ToArray();
    }

    private static byte[] BuildBox(string type, byte[] payload)
    {
        using var ms = new MemoryStream();
        WriteBox(ms, type, payload);
        return ms.ToArray();
    }

    private static byte[] BuildUuidBox(Guid uuid, byte[] payload)
    {
        using var ms = new MemoryStream();
        var id = new byte[16];
        uuid.TryWriteBytes(id, bigEndian: true, out _);
        ms.Write(id);
        ms.Write(payload);
        return ms.ToArray();
    }

    private static void WriteBox(Stream stream, string type, byte[] payload)
    {
        Span<byte> header = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)(payload.Length + 8));
        stream.Write(header);
        stream.Write(Encoding.Latin1.GetBytes(type.PadRight(4, '\0').Substring(0, 4)), 0, 4);
        stream.Write(payload, 0, payload.Length);
    }

    private static void WriteBox64(Stream stream, string type, int payloadLength, byte[] payload)
    {
        var header = new byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(header, 1);
        BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(8), (ulong)(payloadLength + 16));
        Encoding.Latin1.GetBytes(type.PadRight(4, '\0').Substring(0, 4)).CopyTo(header, 4);
        stream.Write(header, 0, 16);
        stream.Write(payload, 0, payload.Length);
    }

    private static void WriteZeroSizedBox(Stream stream, string type)
    {
        var header = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(header, 0);
        Encoding.Latin1.GetBytes(type.PadRight(4, '\0').Substring(0, 4)).CopyTo(header, 4);
        stream.Write(header, 0, 8);
    }

    private static void WritePropBox(Stream stream, string type, int size, byte[] payload)
    {
        var header = new byte[8 + size];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)(payload.Length + 8));
        Encoding.Latin1.GetBytes(type.PadRight(4, '\0').Substring(0, 4)).CopyTo(header, 4);
        payload.CopyTo(header, 8);
        stream.Write(header, 0, header.Length);
    }
}

using Xunit;

namespace ImageMetadataCleaner.Tests;

public class FileNamingTests
{
    [Theory]
    [InlineData("CurrentStyle", "0002_Summer photo 0042_cleaned.jpg")]
    [InlineData("CustomNameWithPosition", "Portfolio_0002-of-0005.jpg")]
    [InlineData("CustomNameWithOriginalNumber", "Portfolio_0042.jpg")]
    [InlineData("CustomNameWithSequence", "Portfolio_0013.jpg")]
    [InlineData("CustomName", "Portfolio.jpg")]
    [InlineData("FilenameWithSequence", "Summer photo 0042_0013.jpg")]
    [InlineData("Filename", "Summer photo 0042.jpg")]
    public void CreateFileName_FormatsRequestedNamingStyle(string format, string expected)
    {
        var path = Path.Combine(Path.GetTempPath(), "Summer photo 0042.jpg");

        var result = FileNaming.CreateFileName(path, 2, 5, 12, Enum.Parse<FileNamingFormat>(format), "Portfolio", ".jpg");

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CreateFileName_UsesSourceModifiedDate()
    {
        var path = Path.Combine(Path.GetTempPath(), $"photo-{Guid.NewGuid():N}.jpg");
        try
        {
            File.WriteAllBytes(path, []);
            File.SetLastWriteTime(path, new DateTime(2024, 2, 3, 12, 0, 0));

            var result = FileNaming.CreateFileName(path, 1, 1, 1, FileNamingFormat.DateAndFilename, "", ".jpg");

            Assert.StartsWith("2024-02-03_photo-", result, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void CreateFileName_SanitizesCustomName()
    {
        var result = FileNaming.CreateFileName(
            "photo.jpg",
            1,
            1,
            1,
            FileNamingFormat.CustomName,
            @"../My:Photo?",
            ".jpg");

        Assert.Equal("_My_Photo_.jpg", result);
    }

    [Fact]
    public void CreateFileName_UsesBatchPositionWhenSourceHasNoTrailingNumber()
    {
        var result = FileNaming.CreateFileName(
            "photo.jpg",
            3,
            4,
            1,
            FileNamingFormat.CustomNameWithOriginalNumber,
            "Portfolio",
            ".jpg");

        Assert.Equal("Portfolio_0003.jpg", result);
    }

    [Fact]
    public void CreateFileName_PreservesVideoExtension()
    {
        var result = FileNaming.CreateFileName(
            "clip.MOV",
            1,
            1,
            1,
            FileNamingFormat.FilenameWithSequence,
            "",
            ".MOV");

        Assert.Equal("clip_0001.MOV", result);
    }

    [Fact]
    public void MakeUniqueName_AddsSuffixWhenNameAlreadyExists()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"MetadataCleaner-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "photo.jpg"), []);

            var result = FileNaming.MakeUniqueName(folder, "photo.jpg");

            Assert.Equal("photo_1.jpg", result);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

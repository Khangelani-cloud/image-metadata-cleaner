using Xunit;

namespace ImageMetadataCleaner.Tests;

public class UpdateServiceTests
{
    [Theory]
    [InlineData("Image Metadata Cleaner Setup.exe")]
    [InlineData("Image.Metadata.Cleaner.Setup.exe")]
    public void IsInstallerAssetName_RecognizesInstallerName(string assetName)
    {
        Assert.True(UpdateService.IsInstallerAssetName(assetName));
    }

    [Fact]
    public void IsInstallerAssetName_RejectsOtherNames()
    {
        Assert.False(UpdateService.IsInstallerAssetName("Source code.zip"));
    }
}

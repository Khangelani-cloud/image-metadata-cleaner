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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("md5:00000000000000000000000000000000")]
    [InlineData("sha256:invalid")]
    public void IsSha256DigestValid_RejectsMissingOrMalformedDigest(string? digest)
    {
        Assert.False(UpdateService.IsSha256DigestValid(digest, "sha256:" + new string('a', 64)));
    }

    [Fact]
    public void IsSha256DigestValid_RejectsNonHexSha256Digest()
    {
        var digest = "sha256:" + new string('g', 64);

        Assert.False(UpdateService.IsSha256DigestValid(digest, digest));
    }

    [Fact]
    public void IsSha256DigestValid_AcceptsMatchingSha256Digest()
    {
        var digest = "sha256:" + new string('a', 64);

        Assert.True(UpdateService.IsSha256DigestValid(digest, digest));
    }

    [Fact]
    public void IsSha256DigestValid_RejectsMismatchedSha256Digest()
    {
        Assert.False(UpdateService.IsSha256DigestValid(
            "sha256:" + new string('a', 64),
            "sha256:" + new string('b', 64)));
    }
}

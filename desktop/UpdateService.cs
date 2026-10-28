using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics;
using System.Windows.Forms;

namespace ImageMetadataCleaner;

internal static class UpdateService
{
    private const string AssetName = "Image Metadata Cleaner Setup.exe";
    private static readonly HttpClient Client = CreateClient();

    internal static bool IsInstallerAssetName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        static string Normalize(string value) => string.Concat(value.Where(char.IsLetterOrDigit));
        return string.Equals(Normalize(name), Normalize(AssetName), StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ImageMetadataCleaner", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public static async Task CheckForUpdatesAsync(Form owner, bool quiet)
    {
        var repository = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(x => x.Key == "UpdateRepository")?.Value;
        if (string.IsNullOrWhiteSpace(repository) || repository.Contains("YOUR_GITHUB_OWNER", StringComparison.OrdinalIgnoreCase))
        {
            if (!quiet) MessageBox.Show(owner, "Updates are not set up for this copy yet.", "Image Metadata Cleaner", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            using var response = await Client.GetAsync($"https://api.github.com/repos/{repository}/releases/latest");
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
            var release = json.RootElement;
            var tag = release.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latestVersion)) return;
            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
            if (latestVersion <= currentVersion)
            {
                if (!quiet) MessageBox.Show(owner, "You already have the latest version.", "Image Metadata Cleaner", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var asset = release.GetProperty("assets").EnumerateArray()
                .FirstOrDefault(x => IsInstallerAssetName(x.GetProperty("name").GetString()));
            if (asset.ValueKind == JsonValueKind.Undefined) throw new InvalidDataException("The update package is not available yet.");
            var downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Host != "github.com")
                throw new InvalidDataException("The update link could not be verified.");
            var notes = release.TryGetProperty("body", out var body) ? body.GetString() : null;
            var message = $"Version {latestVersion} is available. Your images and reports will stay where they are. The app will close briefly while it updates.";
            if (!string.IsNullOrWhiteSpace(notes)) message += "\n\nRelease notes:\n" + notes[..Math.Min(notes.Length, 900)];
            if (MessageBox.Show(owner, message + "\n\nInstall this update now?", "Image Metadata Cleaner update", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;

            owner.UseWaitCursor = true;
            owner.Text = "Image Metadata Cleaner — downloading update…";
            var updateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImageMetadataCleanerSetup", "Updates");
            Directory.CreateDirectory(updateFolder);
            var installerPath = Path.Combine(updateFolder, "Image Metadata Cleaner Setup.exe");
            using (var download = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead))
            {
                download.EnsureSuccessStatusCode();
                await using var input = await download.Content.ReadAsStreamAsync();
                await using var output = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await input.CopyToAsync(output);
            }

            if (asset.TryGetProperty("digest", out var digestValue))
            {
                var digest = digestValue.GetString();
                if (!string.IsNullOrWhiteSpace(digest) && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                {
                    await using var input = File.OpenRead(installerPath);
                    var actual = "sha256:" + Convert.ToHexString(await SHA256.HashDataAsync(input)).ToLowerInvariant();
                    if (!string.Equals(actual, digest, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The downloaded update did not pass its file check.");
                }
            }

            var installFolder = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var start = new ProcessStartInfo(installerPath, "/upgrade \"" + installFolder.Replace("\"", "") + "\"") { UseShellExecute = true };
            Process.Start(start);
            owner.Close();
        }
        catch (Exception ex)
        {
            owner.UseWaitCursor = false;
            owner.Text = "Image Metadata Cleaner";
            if (!quiet) MessageBox.Show(owner, "The update could not be downloaded. Please try again later.\n\n" + ex.Message, "Update unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}

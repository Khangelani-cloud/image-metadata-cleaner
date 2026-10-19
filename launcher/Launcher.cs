using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal static class Launcher
{
    [STAThread]
    private static void Main()
    {
        string root = AppDomain.CurrentDomain.BaseDirectory;
        string dotnet = Path.Combine(root, "dotnet.exe");
        string app = Path.Combine(root, "Image Metadata Cleaner.dll");
        if (!File.Exists(dotnet) || !File.Exists(app))
        {
            MessageBox.Show("The app files are missing. Please reinstall Image Metadata Cleaner.", "Image Metadata Cleaner", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        ProcessStartInfo start = new ProcessStartInfo();
        start.FileName = dotnet;
        start.Arguments = "\"" + app + "\"";
        start.WorkingDirectory = root;
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.WindowStyle = ProcessWindowStyle.Hidden;
        start.EnvironmentVariables["DOTNET_ROOT"] = root;
        start.EnvironmentVariables["DOTNET_ROOT_X64"] = root;
        try { Process.Start(start); }
        catch (Exception ex) { MessageBox.Show("Windows could not start the app.\n\n" + ex.Message, "Image Metadata Cleaner", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}

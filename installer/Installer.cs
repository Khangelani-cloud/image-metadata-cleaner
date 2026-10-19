using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class SetupProgram
{
    internal const string Product = "Image Metadata Cleaner";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ImageMetadataCleaner";
    internal static readonly string DefaultLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
    internal static readonly string DefaultProgramFolder = Path.Combine(DefaultLocation, Product);
    private static string SupportFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImageMetadataCleanerSetup"); } }

    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length > 0 && String.Equals(args[0], "/uninstall", StringComparison.OrdinalIgnoreCase)) { Uninstall(); return; }
        if (args.Length > 0 && String.Equals(args[0], "/upgrade", StringComparison.OrdinalIgnoreCase))
        {
            try { Upgrade(args.Length > 1 ? args[1] : DefaultProgramFolder); }
            catch (Exception ex) { MessageBox.Show("The update could not be installed. Please open the app again and try once more.\n\n" + ex.Message, Product, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            return;
        }
        Application.Run(new SetupForm());
    }

    private static void Upgrade(string folder)
    {
        string target = Path.GetFullPath(folder);
        if (!File.Exists(Path.Combine(target, Product + ".exe"))) throw new InvalidOperationException("The existing app folder could not be found.");
        Mutex running = null;
        try { running = Mutex.OpenExisting(@"Local\ImageMetadataCleanerRunning"); }
        catch (WaitHandleCannotBeOpenedException) { }
        if (running != null)
        {
            using (running)
            {
                bool acquired = false;
                try { acquired = running.WaitOne(TimeSpan.FromMinutes(2)); }
                catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) throw new TimeoutException("The app did not close in time. Close it and try the update again.");
                try { Install(target, true); }
                finally { running.ReleaseMutex(); }
            }
        }
        else Install(target, true);
        Process.Start(new ProcessStartInfo(Path.Combine(target, Product + ".exe")) { UseShellExecute = true });
    }

    internal static void Install(string folder, bool desktop)
    {
        Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("appbundle.zip");
        if (resource == null) throw new InvalidOperationException("The app files are missing from this installer.");
        Directory.CreateDirectory(folder);
        using (resource)
        using (ZipArchive zip = new ZipArchive(resource, ZipArchiveMode.Read))
        {
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                // ZIP paths conventionally use '/', while Compress-Archive on Windows
                // can emit '\\'. Normalize both before resolving the destination.
                string relativePath = entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                bool isDirectory = relativePath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal);
                relativePath = relativePath.TrimEnd(Path.DirectorySeparatorChar);
                string destination = Path.GetFullPath(Path.Combine(folder, relativePath));
                if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The setup package contains an invalid path.");
                if (isDirectory) { Directory.CreateDirectory(destination); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                using (Stream input = entry.Open())
                using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None)) input.CopyTo(output);
            }
        }
        string fxrFolder = Path.Combine(folder, "host", "fxr");
        if (!Directory.Exists(fxrFolder) || Directory.GetFiles(fxrFolder, "hostfxr.dll", SearchOption.AllDirectories).Length == 0)
            throw new InvalidDataException("The app runtime files could not be unpacked. Run setup again to retry.");
        string launcher = Path.Combine(folder, Product + ".exe");
        if (!File.Exists(launcher)) throw new FileNotFoundException("The app could not be installed.", launcher);
        string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", Product);
        Directory.CreateDirectory(startMenu);
        CreateShortcut(Path.Combine(startMenu, Product + ".lnk"), launcher);
        if (desktop) CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Product + ".lnk"), launcher);
        RegisterUninstaller(folder, desktop);
    }

    private static void CreateShortcut(string path, string target)
    {
        string menu = path.Replace("'", "''");
        string app = target.Replace("'", "''");
        string script = "$shell=New-Object -ComObject WScript.Shell; $shortcut=$shell.CreateShortcut('" + menu + "'); $shortcut.TargetPath='" + app + "'; $shortcut.WorkingDirectory='" + Path.GetDirectoryName(app).Replace("'", "''") + "'; $shortcut.Description='" + Product + "'; $shortcut.IconLocation='" + app + ",0'; $shortcut.Save()";
        RunPowerShell(script, true);
    }

    private static void RegisterUninstaller(string programFolder, bool hasDesktopShortcut)
    {
        Directory.CreateDirectory(SupportFolder);
        string scriptPath = Path.Combine(SupportFolder, "uninstall.ps1");
        string program = QuotePs(programFolder);
        string menu = QuotePs(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", Product));
        string desktop = hasDesktopShortcut ? QuotePs(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Product + ".lnk")) : "";
        string support = QuotePs(SupportFolder);
        string registry = QuotePs("HKCU:\\" + UninstallKey);
        string cleanup = "Start-Sleep -Seconds 2; Remove-Item -LiteralPath '" + program + "' -Recurse -Force -ErrorAction SilentlyContinue; Remove-Item -LiteralPath '" + menu + "' -Recurse -Force -ErrorAction SilentlyContinue; " + (desktop.Length > 0 ? "Remove-Item -LiteralPath '" + desktop + "' -Force -ErrorAction SilentlyContinue; " : "") + "Remove-Item -LiteralPath '" + support + "' -Recurse -Force -ErrorAction SilentlyContinue; Remove-Item -LiteralPath '" + registry + "' -Recurse -Force -ErrorAction SilentlyContinue";
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(cleanup));
        string script = "Add-Type -AssemblyName System.Windows.Forms\r\n$answer=[System.Windows.Forms.MessageBox]::Show('Remove Image Metadata Cleaner?`n`nYour images and reports will not be removed.','Image Metadata Cleaner',[System.Windows.Forms.MessageBoxButtons]::YesNo,[System.Windows.Forms.MessageBoxIcon]::Question)\r\nif($answer -ne [System.Windows.Forms.DialogResult]::Yes){exit}\r\nStart-Process powershell.exe -ArgumentList @('-NoProfile','-WindowStyle','Hidden','-ExecutionPolicy','Bypass','-EncodedCommand','" + encoded + "') -WindowStyle Hidden\r\n";
        File.WriteAllText(scriptPath, script, new UTF8Encoding(false));
        string displayVersion = "1.1.0";
        try
        {
            Version version = AssemblyName.GetAssemblyName(Path.Combine(programFolder, Product + ".dll")).Version;
            if (version != null) displayVersion = version.ToString(3);
        }
        catch { }
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            key.SetValue("DisplayName", Product);
            key.SetValue("Publisher", Product);
            key.SetValue("DisplayVersion", displayVersion);
        key.SetValue("InstallLocation", programFolder);
            key.SetValue("UninstallString", "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"" + scriptPath + "\"");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
    }

    private static string QuotePs(string value) { return value.Replace("'", "''"); }

    private static void RunPowerShell(string script, bool wait)
    {
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        ProcessStartInfo start = new ProcessStartInfo("powershell.exe", "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand " + encoded);
        start.UseShellExecute = false; start.CreateNoWindow = true; start.WindowStyle = ProcessWindowStyle.Hidden;
        using (Process process = Process.Start(start)) { if (wait) process.WaitForExit(); if (process.ExitCode != 0) throw new InvalidOperationException("Windows could not create an app shortcut."); }
    }

    private static void Uninstall()
    {
        if (MessageBox.Show("Remove Image Metadata Cleaner from this computer?\n\nYour images and reports will not be removed.", Product, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", Product);
        string desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Product + ".lnk");
        string cleanup = "Start-Sleep -Seconds 2; Remove-Item -LiteralPath '" + QuotePs(DefaultProgramFolder) + "' -Recurse -Force -ErrorAction SilentlyContinue; Remove-Item -LiteralPath '" + QuotePs(startMenu) + "' -Recurse -Force -ErrorAction SilentlyContinue; Remove-Item -LiteralPath '" + QuotePs(desktop) + "' -Force -ErrorAction SilentlyContinue; Remove-Item -LiteralPath '" + QuotePs(SupportFolder) + "' -Recurse -Force -ErrorAction SilentlyContinue; Remove-Item -LiteralPath 'HKCU:\\" + UninstallKey + "' -Recurse -Force -ErrorAction SilentlyContinue";
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(cleanup));
        ProcessStartInfo start = new ProcessStartInfo("powershell.exe", "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand " + encoded);
        start.UseShellExecute = false; start.CreateNoWindow = true; start.WindowStyle = ProcessWindowStyle.Hidden; Process.Start(start);
    }
}

internal sealed class SetupForm : Form
{
    private TextBox folder;
    private CheckBox desktop;
    private Button install;
    private Label status;

    internal SetupForm()
    {
        Text = "Install Image Metadata Cleaner"; StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ClientSize = new Size(650, 360); Font = new Font("Segoe UI", 10f);
        TableLayoutPanel root = new TableLayoutPanel(); root.Dock = DockStyle.Fill; root.Padding = new Padding(28); root.ColumnCount = 1; root.RowCount = 6;
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); Controls.Add(root);
        root.Controls.Add(new Label { Text = "Welcome to Image Metadata Cleaner", Font = new Font("Segoe UI", 19f, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(30, 45, 40), Padding = new Padding(0, 0, 0, 6) }, 0, 0);
        root.Controls.Add(new Label { Text = "Install the app so you can open it from the Start Menu. Your images stay on this computer.", AutoSize = true, Padding = new Padding(0, 0, 0, 20) }, 0, 1);
        root.Controls.Add(new Label { Text = "Install location", AutoSize = true }, 0, 2);
        FlowLayoutPanel row = new FlowLayoutPanel(); row.Dock = DockStyle.Top; row.AutoSize = true; row.WrapContents = false;
        folder = new TextBox { Text = SetupProgram.DefaultLocation, Width = 470 }; row.Controls.Add(folder);
        Button browse = new Button { Text = "Browse…", AutoSize = true, Height = 30 };
        browse.Click += delegate { using (FolderBrowserDialog dialog = new FolderBrowserDialog()) { dialog.Description = "Choose where to install the app"; dialog.SelectedPath = folder.Text; dialog.ShowNewFolderButton = true; if (dialog.ShowDialog(this) == DialogResult.OK) folder.Text = dialog.SelectedPath; } };
        row.Controls.Add(browse); root.Controls.Add(row, 0, 3);
        desktop = new CheckBox { Text = "Add a shortcut to my desktop", Checked = true, AutoSize = true }; root.Controls.Add(desktop, 0, 4);
        TableLayoutPanel bottom = new TableLayoutPanel(); bottom.Dock = DockStyle.Fill; bottom.ColumnCount = 2; bottom.RowCount = 1; bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        status = new Label { Text = "Installs for your Windows account. No administrator access is needed.", AutoSize = true, ForeColor = Color.DimGray };
        install = new Button { Text = "Install", Width = 130, Height = 42, BackColor = Color.FromArgb(28, 106, 78), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        bottom.Controls.Add(status, 0, 0); bottom.Controls.Add(install, 1, 0); root.Controls.Add(bottom, 0, 5);
        install.Click += InstallClicked;
    }

    private void InstallClicked(object sender, EventArgs e)
    {
        string location = folder.Text.Trim();
        if (String.IsNullOrWhiteSpace(location)) { MessageBox.Show(this, "Choose an install location first.", SetupProgram.Product, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        string target = Path.Combine(location, SetupProgram.Product);
        install.Enabled = false; status.Text = "Installing…"; Refresh();
        try
        {
            SetupProgram.Install(target, desktop.Checked);
            Process.Start(new ProcessStartInfo(Path.Combine(target, SetupProgram.Product + ".exe")) { UseShellExecute = true });
            MessageBox.Show(this, "Image Metadata Cleaner is installed. You can open it from the Start Menu.", SetupProgram.Product, MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex) { status.Text = "Installation could not be completed."; install.Enabled = true; MessageBox.Show(this, "Windows could not install the app.\n\n" + ex.Message, SetupProgram.Product, MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}

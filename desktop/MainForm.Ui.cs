using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Win32;
using System.Windows.Forms;

namespace ImageMetadataCleaner;

internal sealed partial class MainForm
{
    private readonly ToolTip _toolTips = new();
    private readonly DropZonePanel _dropZone = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    private readonly OptionCard _onlyOption = new("C2PA credentials only", "Keeps XMP and ordinary photo metadata like dates and copyright.");
    private readonly OptionCard _allOption = new("C2PA and all XMP", "May also remove descriptions, dates, copyright and edit history.");
    private readonly RoundedActionButton _imagesButton = new() { Text = "Images", AccessibleName = "Add JPEG images", Icon = IconKind.Add, Style = ActionButtonStyle.Outlined };
    private readonly RoundedActionButton _folderButton = new() { Text = "Folder", AccessibleName = "Add JPEG images from a folder", Icon = IconKind.Folder, Style = ActionButtonStyle.Outlined };
    private readonly RoundedActionButton _removeSelectedButton = new() { Text = "Remove selected", AccessibleName = "Remove selected images", Style = ActionButtonStyle.Quiet, Enabled = false };
    private readonly RoundedActionButton _clearListButton = new() { Text = "Clear list", AccessibleName = "Clear image list", Style = ActionButtonStyle.Quiet, Enabled = false };
    private readonly RoundedActionButton _process = new() { Text = "Inspect and create cleaned copies", AccessibleName = "Inspect and create cleaned copies", Icon = IconKind.Check, Enabled = false, Style = ActionButtonStyle.Primary };
    private readonly RoundedActionButton _openOutput = new() { Text = "Open output folder", AccessibleName = "Open output folder", Icon = IconKind.Folder, Style = ActionButtonStyle.Outlined, Visible = false };
    private readonly RoundedActionButton _themeToggle = new() { Text = UiTheme.IsDark ? "Dark" : "Light", AccessibleName = "Toggle light and dark mode", Style = ActionButtonStyle.Outlined };
    private readonly ThinProgressBar _progress = new() { Dock = DockStyle.Fill };
    private readonly Label _progressCaption = new() { AutoSize = true, ForeColor = UiTheme.Secondary };
    private readonly RoundedTextBox _output = new() { Dock = DockStyle.Fill };
    private string? _completedFolder;
    private BufferedTableLayoutPanel? _fileListLayout;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.UserPreferenceChanged -= HandleUserPreferenceChanged;
            _toolTips.Dispose();
        }
        base.Dispose(disposing);
        if (disposing) UiTheme.Dispose();
    }

    private Control BuildHeader()
    {
        var header = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, BackColor = UiTheme.Window, ColumnCount = 3, RowCount = 1, AutoSize = true, Margin = new Padding(0, 0, 0, UiTheme.Scale(this, UiTheme.CardGap)) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var titleStack = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = UiTheme.Window, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = Padding.Empty };
        titleStack.Controls.Add(new Label
        {
            Text = "Image Metadata Cleaner",
            AutoSize = true,
            Font = UiTheme.GetFont(UiTheme.TitleFont, FontStyle.Bold),
            ForeColor = UiTheme.Text,
            BackColor = UiTheme.Window,
            Margin = Padding.Empty
        });
        titleStack.Controls.Add(new Label
        {
            Text = "Inspect photos and videos. Photo originals are never changed.",
            AutoSize = true,
            Font = UiTheme.GetFont(UiTheme.TaglineFont),
            ForeColor = UiTheme.Secondary,
            BackColor = UiTheme.Window,
            Margin = new Padding(0, UiTheme.Scale(this, UiTheme.Space4), 0, 0)
        });
        var pill = new PillLabel { Text = "Photo + video • clean copies", Anchor = AnchorStyles.Right, Margin = new Padding(UiTheme.Scale(this, UiTheme.Space12), 0, 0, 0) };
        _themeToggle.Size = new Size(UiTheme.Scale(this, 72), UiTheme.Scale(this, 34));
        _themeToggle.Anchor = AnchorStyles.Right;
        _themeToggle.Margin = new Padding(UiTheme.Scale(this, UiTheme.Space8), 0, 0, 0);
        _toolTips.SetToolTip(_themeToggle, "Switch between light and dark mode.");
        header.Controls.Add(titleStack, 0, 0);
        header.Controls.Add(pill, 1, 0);
        header.Controls.Add(_themeToggle, 2, 0);
        return header;
    }

    private Control BuildImagesCard()
    {
        var card = NewCardPanel();
        var layout = new BufferedTableLayoutPanel { Dock = DockStyle.Top, BackColor = UiTheme.Surface, ColumnCount = 1, RowCount = 2, AutoSize = true, Margin = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(SectionHeading("1", "Choose photos or videos"), 0, 0);

        var content = new BufferedTableLayoutPanel { Dock = DockStyle.Top, BackColor = UiTheme.Surface, ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0, UiTheme.Scale(this, UiTheme.Space10), 0, 0) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, 200)));
        var left = new BufferedTableLayoutPanel { Dock = DockStyle.Top, BackColor = UiTheme.Surface, ColumnCount = 1, RowCount = 2, AutoSize = true };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.Scale(this, UiTheme.ImageActionsHeight)));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        _fileListLayout = left;
        _dropZone.Title = "Drag photos or videos here, or use the buttons";
        _dropZone.AccessibleName = _dropZone.Title;
        _dropZone.Description = "JPEG, MP4, MOV";
        _dropZone.FilesDropped += HandleFilesDropped;
        left.Controls.Add(_dropZone, 0, 0);
        StyleFileList();
        left.Controls.Add(_files, 0, 1);
        var buttonColumn = BuildButtonColumn();
        content.Controls.Add(left, 0, 0);
        content.Controls.Add(buttonColumn, 1, 0);
        layout.Controls.Add(content, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildButtonColumn()
    {
        var buttons = new BufferedTableLayoutPanel { Dock = DockStyle.Top, BackColor = UiTheme.Surface, ColumnCount = 1, RowCount = 4, AutoSize = true, Margin = new Padding(UiTheme.Scale(this, UiTheme.Space12), 0, 0, 0) };
        buttons.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.Scale(this, 42)));
        buttons.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.Scale(this, 42)));
        buttons.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.Scale(this, 34)));
        buttons.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.Scale(this, 34)));
        foreach (var button in new[] { _imagesButton, _folderButton })
        {
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 0, 0, UiTheme.Scale(this, UiTheme.Space8));
        }
        _removeSelectedButton.Dock = DockStyle.Fill;
        _clearListButton.Dock = DockStyle.Fill;
        _removeSelectedButton.Margin = Padding.Empty;
        _clearListButton.Margin = Padding.Empty;
        buttons.Controls.Add(_imagesButton, 0, 0);
        buttons.Controls.Add(_folderButton, 0, 1);
        buttons.Controls.Add(_removeSelectedButton, 0, 2);
        buttons.Controls.Add(_clearListButton, 0, 3);

        _imagesButton.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Choose photos or videos",
                Filter = "Photo and video files (*.jpg;*.jpeg;*.mp4;*.m4v;*.mov)|*.jpg;*.jpeg;*.mp4;*.m4v;*.mov|JPEG images (*.jpg;*.jpeg)|*.jpg;*.jpeg|Videos (*.mp4;*.m4v;*.mov)|*.mp4;*.m4v;*.mov",
                Multiselect = true,
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) == DialogResult.OK) AddPaths(dialog.FileNames);
        };
        _folderButton.Click += (_, _) => AddFolder();
        _removeSelectedButton.Click += (_, _) =>
        {
            var selected = _files.SelectedItems.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            _paths.RemoveAll(selected.Contains);
            RefreshFileList();
        };
        _clearListButton.Click += (_, _) => { _paths.Clear(); RefreshFileList(); };
        _files.SelectedIndexChanged += (_, _) => _removeSelectedButton.Enabled = _files.SelectedItems.Count > 0;
        _toolTips.SetToolTip(_imagesButton, "Add one or more photos or videos.");
        _toolTips.SetToolTip(_folderButton, "Add photos or videos from a folder and its subfolders.");
        _toolTips.SetToolTip(_removeSelectedButton, "Remove selected items from this batch.");
        _toolTips.SetToolTip(_clearListButton, "Remove all items from this batch.");
        return buttons;
    }

    private Control BuildOptionsCard()
    {
        var card = NewCardPanel();
        var layout = new BufferedTableLayoutPanel { Dock = DockStyle.Top, BackColor = UiTheme.Surface, ColumnCount = 1, RowCount = 3, AutoSize = true };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(SectionHeading("2", "Choose what to remove"), 0, 0);
        var options = new BufferedTableLayoutPanel { Dock = DockStyle.Top, BackColor = UiTheme.Surface, ColumnCount = 3, RowCount = 1, Height = UiTheme.Scale(this, 96), Margin = new Padding(0, UiTheme.Scale(this, UiTheme.Space10), 0, 0) };
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiTheme.Scale(this, UiTheme.Space16)));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _onlyOption.Dock = DockStyle.Fill;
        _allOption.Dock = DockStyle.Fill;
        _allOption.TabStop = false;
        _onlyOption.SelectedState = true;
        _onlyOption.Selected += (_, _) => SelectOption(_onlyOption);
        _allOption.Selected += (_, _) => SelectOption(_allOption);
        options.Controls.Add(_onlyOption, 0, 0);
        options.Controls.Add(new Panel { BackColor = UiTheme.Surface }, 1, 0);
        options.Controls.Add(_allOption, 2, 0);
        layout.Controls.Add(options, 0, 1);
        var notice = new NoticeControl
        {
            Message = "Video copies clear supported metadata boxes and timestamps while preserving encoded media. C2PA signatures are not verified; removing all JPEG XMP can erase regular photo details too.",
            Dock = DockStyle.Top,
            Margin = new Padding(0, UiTheme.Scale(this, UiTheme.Space14), 0, 0)
        };
        layout.Controls.Add(notice, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildReportCard()
    {
        var card = NewCardPanel();
        card.AutoSize = false;
        card.Dock = DockStyle.Fill;
        card.Margin = Padding.Empty;
        var layout = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, BackColor = UiTheme.Surface, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(SectionHeading("3", "Save copies and view the report"), 0, 0);
        layout.Controls.Add(BuildPathRow(), 0, 1);
        var gridHost = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            CornerRadius = 10,
            BorderColor = UiTheme.Border,
            BorderThickness = 1,
            Padding = new Padding(UiTheme.Scale(this, 1)),
            MinimumSize = new Size(0, UiTheme.Scale(this, 160)),
            Margin = new Padding(0, UiTheme.Scale(this, UiTheme.Space16), 0, 0)
        };
        ConfigureResultsGrid();
        gridHost.Controls.Add(_results);
        layout.Controls.Add(gridHost, 0, 2);
        layout.Controls.Add(BuildActionRow(), 0, 3);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildPathRow()
    {
        var row = new BufferedTableLayoutPanel { Dock = DockStyle.Top, BackColor = UiTheme.Surface, ColumnCount = 3, RowCount = 1, AutoSize = true, Margin = new Padding(0, UiTheme.Scale(this, UiTheme.Space12), 0, 0) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(new Label
        {
            Text = "Save batch under",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = UiTheme.Text,
            BackColor = UiTheme.Surface,
            Margin = new Padding(0, 0, UiTheme.Scale(this, UiTheme.Space12), 0)
        }, 0, 0);
        _output.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Cleaned Media");
        _output.Dock = DockStyle.Fill;
        _output.Margin = new Padding(0, 0, UiTheme.Scale(this, UiTheme.Space8), 0);
        row.Controls.Add(_output, 1, 0);
        var choose = new RoundedActionButton
        {
            Text = "Choose…",
            Style = ActionButtonStyle.Outlined,
            Size = new Size(UiTheme.Scale(this, 108), UiTheme.Scale(this, 38)),
            Font = UiTheme.GetFont(UiTheme.SecondaryButtonFont)
        };
        choose.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Choose where cleaned copies and reports will be saved",
                UseDescriptionForTitle = true,
                SelectedPath = _output.Text,
                ShowNewFolderButton = true
            };
            if (dialog.ShowDialog(this) == DialogResult.OK) _output.Text = dialog.SelectedPath;
        };
        row.Controls.Add(choose, 2, 0);
        return row;
    }

    private Control BuildActionRow()
    {
        var area = new BufferedTableLayoutPanel { Dock = DockStyle.Top, BackColor = UiTheme.Surface, ColumnCount = 1, RowCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, UiTheme.Scale(this, UiTheme.Space16), 0, 0) };
        area.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        area.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.Scale(this, 6)));
        var buttons = new BufferedTableLayoutPanel { Dock = DockStyle.Top, BackColor = UiTheme.Surface, ColumnCount = 3, RowCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _process.Size = new Size(UiTheme.Scale(this, 320), UiTheme.Scale(this, 46));
        _process.Margin = new Padding(0, 0, UiTheme.Scale(this, UiTheme.Space8), 0);
        _process.Font = UiTheme.GetFont(UiTheme.ButtonFont);
        _openOutput.Size = new Size(UiTheme.Scale(this, 176), UiTheme.Scale(this, 46));
        _openOutput.Margin = new Padding(0, 0, UiTheme.Scale(this, UiTheme.Space12), 0);
        _openOutput.Click += (_, _) => OpenOutputFolder();
        _toolTips.SetToolTip(_openOutput, "Open the latest completed batch folder.");
        _progressCaption.Text = "Add photos or videos to begin.";
        _progressCaption.Anchor = AnchorStyles.Left;
        _progressCaption.Margin = new Padding(UiTheme.Scale(this, UiTheme.Space8), 0, 0, 0);
        _progress.Margin = Padding.Empty;
        buttons.Controls.Add(_process, 0, 0);
        buttons.Controls.Add(_openOutput, 1, 0);
        buttons.Controls.Add(_progressCaption, 2, 0);
        area.Controls.Add(buttons, 0, 0);
        area.Controls.Add(_progress, 0, 1);
        return area;
    }

    private Control BuildFooter()
    {
        var footer = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, BackColor = UiTheme.Window, ColumnCount = 2, RowCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, UiTheme.Scale(this, UiTheme.Space16), 0, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(new Label
        {
            Text = "Each batch gets its own dated folder",
            AutoSize = true,
            ForeColor = UiTheme.Secondary,
            BackColor = UiTheme.Window,
            Font = UiTheme.GetFont(UiTheme.FooterFont),
            Anchor = AnchorStyles.Left
        }, 0, 0);
        var updates = new LinkLabel
        {
            Text = "Check for updates",
            AutoSize = true,
            LinkColor = UiTheme.Accent,
            ActiveLinkColor = UiTheme.AccentDark,
            VisitedLinkColor = UiTheme.Accent,
            LinkBehavior = LinkBehavior.AlwaysUnderline,
            BackColor = UiTheme.Window,
            Font = UiTheme.GetFont(UiTheme.FooterFont, FontStyle.Underline),
            Anchor = AnchorStyles.Right
        };
        updates.Click += async (_, _) => await UpdateService.CheckForUpdatesAsync(this, quiet: false);
        updates.AccessibleName = "Check for updates";
        footer.Controls.Add(updates, 1, 0);
        return footer;
    }

    private void ConfigureResultsGrid()
    {
        _results.Dock = DockStyle.Fill;
        _results.AllowUserToResizeRows = false;
        _results.AllowUserToAddRows = false;
        _results.AllowUserToDeleteRows = false;
        _results.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _results.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        _results.RowHeadersVisible = false;
        _results.BackgroundColor = UiTheme.Surface;
        _results.BorderStyle = BorderStyle.None;
        _results.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _results.GridColor = UiTheme.RowDivider;
        _results.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _results.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _results.ColumnHeadersHeight = UiTheme.Scale(this, 36);
        _results.EnableHeadersVisualStyles = false;
        _results.RowTemplate.Height = UiTheme.Scale(this, 44);
        _results.RowTemplate.MinimumHeight = UiTheme.Scale(this, 44);
        _results.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _results.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = UiTheme.Surface,
            ForeColor = UiTheme.Text,
            SelectionBackColor = UiTheme.AccentTint,
            SelectionForeColor = UiTheme.Text,
            Font = UiTheme.GetFont(UiTheme.TableFont),
            Padding = new Padding(UiTheme.Scale(this, 8), 0, UiTheme.Scale(this, 8), 0),
            WrapMode = DataGridViewTriState.False
        };
        _results.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = UiTheme.RowAlternate };
        _results.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = UiTheme.Window,
            ForeColor = UiTheme.HeaderText,
            Font = UiTheme.GetFont(UiTheme.TableHeaderFont),
            Padding = new Padding(UiTheme.Scale(this, 8), 0, UiTheme.Scale(this, 8), 0)
        };
        _results.Columns.Clear();
        _results.Columns.Add(new DataGridViewTextBoxColumn { Name = "file", HeaderText = "File", FillWeight = 26 });
        _results.Columns.Add(new DataGridViewTextBoxColumn { Name = "status", HeaderText = "Result", FillWeight = 17 });
        _results.Columns.Add(new DataGridViewTextBoxColumn { Name = "type", HeaderText = "Type", FillWeight = 10 });
        _results.Columns.Add(new DataGridViewTextBoxColumn { Name = "metadata", HeaderText = "Metadata found", FillWeight = 24 });
        _results.Columns.Add(new DataGridViewTextBoxColumn { Name = "claims", HeaderText = "Readable C2PA claims", FillWeight = 29, SortMode = DataGridViewColumnSortMode.NotSortable });
        _results.CellPainting -= PaintStatusPill;
        _results.CellPainting += PaintStatusPill;
        _results.Paint -= PaintEmptyState;
        _results.Paint += PaintEmptyState;
        _results.CellMouseEnter += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < _results.Rows.Count) _results.Rows[e.RowIndex].DefaultCellStyle.BackColor = UiTheme.RowHover;
        };
        _results.CellMouseLeave += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < _results.Rows.Count)
                _results.Rows[e.RowIndex].DefaultCellStyle.BackColor = e.RowIndex % 2 == 1 ? UiTheme.RowAlternate : UiTheme.Surface;
        };
    }

    private void PaintEmptyState(object? sender, PaintEventArgs e)
    {
        if (_results.Rows.Count > 0) return;
        var bounds = _results.DisplayRectangle;
        bounds.Y += _results.ColumnHeadersVisible ? _results.ColumnHeadersHeight : 0;
        bounds.Height -= _results.ColumnHeadersVisible ? _results.ColumnHeadersHeight : 0;
        TextRenderer.DrawText(e.Graphics, "Results will appear here after you run a batch.", UiTheme.GetFont(UiTheme.MetadataFont), bounds, UiTheme.Secondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }

    private void PaintStatusPill(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 1) return;
        e.Paint(e.CellBounds, DataGridViewPaintParts.Background);
        var text = Convert.ToString(e.FormattedValue) ?? "";
        var (background, foreground) = text switch
        {
            "Cleaned" => (UiTheme.CleanedBackground, UiTheme.CleanedText),
            "Inspected" => (UiTheme.CleanedBackground, UiTheme.CleanedText),
            "Nothing found" => (UiTheme.NothingBackground, UiTheme.WarningText),
            "Not supported yet" => (UiTheme.RowAlternate, UiTheme.Secondary),
            _ => (UiTheme.FailedBackground, UiTheme.FailedText)
        };
        var textSize = TextRenderer.MeasureText(text, UiTheme.GetFont(UiTheme.PillFont), Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        var pillHeight = UiTheme.Scale(this, 24);
        var pillWidth = Math.Min(e.CellBounds.Width - UiTheme.Scale(this, 12), textSize.Width + UiTheme.Scale(this, 18));
        var bounds = new Rectangle(e.CellBounds.X + (e.CellBounds.Width - pillWidth) / 2, e.CellBounds.Y + (e.CellBounds.Height - pillHeight) / 2, pillWidth, pillHeight);
        using var path = RoundedDrawing.Path(bounds, pillHeight / 2f);
        var graphics = e.Graphics;
        if (graphics is null) return;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.FillPath(UiTheme.GetBrush(background), path);
        TextRenderer.DrawText(graphics, text, UiTheme.GetFont(UiTheme.PillFont), bounds, foreground,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        graphics.DrawLine(UiTheme.GetPen(UiTheme.RowDivider), e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
        e.Handled = true;
    }

    private void AddFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose a folder containing photos or videos",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { AddPaths(Directory.EnumerateFiles(dialog.SelectedPath, "*.*", SearchOption.AllDirectories).Where(MediaInspector.IsSupported).ToArray()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            MessageBox.Show(this, $"Windows could not read that folder.\n\n{ex.Message}", "Folder could not be opened", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void HandleFilesDropped(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths) return;
        var validFiles = new List<string>();
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                try { validFiles.AddRange(Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories).Where(MediaInspector.IsSupported)); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
                {
                    MessageBox.Show(this, $"Windows could not read that folder.\n\n{ex.Message}", "Folder could not be opened", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else if (MediaInspector.IsSupported(path)) validFiles.Add(path);
        }
        AddPaths(validFiles);
    }

    private void SelectOption(OptionCard selected)
    {
        _onlyOption.SelectedState = ReferenceEquals(selected, _onlyOption);
        _allOption.SelectedState = ReferenceEquals(selected, _allOption);
    }

    private void StyleFileList()
    {
        _files.BorderStyle = BorderStyle.None;
        _files.BackColor = UiTheme.Surface;
        _files.ForeColor = UiTheme.Text;
        _files.Font = UiTheme.GetFont(UiTheme.SecondaryFont);
        _files.IntegralHeight = false;
        _files.SelectionMode = SelectionMode.MultiExtended;
        _files.AccessibleName = "Selected photos or videos";
        _files.AccessibleDescription = "Select one or more items to remove them from the batch.";
        _files.DrawMode = DrawMode.OwnerDrawFixed;
        _files.ItemHeight = UiTheme.Scale(this, 24);
        _files.DrawItem -= DrawFileItem;
        _files.DrawItem += DrawFileItem;
        _files.MaximumSize = new Size(0, UiTheme.Scale(this, 140));
        _files.Dock = DockStyle.Fill;
        _files.Margin = Padding.Empty;
        _files.Visible = _paths.Count > 0;
    }

    private void UpdateDropZone()
    {
        var count = _paths.Count;
        var label = count == 0 ? "Drag photos or videos here, or use the buttons" : $"{count:N0} items ready";
        _dropZone.Title = label;
        _dropZone.Description = count == 0 ? "JPEG, MP4, MOV" : "Drop more files, or use the buttons";
        _dropZone.AccessibleName = _dropZone.Title;
        _dropZone.Invalidate();
        _files.Visible = count > 0;
        if (_fileListLayout != null)
        {
            _fileListLayout.RowStyles[0].Height = UiTheme.Scale(this, count == 0 ? UiTheme.ImageActionsHeight : 92);
            _fileListLayout.RowStyles[1].Height = UiTheme.Scale(this, count == 0 ? 0 : 76);
            _fileListLayout.PerformLayout();
        }
        if (!_busy && _completedFolder == null)
            _progressCaption.Text = count == 0 ? "Add photos or videos to begin." : "";
        _removeSelectedButton.Enabled = _files.SelectedItems.Count > 0;
        _clearListButton.Enabled = count > 0;
    }

    private void DrawFileItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        e.Graphics.FillRectangle(UiTheme.GetBrush(selected ? UiTheme.AccentTint : UiTheme.Surface), e.Bounds);
        TextRenderer.DrawText(e.Graphics, _files.GetItemText(_files.Items[e.Index]), _files.Font, e.Bounds, UiTheme.Text,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        if (selected && (e.State & DrawItemState.Focus) != 0)
        {
            var bounds = e.Bounds;
            bounds.Inflate(-1, -1);
            e.Graphics.DrawRectangle(UiTheme.GetPen(UiTheme.Accent, UiTheme.Scale(this, 1)), bounds);
        }
    }

    private void OpenOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(_completedFolder) || !Directory.Exists(_completedFolder)) return;
        try
        {
            var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            start.ArgumentList.Add(_completedFolder);
            Process.Start(start);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            MessageBox.Show(this, $"Windows could not open the output folder.\n\n{ex.Message}", "Cannot open folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SetCompletedFolder(string? folder)
    {
        _completedFolder = folder;
        _openOutput.Visible = !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder);
    }

    private BufferedCardPanel NewCardPanel()
    {
        var card = new BufferedCardPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            CornerRadius = UiTheme.CardRadius,
            BorderColor = UiTheme.Border,
            BorderThickness = 1,
            Padding = new Padding(UiTheme.Scale(this, UiTheme.CardPadding)),
            Margin = new Padding(0, 0, 0, UiTheme.Scale(this, UiTheme.CardGap))
        };
        return card;
    }

    private Control SectionHeading(string number, string title)
    {
        var heading = new BufferedFlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty
        };
        heading.Controls.Add(new StepBadge(number) { Margin = new Padding(0, 0, UiTheme.Scale(this, UiTheme.Space8), 0) });
        heading.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = UiTheme.GetFont(UiTheme.SectionHeadingFont),
            ForeColor = UiTheme.Text,
            BackColor = UiTheme.Surface,
            Margin = new Padding(0, UiTheme.Scale(this, UiTheme.Space4), 0, 0)
        });
        return heading;
    }
}

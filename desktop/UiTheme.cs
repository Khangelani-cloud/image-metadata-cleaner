using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ImageMetadataCleaner;

internal static class UiTheme
{
    private static readonly FontFamily ThemeFontFamily = SelectFontFamily();
    private static readonly Dictionary<(float Size, FontStyle Style), Font> Fonts = new();
    private static readonly Dictionary<int, SolidBrush> Brushes = new();
    private static readonly Dictionary<(int Color, float Width, DashStyle Dash), Pen> Pens = new();

    public static readonly Color Window = Color.FromArgb(244, 246, 245);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(227, 231, 229);
    public static readonly Color UnselectedOptionBorder = Color.FromArgb(203, 210, 207);
    public static readonly Color Text = Color.FromArgb(31, 41, 55);
    public static readonly Color Secondary = Color.FromArgb(91, 102, 112);
    public static readonly Color Accent = Color.FromArgb(31, 111, 84);
    public static readonly Color AccentTint = Color.FromArgb(241, 248, 244);
    public static readonly Color AccentBorder = Color.FromArgb(157, 184, 172);
    public static readonly Color AccentDark = Color.FromArgb(22, 79, 60);
    public static readonly Color AccentHover = Color.FromArgb(26, 95, 72);
    public static readonly Color DisabledAccent = Color.FromArgb(86, 122, 106);
    public static readonly Color QuietHover = Color.FromArgb(238, 241, 239);
    public static readonly Color DisabledText = Color.FromArgb(107, 116, 123);
    public static readonly Color InputBackground = Color.FromArgb(248, 250, 249);
    public static readonly Color DropBackground = Color.FromArgb(246, 250, 248);
    public static readonly Color HeaderText = Color.FromArgb(74, 85, 96);
    public static readonly Color RowHover = Color.FromArgb(241, 248, 244);
    public static readonly Color RowAlternate = Color.FromArgb(250, 251, 250);
    public static readonly Color RowDivider = Color.FromArgb(238, 241, 239);
    public static readonly Color ProgressTrack = Color.FromArgb(227, 231, 229);
    public static readonly Color WarningBackground = Color.FromArgb(255, 246, 224);
    public static readonly Color WarningText = Color.FromArgb(107, 68, 0);
    public static readonly Color CleanedBackground = Color.FromArgb(223, 243, 232);
    public static readonly Color CleanedText = Color.FromArgb(20, 87, 58);
    public static readonly Color NothingBackground = Color.FromArgb(255, 241, 214);
    public static readonly Color FailedBackground = Color.FromArgb(253, 230, 230);
    public static readonly Color FailedText = Color.FromArgb(138, 28, 28);
    public static readonly Color PillBackground = Color.FromArgb(227, 240, 234);

    public const int CardRadius = 12;
    public const int ControlRadius = 8;
    public const int CardPadding = 20;
    public const int CardGap = 16;
    public const int ImageActionsHeight = 168;
    public const int Space4 = 4;
    public const int Space6 = 6;
    public const int Space8 = 8;
    public const int Space10 = 10;
    public const int Space12 = 12;
    public const int Space14 = 14;
    public const int Space16 = 16;
    public const int Space24 = 24;
    public const int Space32 = 32;
    public const float TitleFont = 19.5f;
    public const float TaglineFont = 10.5f;
    public const float SectionHeadingFont = 11.25f;
    public const float ButtonFont = 9.75f;
    public const float SecondaryButtonFont = 9.25f;
    public const float TableFont = 9.25f;
    public const float TableHeaderFont = 9f;
    public const float MetadataFont = 9.5f;
    public const float BodyFont = 9.75f;
    public const float SecondaryFont = 9f;
    public const float SmallFont = 8.5f;
    public const float PillFont = 9f;
    public const float BadgeFont = 9f;
    public const float DropTitleFont = 10f;
    public const float NoticeFont = 9.25f;
    public const float OutputFont = 9.5f;
    public const float FooterFont = 8.5f;

    public static Font GetFont(float size, FontStyle style = FontStyle.Regular)
    {
        var key = (size, style);
        if (!Fonts.TryGetValue(key, out var font))
        {
            font = new Font(ThemeFontFamily, size, style, GraphicsUnit.Point);
            Fonts.Add(key, font);
        }
        return font;
    }

    public static SolidBrush GetBrush(Color color)
    {
        if (!Brushes.TryGetValue(color.ToArgb(), out var brush))
        {
            brush = new SolidBrush(color);
            Brushes.Add(color.ToArgb(), brush);
        }
        return brush;
    }

    public static Pen GetPen(Color color, float width = 1, DashStyle dash = DashStyle.Solid)
    {
        var key = (color.ToArgb(), width, dash);
        if (!Pens.TryGetValue(key, out var pen))
        {
            pen = new Pen(color, width) { DashStyle = dash };
            Pens.Add(key, pen);
        }
        return pen;
    }

    public static int Scale(Control control, int logicalPixels) => control.LogicalToDeviceUnits(logicalPixels);
    public static float Scale(Control control, float logicalPixels) => logicalPixels * control.DeviceDpi / 96f;

    public static void Dispose()
    {
        foreach (var font in Fonts.Values) font.Dispose();
        foreach (var brush in Brushes.Values) brush.Dispose();
        foreach (var pen in Pens.Values) pen.Dispose();
        Fonts.Clear();
        Brushes.Clear();
        Pens.Clear();
        ThemeFontFamily.Dispose();
    }

    private static FontFamily SelectFontFamily()
    {
        using var installed = new InstalledFontCollection();
        var preferred = installed.Families.FirstOrDefault(f => string.Equals(f.Name, "Segoe UI Variable Text", StringComparison.OrdinalIgnoreCase))?.Name;
        var fallback = installed.Families.FirstOrDefault(f => string.Equals(f.Name, "Segoe UI", StringComparison.OrdinalIgnoreCase))?.Name;
        return new FontFamily(preferred ?? fallback ?? System.Drawing.FontFamily.GenericSansSerif.Name);
    }
}

internal enum IconKind
{
    Add,
    Folder,
    Upload,
    Check,
    Info,
    Shield,
    Chevron
}

internal static class Icons
{
    public static void Draw(Graphics graphics, IconKind icon, Rectangle bounds, Color color)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var s = Math.Min(bounds.Width, bounds.Height);
        var x = bounds.X + (bounds.Width - s) / 2f;
        var y = bounds.Y + (bounds.Height - s) / 2f;
        var scale = s / 24f;
        using var path = new GraphicsPath();
        switch (icon)
        {
            case IconKind.Add:
                path.AddLine(x + 12 * scale, y + 5 * scale, x + 12 * scale, y + 19 * scale);
                path.StartFigure();
                path.AddLine(x + 5 * scale, y + 12 * scale, x + 19 * scale, y + 12 * scale);
                break;
            case IconKind.Folder:
                path.AddLine(x + 3 * scale, y + 6 * scale, x + 10 * scale, y + 6 * scale);
                path.AddLine(x + 10 * scale, y + 6 * scale, x + 12 * scale, y + 8 * scale);
                path.AddLine(x + 12 * scale, y + 8 * scale, x + 21 * scale, y + 8 * scale);
                path.AddLine(x + 21 * scale, y + 8 * scale, x + 21 * scale, y + 19 * scale);
                path.AddLine(x + 21 * scale, y + 19 * scale, x + 3 * scale, y + 19 * scale);
                path.CloseFigure();
                path.StartFigure();
                path.AddLine(x + 3 * scale, y + 8 * scale, x + 21 * scale, y + 8 * scale);
                break;
            case IconKind.Upload:
                path.AddLine(x + 12 * scale, y + 16 * scale, x + 12 * scale, y + 4 * scale);
                path.StartFigure();
                path.AddLine(x + 7 * scale, y + 9 * scale, x + 12 * scale, y + 4 * scale);
                path.AddLine(x + 12 * scale, y + 4 * scale, x + 17 * scale, y + 9 * scale);
                path.StartFigure();
                path.AddLine(x + 5 * scale, y + 15 * scale, x + 5 * scale, y + 20 * scale);
                path.AddLine(x + 5 * scale, y + 20 * scale, x + 19 * scale, y + 20 * scale);
                path.AddLine(x + 19 * scale, y + 20 * scale, x + 19 * scale, y + 15 * scale);
                break;
            case IconKind.Check:
                path.AddLine(x + 4 * scale, y + 12 * scale, x + 10 * scale, y + 18 * scale);
                path.AddLine(x + 10 * scale, y + 18 * scale, x + 20 * scale, y + 6 * scale);
                break;
            case IconKind.Info:
                path.AddEllipse(x + 3 * scale, y + 3 * scale, 18 * scale, 18 * scale);
                path.StartFigure();
                path.AddLine(x + 12 * scale, y + 11 * scale, x + 12 * scale, y + 17 * scale);
                path.StartFigure();
                path.AddLine(x + 12 * scale, y + 7 * scale, x + 12 * scale, y + 7.2f * scale);
                break;
            case IconKind.Shield:
                path.AddLine(x + 12 * scale, y + 2 * scale, x + 20 * scale, y + 5 * scale);
                path.AddLine(x + 20 * scale, y + 5 * scale, x + 19 * scale, y + 14 * scale);
                path.AddLine(x + 19 * scale, y + 14 * scale, x + 12 * scale, y + 21 * scale);
                path.AddLine(x + 12 * scale, y + 21 * scale, x + 5 * scale, y + 14 * scale);
                path.AddLine(x + 5 * scale, y + 14 * scale, x + 4 * scale, y + 5 * scale);
                path.CloseFigure();
                path.StartFigure();
                path.AddLine(x + 8 * scale, y + 12 * scale, x + 11 * scale, y + 15 * scale);
                path.AddLine(x + 11 * scale, y + 15 * scale, x + 16 * scale, y + 9 * scale);
                break;
            case IconKind.Chevron:
                path.AddLine(x + 7 * scale, y + 9 * scale, x + 12 * scale, y + 14 * scale);
                path.AddLine(x + 12 * scale, y + 14 * scale, x + 17 * scale, y + 9 * scale);
                break;
        }
        graphics.DrawPath(UiTheme.GetPen(color, Math.Max(1, 1.8f * scale), icon == IconKind.Info ? DashStyle.Solid : DashStyle.Solid), path);
    }
}

internal abstract class RoundedControlBase : Panel
{
    private GraphicsPath? _cachedPath;
    private Size _pathSize;
    private int _pathRadius = -1;

    protected RoundedControlBase()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        UpdateStyles();
        BackColor = UiTheme.Surface;
    }

    public int CornerRadius { get; set; } = UiTheme.ControlRadius;

    protected Color EffectiveParentColor
    {
        get
        {
            for (var parent = Parent; parent != null; parent = parent.Parent)
                if (parent.BackColor.A == 255) return parent.BackColor;
            return UiTheme.Window;
        }
    }

    protected GraphicsPath RoundedPath(Rectangle rectangle, int radius)
    {
        if (_cachedPath != null && _pathSize == rectangle.Size && _pathRadius == radius) return _cachedPath;
        _cachedPath?.Dispose();
        _pathSize = rectangle.Size;
        _pathRadius = radius;
        _cachedPath = RoundedDrawing.Path(rectangle, radius);
        return _cachedPath;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(EffectiveParentColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var rect = ClientRectangle;
        if (rect.Width <= 1 || rect.Height <= 1) return;
        var path = RoundedPath(rect, Math.Min(UiTheme.Scale(this, CornerRadius), rect.Height / 2));
        e.Graphics.FillPath(UiTheme.GetBrush(BackColor), path);
    }

    protected override void OnMouseEnter(EventArgs e) { Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnSizeChanged(EventArgs e) { _cachedPath?.Dispose(); _cachedPath = null; base.OnSizeChanged(e); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { _cachedPath?.Dispose(); _cachedPath = null; base.OnDpiChangedAfterParent(e); }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _cachedPath?.Dispose();
        base.Dispose(disposing);
    }
}

internal class RoundedPanel : RoundedControlBase
{
    public Color BorderColor { get; set; } = UiTheme.Border;
    public float BorderThickness { get; set; } = 1;

    public RoundedPanel()
    {
        BackColor = UiTheme.Surface;
        Padding = new Padding(UiTheme.Scale(this, UiTheme.CardPadding));
        TabStop = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var borderWidth = UiTheme.Scale(this, BorderThickness);
        var bounds = ClientRectangle;
        bounds.Inflate(-Math.Max(1, (int)Math.Ceiling(borderWidth / 2f)), -Math.Max(1, (int)Math.Ceiling(borderWidth / 2f)));
        using var path = RoundedDrawing.Path(bounds, UiTheme.Scale(this, CornerRadius));
        e.Graphics.FillPath(UiTheme.GetBrush(BackColor), path);
        e.Graphics.DrawPath(UiTheme.GetPen(BorderColor, borderWidth), path);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var content = Controls.Cast<Control>().FirstOrDefault(control => control.Visible);
        if (content == null) return MinimumSize;
        var availableWidth = proposedSize.Width > 0 ? Math.Max(0, proposedSize.Width - Padding.Horizontal) : Math.Max(0, (Parent?.ClientSize.Width ?? 0) - Padding.Horizontal);
        var contentSize = content.GetPreferredSize(new Size(availableWidth, 0));
        var preferredWidth = contentSize.Width + Padding.Horizontal + content.Margin.Horizontal;
        var preferredHeight = contentSize.Height + Padding.Vertical + content.Margin.Vertical;
        return new Size(Math.Max(preferredWidth, MinimumSize.Width), Math.Max(preferredHeight, MinimumSize.Height));
    }
}

internal enum ActionButtonStyle { Primary, Outlined, Quiet }

internal sealed class RoundedActionButton : RoundedControlBase
{
    public ActionButtonStyle Style { get; set; }
    public IconKind? Icon { get; set; }
    private bool _hover;
    private bool _pressed;

    public RoundedActionButton()
    {
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
        Font = UiTheme.GetFont(UiTheme.ButtonFont, FontStyle.Regular);
        Size = new Size(UiTheme.Scale(this, 180), UiTheme.Scale(this, 42));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var background = Style switch
        {
            ActionButtonStyle.Primary => !Enabled ? UiTheme.DisabledAccent : _pressed ? UiTheme.AccentDark : _hover ? UiTheme.AccentHover : UiTheme.Accent,
            ActionButtonStyle.Outlined => _hover ? UiTheme.AccentTint : UiTheme.Surface,
            _ => _hover ? UiTheme.QuietHover : UiTheme.Surface
        };
        var foreground = !Enabled
            ? Style == ActionButtonStyle.Primary ? Color.White : UiTheme.DisabledText
            : Style == ActionButtonStyle.Primary ? Color.White : Style == ActionButtonStyle.Quiet ? UiTheme.Secondary : UiTheme.Accent;
        var bounds = ClientRectangle;
        var border = Style == ActionButtonStyle.Outlined ? UiTheme.Accent : background;
        var penWidth = Style == ActionButtonStyle.Outlined ? UiTheme.Scale(this, 1) : 0;
        bounds.Inflate(-Math.Max(1, (int)Math.Ceiling(penWidth / 2f)), -Math.Max(1, (int)Math.Ceiling(penWidth / 2f)));
        var radius = UiTheme.Scale(this, CornerRadius);
        using var path = RoundedDrawing.Path(bounds, radius);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.FillPath(UiTheme.GetBrush(background), path);
        if (penWidth > 0) e.Graphics.DrawPath(UiTheme.GetPen(border, penWidth), path);
        var iconSize = UiTheme.Scale(this, 18);
        var textSize = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        var gap = Icon.HasValue ? UiTheme.Scale(this, 8) : 0;
        var total = Math.Min(bounds.Width - UiTheme.Scale(this, 16), textSize.Width + iconSize + gap);
        var startX = bounds.Left + (bounds.Width - total) / 2;
        if (Icon.HasValue)
        {
            var iconBounds = new Rectangle(startX, bounds.Top + (bounds.Height - iconSize) / 2, iconSize, iconSize);
            Icons.Draw(e.Graphics, Icon.Value, iconBounds, foreground);
            startX += iconSize + gap;
        }
        var textBounds = new Rectangle(startX, bounds.Top, Math.Max(1, bounds.Right - startX), bounds.Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, foreground,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        if (Focused && ShowFocusCues)
        {
            bounds.Inflate(-UiTheme.Scale(this, 3), -UiTheme.Scale(this, 3));
                var focusPath = RoundedDrawing.Path(bounds, Math.Max(2, radius - UiTheme.Scale(this, 2)));
            using (focusPath) e.Graphics.DrawPath(UiTheme.GetPen(UiTheme.Accent, UiTheme.Scale(this, 2)), focusPath);
        }
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) _pressed = true; base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; base.OnMouseUp(e); }
    protected override void OnClick(EventArgs e) { if (Enabled) base.OnClick(e); }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Enabled && e.KeyCode is Keys.Space or Keys.Enter)
        {
            _pressed = true;
            Invalidate();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            var invoke = Enabled && _pressed;
            _pressed = false;
            Invalidate();
            if (invoke) OnClick(EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyUp(e);
    }
}

internal sealed class DropZonePanel : RoundedControlBase
{
    public bool IsDragActive { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public event EventHandler<DragEventArgs>? FilesDropped;
    private GraphicsPath? _zonePath;
    private Size _zoneSize;

    public DropZonePanel()
    {
        AllowDrop = true;
        BackColor = UiTheme.InputBackground;
        AccessibleRole = AccessibleRole.DropList;
        TabStop = false;
        DragEnter += (_, e) => UpdateDragEffect(e);
        DragOver += (_, e) => UpdateDragEffect(e);
        DragLeave += (_, _) => { IsDragActive = false; Invalidate(); };
        DragDrop += (_, e) => { IsDragActive = false; Invalidate(); FilesDropped?.Invoke(this, e); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var bounds = ClientRectangle;
        e.Graphics.Clear(EffectiveParentColor);
        bounds.Inflate(-1, -1);
        if (_zonePath == null || _zoneSize != ClientSize)
        {
            _zonePath?.Dispose();
            _zonePath = RoundedDrawing.Path(bounds, UiTheme.Scale(this, 10));
            _zoneSize = ClientSize;
        }
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.FillPath(UiTheme.GetBrush(IsDragActive ? UiTheme.PillBackground : UiTheme.DropBackground), _zonePath);
        using var dashedPen = new Pen(IsDragActive ? UiTheme.Accent : UiTheme.AccentBorder, UiTheme.Scale(this, 1.5f)) { DashStyle = DashStyle.Dash };
        var inset = dashedPen.Width / 2;
        var borderBounds = new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        borderBounds.Inflate(-inset, -inset);
        using var borderPath = RoundedDrawing.Path(borderBounds, UiTheme.Scale(this, 10));
        e.Graphics.DrawPath(dashedPen, borderPath);

        var iconSize = UiTheme.Scale(this, 30);
        var textWidth = UiTheme.Scale(this, 360);
        var groupWidth = iconSize + UiTheme.Scale(this, 14) + textWidth;
        var left = (ClientSize.Width - groupWidth) / 2;
        var top = (ClientSize.Height - UiTheme.Scale(this, 48)) / 2;
        Icons.Draw(e.Graphics, IconKind.Upload, new Rectangle(left, top + UiTheme.Scale(this, 8), iconSize, iconSize), UiTheme.Accent);
        var textRect = new Rectangle(left + iconSize + UiTheme.Scale(this, 14), top, textWidth, UiTheme.Scale(this, 48));
        TextRenderer.DrawText(e.Graphics, Title, UiTheme.GetFont(UiTheme.DropTitleFont, FontStyle.Regular), textRect, UiTheme.Text,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        var detail = new Rectangle(textRect.X, textRect.Y + UiTheme.Scale(this, 25), textRect.Width, UiTheme.Scale(this, 20));
        TextRenderer.DrawText(e.Graphics, Description, UiTheme.GetFont(UiTheme.SecondaryFont), detail, UiTheme.Secondary,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }

    private void UpdateDragEffect(DragEventArgs e)
    {
        var jpegPresent = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            && e.Data.GetData(DataFormats.FileDrop) is string[] paths
            && paths.Any(path => Directory.Exists(path)
                || string.Equals(Path.GetExtension(path), ".jpg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetExtension(path), ".jpeg", StringComparison.OrdinalIgnoreCase));
        e.Effect = jpegPresent ? DragDropEffects.Copy : DragDropEffects.None;
        IsDragActive = jpegPresent;
        Invalidate();
    }

    protected override void OnSizeChanged(EventArgs e) { _zonePath?.Dispose(); _zonePath = null; base.OnSizeChanged(e); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { _zonePath?.Dispose(); _zonePath = null; base.OnDpiChangedAfterParent(e); }
    protected override void Dispose(bool disposing) { if (disposing) _zonePath?.Dispose(); base.Dispose(disposing); }
}

internal sealed class OptionCard : RoundedControlBase
{
    private bool _selected;
    private readonly string _title;
    private readonly string _description;
    public event EventHandler? Selected;

    public bool SelectedState
    {
        get => _selected;
        set { _selected = value; Invalidate(); }
    }

    public OptionCard(string title, string description)
    {
        _title = title;
        _description = description;
        BackColor = UiTheme.Surface;
        TabStop = true;
        AccessibleRole = AccessibleRole.RadioButton;
        AccessibleName = title;
        AccessibleDescription = description;
        Size = new Size(UiTheme.Scale(this, 200), UiTheme.Scale(this, 96));
        Click += (_, _) => SelectCard();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width > 0 ? proposedSize.Width : UiTheme.Scale(this, 300);
        var innerWidth = Math.Max(UiTheme.Scale(this, 80), width - UiTheme.Scale(this, 32));
        var desc = TextRenderer.MeasureText(_description, UiTheme.GetFont(UiTheme.SecondaryFont), new Size(innerWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        return new Size(width, UiTheme.Scale(this, 16 + 20 + 8) + desc.Height + UiTheme.Scale(this, 16));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var bounds = ClientRectangle;
        var borderWidth = UiTheme.Scale(this, _selected ? 2 : 1);
        bounds.Inflate(-Math.Max(1, (int)Math.Ceiling(borderWidth / 2f)), -Math.Max(1, (int)Math.Ceiling(borderWidth / 2f)));
        using var path = RoundedDrawing.Path(bounds, UiTheme.Scale(this, 10));
        e.Graphics.FillPath(UiTheme.GetBrush(_selected ? UiTheme.AccentTint : UiTheme.Surface), path);
        e.Graphics.DrawPath(UiTheme.GetPen(_selected ? UiTheme.Accent : UiTheme.UnselectedOptionBorder, borderWidth), path);
        var padding = UiTheme.Scale(this, 16);
        var title = new Rectangle(padding, padding, Math.Max(1, ClientSize.Width - padding * 2), UiTheme.Scale(this, 22));
        TextRenderer.DrawText(e.Graphics, _title, UiTheme.GetFont(UiTheme.ButtonFont, FontStyle.Regular), title, UiTheme.Text,
            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        var description = new Rectangle(padding, title.Bottom + UiTheme.Scale(this, 4), Math.Max(1, ClientSize.Width - padding * 2), Math.Max(1, ClientSize.Height - title.Bottom - padding));
        TextRenderer.DrawText(e.Graphics, _description, UiTheme.GetFont(UiTheme.SecondaryFont), description, UiTheme.Secondary,
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        if (Focused && ShowFocusCues)
        {
            bounds.Inflate(-UiTheme.Scale(this, 4), -UiTheme.Scale(this, 4));
            using var focusPath = RoundedDrawing.Path(bounds, UiTheme.Scale(this, 6));
            e.Graphics.DrawPath(UiTheme.GetPen(UiTheme.Accent, UiTheme.Scale(this, 2)), focusPath);
        }
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) { SelectCard(); e.Handled = true; }
        else if (e.KeyCode is Keys.Left or Keys.Up or Keys.Right or Keys.Down)
        {
            var sibling = Parent?.Controls.OfType<OptionCard>().FirstOrDefault(x => !ReferenceEquals(x, this));
            if (sibling != null) { sibling.SelectCard(); sibling.Focus(); }
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new OptionCardAccessibleObject(this);

    private void SelectCard()
    {
        Selected?.Invoke(this, EventArgs.Empty);
        SelectedState = true;
        Focus();
    }

    private sealed class OptionCardAccessibleObject : ControlAccessibleObject
    {
        private readonly OptionCard _owner;
        public OptionCardAccessibleObject(OptionCard owner) : base(owner) => _owner = owner;
        public override AccessibleRole Role => AccessibleRole.RadioButton;
        public override AccessibleStates State => base.State | (_owner.SelectedState ? AccessibleStates.Checked : AccessibleStates.None);
        public override string? DefaultAction => "Select";
        public override void DoDefaultAction() => _owner.SelectCard();
    }
}

internal sealed class NoticeControl : RoundedControlBase
{
    public string Message { get; set; } = "";
    public NoticeControl()
    {
        BackColor = UiTheme.WarningBackground;
        CornerRadius = 8;
        AutoSize = true;
        MinimumSize = new Size(0, UiTheme.Scale(this, 42));
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var iconWidth = UiTheme.Scale(this, 24);
        var width = proposedSize.Width > 0 ? proposedSize.Width : Parent?.ClientSize.Width ?? UiTheme.Scale(this, 320);
        var textWidth = Math.Max(UiTheme.Scale(this, 120), width - iconWidth - UiTheme.Scale(this, 28));
        var text = TextRenderer.MeasureText(Message, UiTheme.GetFont(UiTheme.NoticeFont), new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        return new Size(width, Math.Max(UiTheme.Scale(this, 42), text.Height + UiTheme.Scale(this, 20)));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var iconSize = UiTheme.Scale(this, 18);
        var x = UiTheme.Scale(this, 14);
        var y = UiTheme.Scale(this, 12);
        Icons.Draw(e.Graphics, IconKind.Info, new Rectangle(x, y, iconSize, iconSize), UiTheme.WarningText);
        var text = new Rectangle(x + iconSize + UiTheme.Scale(this, 10), UiTheme.Scale(this, 10), Math.Max(1, ClientSize.Width - x - iconSize - UiTheme.Scale(this, 24)), ClientSize.Height - UiTheme.Scale(this, 20));
        TextRenderer.DrawText(e.Graphics, Message, UiTheme.GetFont(UiTheme.NoticeFont), text, UiTheme.WarningText, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
    }
}

internal sealed class ThinProgressBar : RoundedControlBase
{
    private int _value;
    public int Value
    {
        get => _value;
        set { _value = Math.Clamp(value, 0, 100); Invalidate(); }
    }

    public ThinProgressBar()
    {
        BackColor = UiTheme.ProgressTrack;
        AccessibleRole = AccessibleRole.ProgressBar;
        AccessibleName = "Batch progress";
        Height = UiTheme.Scale(this, 6);
        TabStop = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var width = ClientSize.Width * _value / 100;
        if (width <= 0) return;
        var rect = new Rectangle(0, 0, width, ClientSize.Height);
        using var path = RoundedDrawing.Path(rect, ClientSize.Height / 2);
        e.Graphics.FillPath(UiTheme.GetBrush(UiTheme.Accent), path);
    }
}

internal sealed class PillLabel : RoundedControlBase
{
    public PillLabel()
    {
        BackColor = UiTheme.PillBackground;
        ForeColor = UiTheme.AccentDark;
        Font = UiTheme.GetFont(UiTheme.SmallFont);
        CornerRadius = 16;
        AutoSize = true;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var measured = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        var height = UiTheme.Scale(this, 30);
        return new Size(measured.Width + UiTheme.Scale(this, 42), height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var icon = new Rectangle(UiTheme.Scale(this, 10), 0, UiTheme.Scale(this, 18), ClientSize.Height);
        Icons.Draw(e.Graphics, IconKind.Shield, icon, ForeColor);
        var text = new Rectangle(icon.Right + UiTheme.Scale(this, 4), 0, ClientSize.Width - icon.Right - UiTheme.Scale(this, 10), ClientSize.Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, text, ForeColor, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
}

internal sealed class RoundedTextBox : RoundedControlBase
{
    private GraphicsPath? _path;
    private Size _cachedSize;
    private bool _readOnly = true;

    public bool ReadOnly
    {
        get => _readOnly;
        set { _readOnly = value; TabStop = !value; Invalidate(); }
    }

    public RoundedTextBox()
    {
        BackColor = UiTheme.InputBackground;
        ForeColor = UiTheme.Text;
        Font = UiTheme.GetFont(UiTheme.OutputFont);
        CornerRadius = UiTheme.ControlRadius;
        Height = UiTheme.Scale(this, 38);
        AccessibleRole = AccessibleRole.Text;
        AccessibleName = "Save batch under";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(EffectiveParentColor);
        var bounds = ClientRectangle;
        var borderWidth = UiTheme.Scale(this, 1);
        bounds.Inflate(-Math.Max(1, (int)Math.Ceiling(borderWidth / 2f)), -Math.Max(1, (int)Math.Ceiling(borderWidth / 2f)));
        if (_path == null || _cachedSize != ClientSize)
        {
            _path?.Dispose();
            _path = RoundedDrawing.Path(bounds, UiTheme.Scale(this, CornerRadius));
            _cachedSize = ClientSize;
        }
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.FillPath(UiTheme.GetBrush(BackColor), _path);
        e.Graphics.DrawPath(UiTheme.GetPen(UiTheme.UnselectedOptionBorder, borderWidth), _path);
        var textBounds = ClientRectangle;
        textBounds.Inflate(-UiTheme.Scale(this, UiTheme.Space12), 0);
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, ForeColor,
            TextFormatFlags.PathEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        if (Focused && ShowFocusCues)
        {
            bounds.Inflate(-UiTheme.Scale(this, 3), -UiTheme.Scale(this, 3));
            using var focus = RoundedDrawing.Path(bounds, UiTheme.Scale(this, 5));
            e.Graphics.DrawPath(UiTheme.GetPen(UiTheme.Accent, UiTheme.Scale(this, 2)), focus);
        }
    }

    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnSizeChanged(EventArgs e) { _path?.Dispose(); _path = null; base.OnSizeChanged(e); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { _path?.Dispose(); _path = null; base.OnDpiChangedAfterParent(e); }
    protected override void Dispose(bool disposing) { if (disposing) _path?.Dispose(); base.Dispose(disposing); }
}

internal sealed class BufferedTableLayoutPanel : TableLayoutPanel
{
    public BufferedTableLayoutPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
}

internal sealed class BufferedFlowLayoutPanel : FlowLayoutPanel
{
    public BufferedFlowLayoutPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
}

internal sealed class BufferedCardPanel : RoundedPanel
{
    public BufferedCardPanel()
    {
        AutoSize = true;
    }
}

internal sealed class BufferedDataGridView : DataGridView
{
    public BufferedDataGridView()
    {
        DoubleBuffered = true;
    }
}

internal sealed class StepBadge : RoundedControlBase
{
    private readonly string _number;

    public StepBadge(string number)
    {
        _number = number;
        Size = new Size(UiTheme.Scale(this, 24), UiTheme.Scale(this, 24));
        BackColor = UiTheme.Accent;
        ForeColor = Color.White;
        Font = UiTheme.GetFont(UiTheme.BadgeFont, FontStyle.Bold);
        TabStop = false;
        AccessibleRole = AccessibleRole.StaticText;
        AccessibleName = $"Step {number}";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.Clear(EffectiveParentColor);
        using var path = RoundedDrawing.Path(ClientRectangle, ClientSize.Height / 2f);
        e.Graphics.FillPath(UiTheme.GetBrush(UiTheme.Accent), path);
        TextRenderer.DrawText(e.Graphics, _number, Font, ClientRectangle, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
}

internal sealed class RoundedDrawing
{
    public static GraphicsPath Path(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
        var arc = new RectangleF(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.X;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

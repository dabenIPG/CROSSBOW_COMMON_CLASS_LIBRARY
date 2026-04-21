using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Windows.Forms.Design;

namespace CROSSBOW; // is this needed?

/// <summary>
/// A StatusStrip-compatible progress bar that displays battery state of charge
/// with color-coded fill (green → yellow → red) and centered percentage text.
/// </summary>
[ToolboxItem(true)]
[ToolStripItemDesignerAvailability(ToolStripItemDesignerAvailability.StatusStrip |
                                   ToolStripItemDesignerAvailability.ToolStrip)]
public class BatteryStatusBar : ToolStripProgressBar
{
    // Stored reference — prevents the renderer from being garbage collected
    private BatteryProgressRenderer? _renderer;

    [Category("Battery")]
    [Description("Custom display text. If set, overrides the default 'Label: ZZZ%' format.")]
    [DefaultValue(null)]
    public string? DisplayText { get; set; } = null;

    [Category("Battery")]
    [Description("Percentage threshold below which the bar turns yellow. Default: 40")]
    [DefaultValue(40)]                          // FIX 2a: lets designer detect non-default values
    public int LowThreshold { get; set; } = 40;

    [Category("Battery")]
    [Description("Percentage threshold below which the bar turns red. Default: 15")]
    [DefaultValue(15)]                          // FIX 2b
    public int CriticalThreshold { get; set; } = 15;

    [Category("Battery")]
    [Description("Label shown before the percentage value.")]
    [DefaultValue("Battery")]                   // FIX 2c
    public string Label { get; set; } = "Battery";

    [Category("Battery")]
    [Description("When true, the bar pulses red in a critical state.")]
    [DefaultValue(true)]                        // FIX 2d
    public bool PulseWhenCritical { get; set; } = true;

    // FIX 1: Parameterless constructor — used at runtime / first drop onto designer
    public BatteryStatusBar() : base()
    {
        Initialize();
    }

    // FIX 1: Named constructor — the WinForms designer REQUIRES this overload to
    // reconstruct ToolStripItem subclasses when reloading the form from Designer.cs.
    // Without it the item is silently dropped every time the IDE restarts.
    public BatteryStatusBar(string name) : base(name)
    {
        Initialize();
    }

    private void Initialize()
    {
        Minimum = 0;
        Maximum = 100;
        Value   = 0;
        Size    = new Size(180, 18);
	if (!DesignMode)
            Control.HandleCreated += OnHandleCreated;
    }

    /// <summary>
    /// Update the charge level (0–100) and trigger a repaint.
    /// </summary>
    public void SetCharge(int percent)
    {
        Value = Math.Clamp(percent, Minimum, Maximum);
        Control.Invalidate();
    }

    private void OnHandleCreated(object? sender, EventArgs e)
    {
        _renderer = new BatteryProgressRenderer(this, (ProgressBar)Control);
    }
}


/// <summary>
/// Hooks into the ProgressBar's native window to owner-draw the battery fill and text.
/// Kept internal and sealed — it is an implementation detail of BatteryStatusBar only.
/// </summary>
internal sealed class BatteryProgressRenderer : NativeWindow, IDisposable
{
    private const int WM_PAINT = 0x000F;
    private const int WM_ERASEBKGND = 0x0014;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    // Fill colors
    private static readonly Color GreenLight  = Color.FromArgb(74,  222, 128);
    private static readonly Color GreenDark   = Color.FromArgb(22,  163, 74);
    private static readonly Color YellowLight = Color.FromArgb(253, 224, 71);
    private static readonly Color YellowDark  = Color.FromArgb(202, 138, 4);
    private static readonly Color RedLight    = Color.FromArgb(252, 165, 165);
    private static readonly Color RedDark     = Color.FromArgb(220, 38,  38);

    private static readonly Color BackColor   = Color.FromArgb(30,  30,  30);
    private static readonly Color BorderColor = Color.FromArgb(90,  90,  90);
    private static readonly Color TextColor   = Color.White;

    private readonly BatteryStatusBar _owner;
    private readonly ProgressBar      _bar;

    // Pulsing support for critical state
    private readonly System.Windows.Forms.Timer _pulseTimer = new();
    private bool _pulseDim = false;

    public BatteryProgressRenderer(BatteryStatusBar owner, ProgressBar bar)
    {
        _owner = owner;
        _bar   = bar;

        AssignHandle(_bar.Handle);
        // Remove animated fill — set PBS_SMOOTH style
        const int GWL_STYLE = -16;
        const int PBS_SMOOTH = 0x01;
        int style = GetWindowLong(_bar.Handle, GWL_STYLE);
        SetWindowLong(_bar.Handle, GWL_STYLE, style | PBS_SMOOTH);

        _pulseTimer.Interval = 600;
        _pulseTimer.Tick    += (_, _) => { _pulseDim = !_pulseDim; _bar.Invalidate(); };
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_ERASEBKGND)
        {
            // Suppress background erase — eliminates flicker
            m.Result = (IntPtr)1;
            return;
        }

        base.WndProc(ref m);

        if (m.Msg == WM_PAINT)
            Render();
    }

    private void Render()
    {
        using var g = _bar.CreateGraphics();
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = _bar.ClientRectangle;
        int value  = _bar.Value;
        int range  = _bar.Maximum - _bar.Minimum;
        int pct    = range == 0 ? 0 : (int)Math.Round((value - _bar.Minimum) / (double)range * 100);

        bool isCritical = pct <= _owner.CriticalThreshold;
        bool isLow      = pct <= _owner.LowThreshold;

        // Manage pulse timer
        if (_owner.PulseWhenCritical)
        {
            if (isCritical && !_pulseTimer.Enabled) _pulseTimer.Start();
            if (!isCritical &&  _pulseTimer.Enabled) { _pulseTimer.Stop(); _pulseDim = false; }
        }

        // ── Background ───────────────────────────────────────────────
        using (var bgBrush = new SolidBrush(BackColor))
            g.FillRectangle(bgBrush, bounds);

        // ── Filled portion ────────────────────────────────────────────
        int fillW = (int)Math.Round((value - _bar.Minimum) / (double)range * bounds.Width);
        if (fillW > 1)
        {
            var fillRect = new Rectangle(bounds.X, bounds.Y, fillW, bounds.Height);

            (Color light, Color dark) = isCritical ? (RedLight,    RedDark)
                                      : isLow      ? (YellowLight, YellowDark)
                                                   : (GreenLight,  GreenDark);

            // Dim fill on pulse alternate tick
            if (_pulseDim)
            {
                light = ControlPaint.Dark(light, 0.4f);
                dark  = ControlPaint.Dark(dark,  0.4f);
            }

            using var fillBrush = new LinearGradientBrush(
                fillRect, light, dark, LinearGradientMode.Vertical);
            g.FillRectangle(fillBrush, fillRect);
        }

        // ── Border ────────────────────────────────────────────────────
        using (var borderPen = new Pen(BorderColor))
            g.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

        // ── Text ──────────────────────────────────────────────────────
        //string text      = $"{_owner.Label}: {pct}%";
        string text = _owner.DisplayText ?? $"{_owner.Label}: {pct}%";
        var    textFlags = TextFormatFlags.HorizontalCenter
                         | TextFormatFlags.VerticalCenter
                         | TextFormatFlags.SingleLine;

        // Subtle shadow for legibility over the fill
        TextRenderer.DrawText(g, text, _bar.Font,
            new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width, bounds.Height),
            Color.FromArgb(80, 0, 0, 0), textFlags);

        TextRenderer.DrawText(g, text, _bar.Font, bounds, TextColor, textFlags);
    }

    public void Dispose()
    {
        _pulseTimer.Dispose();
        ReleaseHandle();
    }
}

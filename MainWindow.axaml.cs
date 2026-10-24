using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Shapes = Avalonia.Controls.Shapes;

namespace VirtualMan;

public partial class MainWindow : Window
{
    // ------------------------------------------------------------------ configuration
    private static readonly double[] SpeedOptions = { 0.5, 1, 2, 5, 10, 30 };

    private const double RightBoxLeft = 480, RightBoxW = 150, RightBoxH = 40;
    private const double LeftBoxLeft = 10, LeftBoxW = 130, LeftBoxH = 26;
    private const double Elbow = 20;

    /// <summary>Where each callout sits on the 640x610 body canvas and which point of the body it targets.</summary>
    private sealed record CalloutSpec(string Organ, bool Right, double Y, double TargetX, double TargetY, string BoxColor, string LineColor);

    private static readonly CalloutSpec[] CalloutSpecs =
    {
        // right column: large boxes
        new("Pituitary",      true,  30, 330,  64, "#553C9A", "#D6BCFA"),
        new("Heart",          true,  78, 331, 165, "#E53E3E", "#FC8181"),
        new("Lungs",          true, 174, 352, 174, "#3182CE", "#63B3ED"),
        new("Spleen",         true, 225, 351, 205, "#97266D", "#FBB6CE"),
        new("Liver",          true, 270, 313, 201, "#38A169", "#68D391"),
        new("Adrenal Glands", true, 322, 346, 231, "#C05621", "#F6AD55"),
        new("Kidneys",        true, 377, 354, 247, "#805AD5", "#B794F4"),
        new("Large Intestine",true, 430, 351, 269, "#276749", "#9AE6B4"),
        new("Bone Marrow",    true, 482, 301, 312, "#9C4221", "#FC8181"),
        // left column: compact boxes
        new("Cerebrum",       false,  30, 315,  48, "#6B46C1", "#B794F4"),
        new("Eyes",           false,  60, 312,  74, "#2B6CB0", "#63B3ED"),
        new("Ear",            false,  90, 299,  74, "#4A5568", "#A0AEC0"),
        new("Brainstem",      false, 120, 323,  92, "#2F855A", "#48BB78"),
        new("Thyroid",        false, 150, 323, 108, "#B83280", "#F687B3"),
        new("Stomach",        false, 205, 338, 216, "#B7791F", "#ECC94B"),
        new("Pancreas",       false, 240, 326, 230, "#DD6B20", "#F6AD55"),
        new("Small Intestine",false, 275, 319, 252, "#C05621", "#FBD38D"),
        new("Bladder",        false, 310, 323, 297, "#319795", "#81E6D9"),
        new("Skeletal Muscles",false,380, 258, 196, "#9B2C2C", "#FEB2B2"),
        new("Legs",           false, 416, 293, 410, "#0987A0", "#4FD1C5"),
        new("Skin",           false, 452, 231, 299, "#975A16", "#F6E05E"),
    };

    private sealed class CalloutVisual
    {
        public Organ Organ = null!;
        public CalloutSpec Spec = null!;
        public Button Button = null!;
        public List<Control> Decor = new();
    }

    // ------------------------------------------------------------------ state
    private readonly Simulation _sim = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly EcgGenerator _ecg = new(300);
    private readonly ChartControl _ecgChart = new() { Min = -0.4, Max = 1.2, GridLines = 4, LineThickness = 1.6 };

    private readonly List<SimEvent> _log = new();
    private readonly List<CalloutVisual> _callouts = new();
    private readonly Dictionary<string, bool> _layerEnabled = new();
    private readonly Dictionary<Organ, TextBlock> _organLines = new();
    private readonly Dictionary<string, TextBlock> _systemStatus = new();
    private readonly List<MenuItem> _speedItems = new();
    private List<IGrouping<string, Organ>> _systemGroups = new();

    private double _lastTick;
    private double _speed = 1;
    private bool _running = true;
    private bool _showLines = true;
    private bool _logDirty = true;
    private bool _updatingSlider;
    private bool _sliderDragging;
    private bool _loadingParam;
    private AnalyticsWindow? _analytics;
    private HospitalWindow? _hospital;
    private readonly CaseManager _cases;
    private readonly Dictionary<string, MenuItem> _procMenuItems = new();

    // ------------------------------------------------------------------ construction
    public MainWindow()
    {
        InitializeComponent();
        _cases = new CaseManager(_sim);

        BuildCards();
        BuildCallouts();
        BuildLayers();
        BuildParameterUi();
        BuildSpeedUi();
        BuildFaultMenus();
        BuildHospitalMenus();

        EcgHost.Child = _ecgChart;
        _ecgChart.Series.Add(new ChartSeries("Lead II", Ui.Brush("#48BB78")));

        foreach (var mi in new[] { MenuLines, MenuCards, MenuLog, MenuEcg }) SetCheck(mi, true);

        _sim.EventRaised += OnSimEvent;
        _cases.CaseFinished += r => new InfoWindow("Case Result — " + r.Title, r.ToText(), 760, 620).Show(this);
        LogFilter.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) _logDirty = true; };

        TimelineSlider.PropertyChanged += OnSliderPropertyChanged;
        TimelineSlider.AddHandler(PointerPressedEvent, (_, _) => _sliderDragging = true, RoutingStrategies.Tunnel, true);
        TimelineSlider.AddHandler(PointerReleasedEvent, (_, _) => _sliderDragging = false, RoutingStrategies.Tunnel, true);
        TimelineSlider.PointerCaptureLost += (_, _) => _sliderDragging = false;

        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        Closed += (_, _) => { _timer.Stop(); _analytics?.Close(); _hospital?.Close(); };

        _sim.Raise(Severity.Success, "[SYSTEM]: Simulation initialised. All systems nominal.");

        _lastTick = _clock.Elapsed.TotalSeconds;
        _timer.Tick += OnTick;
        _timer.Start();
        RefreshUi();
    }

    // ------------------------------------------------------------------ builders (everything comes from the model)
    private void BuildCards()
    {
        _systemGroups = _sim.Organs.GroupBy(o => o.System).ToList();
        foreach (var group in _systemGroups)
        {
            var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var name = new TextBlock { Text = group.Key.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeight.Bold };
            var status = new TextBlock { FontSize = 10, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(status, 1);
            header.Children.Add(name);
            header.Children.Add(status);
            _systemStatus[group.Key] = status;

            var stack = new StackPanel { Spacing = 3 };
            stack.Children.Add(header);
            foreach (var organ in group)
            {
                // every organ line is clickable: fail / degrade / restore, and the tooltip shows its dependencies
                var line = new TextBlock
                {
                    FontSize = 10.5,
                    Background = Brushes.Transparent,
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                var menu = new ContextMenu { ItemsSource = BuildOrganMenu(organ) };
                line.ContextMenu = menu;
                line.PointerReleased += (_, e) =>
                {
                    if (e.InitialPressMouseButton == MouseButton.Left) menu.Open(line);
                };
                ToolTip.SetTip(line, _sim.Describe(organ));
                _organLines[organ] = line;
                stack.Children.Add(line);
            }

            SystemCardsContainer.Children.Add(new Border
            {
                Background = Ui.Brush("#252D3B"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 8),
                Child = stack,
            });
        }
    }

    private void BuildCallouts()
    {
        foreach (var spec in CalloutSpecs)
        {
            var organ = _sim.Organs.FirstOrDefault(o => o.Name == spec.Organ);
            if (organ == null) continue;

            var cv = new CalloutVisual { Organ = organ, Spec = spec };
            double boxLeft = spec.Right ? RightBoxLeft : LeftBoxLeft;
            double boxW = spec.Right ? RightBoxW : LeftBoxW;
            var stroke = Ui.Brush(spec.LineColor);

            void AddLine(double x1, double y1, double x2, double y2)
            {
                var line = new Shapes.Line
                {
                    StartPoint = new Point(x1, y1),
                    EndPoint = new Point(x2, y2),
                    Stroke = stroke,
                    StrokeThickness = 1.3,
                    IsHitTestVisible = false,
                };
                AvatarCanvas.Children.Add(line);
                cv.Decor.Add(line);
            }

            if (spec.Right)
            {
                if (Math.Abs(spec.Y - spec.TargetY) < 1.5) AddLine(boxLeft, spec.Y, spec.TargetX, spec.TargetY);
                else
                {
                    AddLine(boxLeft, spec.Y, boxLeft - Elbow, spec.Y);
                    AddLine(boxLeft - Elbow, spec.Y, spec.TargetX, spec.TargetY);
                }
            }
            else AddLine(boxLeft + boxW, spec.Y, spec.TargetX, spec.TargetY);

            var dot = new Shapes.Ellipse { Width = 8, Height = 8, Fill = Brushes.White, IsHitTestVisible = false };
            Canvas.SetLeft(dot, spec.TargetX - 4);
            Canvas.SetTop(dot, spec.TargetY - 4);
            AvatarCanvas.Children.Add(dot);
            cv.Decor.Add(dot);

            _callouts.Add(cv);
        }

        // buttons are added after all lines so they sit on top
        foreach (var cv in _callouts)
        {
            bool right = cv.Spec.Right;
            double w = right ? RightBoxW : LeftBoxW, h = right ? RightBoxH : LeftBoxH;
            var btn = new Button
            {
                Width = w,
                Height = h,
                Padding = new Thickness(10, 0),
                FontSize = right ? 11 : 10,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            Canvas.SetLeft(btn, right ? RightBoxLeft : LeftBoxLeft);
            Canvas.SetTop(btn, cv.Spec.Y - h / 2);

            var menu = new ContextMenu { ItemsSource = BuildOrganMenu(cv.Organ) };
            btn.ContextMenu = menu;
            btn.Click += (_, _) => menu.Open(btn);
            ToolTip.SetTip(btn, $"Click to inject a fault into the {cv.Organ.Name.ToLowerInvariant()} or restore it");

            cv.Button = btn;
            AvatarCanvas.Children.Add(btn);
        }
    }

    private void BuildLayers()
    {
        foreach (var layer in _callouts.Select(c => c.Organ.Layer).Distinct())
        {
            string name = layer;
            _layerEnabled[name] = true;
            var cb = new CheckBox { Content = name, IsChecked = true, Foreground = Brushes.White, FontSize = 11 };
            cb.IsCheckedChanged += (_, _) => _layerEnabled[name] = cb.IsChecked == true;
            LayersPanel.Children.Add(cb);
        }
    }

    private void BuildParameterUi()
    {
        ParamSelector.ItemsSource = _sim.Parameters.Select(p => p.Name).ToList();
        ParamSelector.SelectionChanged += (_, _) => PopulateParamValues();
        ParamValueSelector.SelectionChanged += OnParamValueChanged;
    }

    private void BuildSpeedUi()
    {
        var labels = SpeedOptions.Select(s => $"{s:0.##}x").ToList();
        SpeedBox.ItemsSource = labels;
        for (int i = 0; i < labels.Count; i++)
        {
            int idx = i;
            var mi = new MenuItem { Header = labels[i] };
            mi.Click += (_, _) => SpeedBox.SelectedIndex = idx;
            _speedItems.Add(mi);
        }
        SpeedMenu.ItemsSource = _speedItems;
        SpeedBox.SelectedIndex = Math.Max(0, Array.IndexOf(SpeedOptions, 1.0));
        SyncSpeedMenu();
        SpeedBox.SelectionChanged += OnSpeedChanged;
    }

    private void BuildFaultMenus()
    {
        // organs grouped by body system, each with fail / degrade / restore actions
        FaultOrgansMenu.ItemsSource = _systemGroups.Select(g => new MenuItem
        {
            Header = g.Key,
            ItemsSource = g.Select(o => new MenuItem { Header = o.Name, ItemsSource = BuildOrganMenu(o) }).ToList(),
        }).ToList();

        // clinical scenarios grouped by category
        ScenarioMenu.ItemsSource = Simulation.Scenarios.GroupBy(sc => sc.Category).Select(g => new MenuItem
        {
            Header = g.Key,
            ItemsSource = g.Select(sc =>
            {
                var mi = new MenuItem { Header = sc.Name };
                ToolTip.SetTip(mi, sc.Description);
                mi.Click += (_, _) => _sim.ApplyScenario(sc);
                return mi;
            }).ToList(),
        }).ToList();
    }

    private List<MenuItem> BuildOrganMenu(Organ o) => new()
    {
        Item($"Fail {o.Name} (0%)", () => _sim.SetCondition(o, 0)),
        Item("Degrade to 50%", () => _sim.SetCondition(o, 50)),
        Item("Degrade to 25%", () => _sim.SetCondition(o, 25)),
        Item("Restore to 100%", () => _sim.RestoreOrgan(o)),
    };

    private static MenuItem Item(string header, Action action)
    {
        var mi = new MenuItem { Header = header };
        mi.Click += (_, _) => action();
        return mi;
    }

    private static void SetCheck(MenuItem item, bool on) =>
        item.Icon = on ? new TextBlock { Text = "✓" } : null;

    // ------------------------------------------------------------------ main loop
    private void OnTick(object? sender, EventArgs e)
    {
        double now = _clock.Elapsed.TotalSeconds;
        double dt = Math.Min(now - _lastTick, 0.25);   // clamp so a window drag cannot cause a time jump
        _lastTick = now;

        if (_running)
        {
            _sim.Advance(dt * _speed);
            _ecg.Advance(dt, _sim.Vitals.HeartRate, _sim.IsFibrillating);
        }
        _cases.Tick();
        RefreshUi();
    }

    private void RefreshUi()
    {
        var v = _sim.Vitals;
        var state = _sim.SystemState;

        // header
        ClockText.Text = DateTime.Now.ToString("MMM dd, yyyy | HH:mm:ss", CultureInfo.InvariantCulture).ToUpperInvariant();
        SessionText.Text = _running ? "SESSION: RUNNING" : "SESSION: PAUSED";
        TopStatusBanner.Text = $"Status: SYSTEM {state.ToUpperInvariant()} / {(_running ? "ACTIVE" : "PAUSED")}";
        TopStatusBanner.Foreground = Ui.Brush(Ui.StateColor(state));

        // playback bar
        TimeText.Text = $"TIME: {Simulation.FormatTime(_sim.Time)}";
        BtnPlay.IsEnabled = !_running;
        BtnPause.IsEnabled = _running;
        BtnStep.IsEnabled = !_running;
        MenuSimToggle.Header = _running ? "Pause" : "Play";
        if (!_sliderDragging)
        {
            double max = Math.Max(1, _sim.History.Count > 0 ? _sim.History[^1].Time : 1);
            _updatingSlider = true;
            TimelineSlider.Maximum = max;
            TimelineSlider.Value = Math.Min(_sim.Time, max);
            _updatingSlider = false;
        }

        // system cards
        foreach (var (organ, line) in _organLines)
        {
            string extras = organ.Metrics.Count == 0
                ? ""
                : " [" + string.Join(", ", organ.Metrics.Select(m => $"{m.Key} {m.Value}")) + "]";
            line.Text = $"• {organ.Name}: {organ.Efficiency:0}%{extras}";
            line.Foreground = Ui.Brush(organ.Status == OrganStatus.Nominal ? "#CBD5E0" : Ui.StatusColor(organ.Status));
        }
        foreach (var group in _systemGroups)
        {
            var worst = group.Max(o => o.Status);
            var tb = _systemStatus[group.Key];
            tb.Text = "● " + (worst == OrganStatus.Offline ? "OFFLINE" : worst.ToString().ToUpperInvariant());
            tb.Foreground = Ui.Brush(Ui.StatusColor(worst));
        }

        // body callouts
        foreach (var cv in _callouts)
        {
            var o = cv.Organ;
            var status = o.Status;
            bool layerOn = !_layerEnabled.TryGetValue(o.Layer, out var on) || on;
            string value = status == OrganStatus.Offline ? "OFFLINE" : $"{o.Efficiency:0}%";
            cv.Button.IsVisible = layerOn;
            foreach (var d in cv.Decor) d.IsVisible = layerOn && _showLines;
            cv.Button.Content = cv.Spec.Right ? $"{o.Name}\n{value}" : $"{o.Name}  {value}";
            cv.Button.Background = Ui.Brush(status == OrganStatus.Nominal ? cv.Spec.BoxColor : StatusFill(status));
            cv.Button.BorderBrush = Ui.Brush(status == OrganStatus.Nominal ? cv.Spec.LineColor : Ui.StatusColor(status));
        }

        // gait
        AvatarGaitText.Text = "Gait State: " + _sim.Gait;
        AvatarGaitText.Foreground = Ui.Brush(
            _sim.Gait.StartsWith("Normal") ? "#CBD5E0" :
            _sim.Gait.StartsWith("Impaired") ? "#F6AD55" : "#FC8181");

        // vitals + ECG
        EcgTitle.Text = $"REAL-TIME ECG (Lead II) — {(_sim.IsFibrillating ? "Ventricular Fibrillation (SHOCKABLE)" : _sim.CprOn && v.HeartRate >= 100 && _sim.Vitals.CardiacOutput < 3 ? "CPR in progress" : Rhythm(v.HeartRate))}";
        VitalsLine1.Text = $"HR {v.HeartRate:0} bpm  |  BP {v.Systolic:0}/{v.Diastolic:0}  |  SpO2 {v.SpO2:0}%  |  RR {v.RespRate:0}/min";
        VitalsLine2.Text = $"CO {v.CardiacOutput:0.0} L/min  |  GFR {v.Gfr:0} mL/min  |  Toxins {v.Toxins:0}%  |  Energy {v.Energy:0}%";
        VitalsLine3.Text = $"Temp {v.Temperature:0.0} °C  |  pH {v.Ph:0.00}  |  Infection {v.Infection:0}%  |  Red cells {v.Hemoglobin:0}%";
        var ecgValues = _ecgChart.Series[0].Values;
        ecgValues.Clear();
        ecgValues.AddRange(_ecg.Buffer);
        _ecgChart.InvalidateVisual();

        // active diseases, medicines and procedures
        var lines = new List<string>();
        lines.Add("Diseases:");
        lines.AddRange(_sim.Diseases.Count == 0 ? new[] { "  none" } : _sim.Diseases.Select(d => $"  {Pathology.Find(d.Key)?.Name} {d.Value:0}%"));
        lines.Add("Medicines:");
        lines.AddRange(_sim.Drugs.Count == 0 ? new[] { "  none" } : _sim.Drugs.OrderBy(d => d.Key).Select(d => $"  {d.Key} x{d.Value:0.0}"));
        var running = new[] { Rx.Oxygen, Rx.Ventilator, Rx.Cpr, Rx.Dialysis }.Where(_sim.IsOn).ToList();
        lines.Add("Procedures:");
        lines.Add(running.Count == 0 ? "  none" : "  " + string.Join(", ", running));
        if (_cases.Active != null) lines.Add($"CASE: {_cases.Active.Title}\n  time left {Simulation.FormatTime(_cases.TimeLeft)}");
        ActiveText.Text = string.Join("\n", lines);
        foreach (var (procName, item) in _procMenuItems) SetCheck(item, _sim.IsOn(procName));

        // parameters
        ParamSummaryText.Text = string.Join("\n", _sim.Parameters.Select(p => $"{p.Name}: {p.Options[p.Index]}"));
        if (ParamSelector.SelectedIndex >= 0 && ParamSelector.SelectedIndex < _sim.Parameters.Count)
        {
            var p = _sim.Parameters[ParamSelector.SelectedIndex];
            if (ParamValueSelector.SelectedIndex != p.Index)
            {
                _loadingParam = true;
                ParamValueSelector.SelectedIndex = p.Index;
                _loadingParam = false;
            }
        }

        if (_logDirty) RebuildLog();
    }

    private static string StatusFill(OrganStatus s) => s switch
    {
        OrganStatus.Degraded => "#975A16",
        OrganStatus.Critical => "#9C4221",
        _ => "#742A2A",
    };

    private static string Rhythm(double hr) =>
        hr <= 0 ? "Asystole (flatline)" :
        hr < 50 ? "Sinus Bradycardia" :
        hr > 100 ? "Sinus Tachycardia" : "Normal Sinus Rhythm";

    // ------------------------------------------------------------------ log
    private void OnSimEvent(SimEvent e)
    {
        _log.Add(e);
        if (_log.Count > 2000) _log.RemoveRange(0, 500);
        _logDirty = true;
    }

    private static string FormatLine(SimEvent e) => $"[{e.Wall:HH:mm:ss}] {e.Text}";

    private void RebuildLog()
    {
        _logDirty = false;
        string filter = LogFilter.Text?.Trim() ?? "";
        LogContainer.Children.Clear();
        int shown = 0;
        for (int i = _log.Count - 1; i >= 0 && shown < 200; i--)
        {
            var e = _log[i];
            string line = FormatLine(e);
            if (filter.Length > 0 && line.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            LogContainer.Children.Add(new TextBlock
            {
                Text = line,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = Ui.Brush(Ui.SeverityColor(e.Severity)),
            });
            shown++;
        }
    }

    // ------------------------------------------------------------------ playback
    private void SetRunning(bool run)
    {
        if (_running == run) return;
        _running = run;
        _sim.Raise(Severity.Action, run ? "[USER]: Simulation resumed." : "[USER]: Simulation paused.");
        RefreshUi();
    }

    private void DoStep()
    {
        if (_running) return;
        _sim.Step();
        _ecg.Advance(1.0, _sim.Vitals.HeartRate, _sim.IsFibrillating);
        RefreshUi();
    }

    private void DoReset()
    {
        _sim.Reset();
        RefreshUi();
    }

    private void OnPlayClicked(object? s, RoutedEventArgs e) => SetRunning(true);
    private void OnPauseClicked(object? s, RoutedEventArgs e) => SetRunning(false);
    private void OnToggleRun(object? s, RoutedEventArgs e) => SetRunning(!_running);
    private void OnStepClicked(object? s, RoutedEventArgs e) { if (_running) SetRunning(false); DoStep(); }
    private void OnResetClicked(object? s, RoutedEventArgs e) => DoReset();

    private void OnSliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != RangeBase.ValueProperty || _updatingSlider) return;
        // the user moved the timeline: pause and jump to the nearest recorded moment
        if (_running) SetRunning(false);
        double target = TimelineSlider.Value;
        int idx = _sim.History.FindLastIndex(snap => snap.Time <= target + 1e-9);
        _sim.RestoreSnapshot(Math.Max(0, idx));
        RefreshUi();
    }

    private void OnSpeedChanged(object? sender, SelectionChangedEventArgs e)
    {
        int idx = SpeedBox.SelectedIndex;
        if (idx < 0 || idx >= SpeedOptions.Length) return;
        _speed = SpeedOptions[idx];
        SyncSpeedMenu();
        _sim.Raise(Severity.Action, $"[USER]: Simulation speed set to {SpeedOptions[idx]:0.##}x.");
    }

    private void SyncSpeedMenu()
    {
        for (int i = 0; i < _speedItems.Count; i++)
            SetCheck(_speedItems[i], i == SpeedBox.SelectedIndex);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox) return;
        switch (e.Key)
        {
            case Key.Space: SetRunning(!_running); e.Handled = true; break;
            case Key.N: if (_running) SetRunning(false); DoStep(); e.Handled = true; break;
            case Key.R: DoReset(); e.Handled = true; break;
            case Key.F: _sim.InjectRandomFault(); e.Handled = true; break;
            case Key.H: OpenHospital(); e.Handled = true; break;
            case Key.D: _sim.Defibrillate(); e.Handled = true; break;
            case Key.C: _sim.SetProcedure(Rx.Cpr, !_sim.IsOn(Rx.Cpr)); e.Handled = true; break;
            case Key.O: _sim.SetProcedure(Rx.Oxygen, !_sim.IsOn(Rx.Oxygen)); e.Handled = true; break;
        }
    }

    // ------------------------------------------------------------------ parameters
    private void PopulateParamValues()
    {
        int i = ParamSelector.SelectedIndex;
        if (i < 0 || i >= _sim.Parameters.Count) return;
        var p = _sim.Parameters[i];
        _loadingParam = true;
        ParamValueSelector.ItemsSource = p.Options;
        ParamValueSelector.SelectedIndex = p.Index;
        ParamValueSelector.IsEnabled = true;
        _loadingParam = false;
        ParamValueLabel.Text = p.Name;
        ParamEffectText.Text = p.Effect;
    }

    private void OnParamValueChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loadingParam) return;
        int pi = ParamSelector.SelectedIndex, vi = ParamValueSelector.SelectedIndex;
        if (pi < 0 || vi < 0) return;
        _sim.SetParameter(_sim.Parameters[pi], vi);
    }

    // ------------------------------------------------------------------ FILE menu
    private async void OnSaveSession(object? s, RoutedEventArgs e) =>
        await SaveTextAsync("Save session", $"biosim-session-{DateTime.Now:yyyyMMdd-HHmmss}.json", "json", "BioSim session", _sim.ExportSession());

    private async void OnExportLog(object? s, RoutedEventArgs e)
    {
        string text = string.Join(Environment.NewLine, _log.Select(x =>
            $"[{x.Wall:yyyy-MM-dd HH:mm:ss}] [T+{Simulation.FormatTime(x.SimTime)}] [{x.Severity}] {x.Text}"));
        await SaveTextAsync("Export telemetry log", $"biosim-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt", "txt", "Text file", text);
    }

    private async void OnExportCsv(object? s, RoutedEventArgs e) =>
        await SaveTextAsync("Export vitals history", $"biosim-vitals-{DateTime.Now:yyyyMMdd-HHmmss}.csv", "csv", "CSV file", _sim.BuildCsv());

    private async void OnLoadSession(object? s, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Load session",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("BioSim session") { Patterns = new[] { "*.json" } } },
            });
            if (files.Count == 0) return;
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            string json = await reader.ReadToEndAsync();
            if (!_sim.ImportSession(json, out string error))
                _sim.Raise(Severity.Warning, $"[FILE]: Could not load session — {error}");
        }
        catch (Exception ex)
        {
            _sim.Raise(Severity.Warning, $"[FILE]: Load failed — {ex.Message}");
        }
    }

    private async Task SaveTextAsync(string title, string suggestedName, string extension, string description, string content)
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = suggestedName,
                DefaultExtension = extension,
                FileTypeChoices = new[] { new FilePickerFileType(description) { Patterns = new[] { "*." + extension } } },
            });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(content);
            _sim.Raise(Severity.Success, $"[FILE]: Saved {file.Name}.");
        }
        catch (Exception ex)
        {
            _sim.Raise(Severity.Warning, $"[FILE]: Save failed — {ex.Message}");
        }
    }

    private void OnExit(object? s, RoutedEventArgs e) => Close();

    // ------------------------------------------------------------------ VIEW menu
    private void OnToggleLines(object? s, RoutedEventArgs e)
    {
        _showLines = !_showLines;
        SetCheck(MenuLines, _showLines);
    }

    private void OnToggleCards(object? s, RoutedEventArgs e)
    {
        bool show = !CardsPanel.IsVisible;
        CardsPanel.IsVisible = show;
        WorkGrid.ColumnDefinitions[0].Width = show ? new GridLength(290) : new GridLength(0);
        SetCheck(MenuCards, show);
    }

    private void OnToggleLog(object? s, RoutedEventArgs e)
    {
        LogPanel.IsVisible = !LogPanel.IsVisible;
        SetCheck(MenuLog, LogPanel.IsVisible);
    }

    private void OnToggleEcg(object? s, RoutedEventArgs e)
    {
        EcgPanel.IsVisible = !EcgPanel.IsVisible;
        SetCheck(MenuEcg, EcgPanel.IsVisible);
    }

    private void OnClearLog(object? s, RoutedEventArgs e)
    {
        _log.Clear();
        _logDirty = true;
    }

    // ------------------------------------------------------------------ HOSPITAL menu
    private void BuildHospitalMenus()
    {
        CaseMenu.ItemsSource = Training.Cases.GroupBy(c => c.Difficulty).OrderBy(g => g.Key).Select(g => new MenuItem
        {
            Header = $"Level {g.Key}" + (g.Key == 1 ? " (easy)" : g.Key == 2 ? " (medium)" : " (hard)"),
            ItemsSource = g.Select(c => Item(c.Title, () => StartCase(c))).ToList(),
        }).ToList();

        DiseaseMenu.ItemsSource = Pathology.Diseases.GroupBy(d => d.Category).Select(g => new MenuItem
        {
            Header = g.Key,
            ItemsSource = g.Select(d =>
            {
                var mi = Item(d.Name, () => _sim.InjectDisease(d));
                ToolTip.SetTip(mi, d.Description + "\n\n" + d.Teaching);
                return mi;
            }).ToList(),
        }).ToList();

        PharmacyMenu.ItemsSource = Pharmacy.All.GroupBy(m => m.Category).Select(g => new MenuItem
        {
            Header = g.Key,
            ItemsSource = g.Select(m =>
            {
                var mi = Item($"{m.Name} — {m.Dose}", () => _sim.GiveMedicine(m));
                ToolTip.SetTip(mi, m.Use + "\n\nWarning: " + m.Warning);
                return mi;
            }).ToList(),
        }).ToList();

        TestsMenu.ItemsSource = Lab.All.GroupBy(t => t.Group).Select(g => new MenuItem
        {
            Header = g.Key,
            ItemsSource = g.Select(t => Item($"{t.Name}  ({t.Seconds:0} s)", () => _sim.OrderTest(t))).ToList(),
        }).ToList();

        var procedures = new List<MenuItem>();
        foreach (var name in new[] { Rx.Oxygen, Rx.Ventilator, Rx.Cpr, Rx.Dialysis })
        {
            string procedure = name;
            var mi = Item(procedure, () => _sim.SetProcedure(procedure, !_sim.IsOn(procedure)));
            _procMenuItems[procedure] = mi;
            procedures.Add(mi);
        }
        procedures.Add(Item("Defibrillate (shock)", () => _sim.Defibrillate()));
        procedures.Add(Item("Transfuse 1 unit of blood", () => _sim.Transfuse()));
        ProceduresMenu.ItemsSource = procedures;
    }

    private void StartCase(MedicalCase c)
    {
        _cases.Start(c);
        OpenHospital();
        _hospital?.ShowCaseTab();
    }

    private void OpenHospital()
    {
        if (_hospital is { IsVisible: true }) { _hospital.Activate(); return; }
        _hospital = new HospitalWindow(_sim, _cases);
        _hospital.Show(this);
    }

    private void OnOpenHospital(object? s, RoutedEventArgs e) => OpenHospital();

    // ------------------------------------------------------------------ FAULT MODEL menu
    private void OnRandomFault(object? s, RoutedEventArgs e) => _sim.InjectRandomFault();
    private void OnRestoreAll(object? s, RoutedEventArgs e) => _sim.RestoreAll();

    // ------------------------------------------------------------------ ANALYTICS menu
    private void OnOpenAnalytics(object? s, RoutedEventArgs e)
    {
        if (_analytics is { IsVisible: true }) { _analytics.Activate(); return; }
        _analytics = new AnalyticsWindow(_sim);
        _analytics.Show(this);
    }

    private void OnOpenReport(object? s, RoutedEventArgs e) =>
        new InfoWindow("Organ Summary Report", _sim.BuildReport(), 760, 600).Show(this);

    private void OnOpenDependencyMap(object? s, RoutedEventArgs e) =>
        new InfoWindow("Organ Dependency Map", _sim.BuildDependencyMap(), 900, 700).Show(this);

    // ------------------------------------------------------------------ HELP menu
    private void OnHelpShortcuts(object? s, RoutedEventArgs e) =>
        new InfoWindow("Keyboard Shortcuts",
            "Space ........ Play / Pause\n" +
            "N ............ Step forward (+5 s, only while paused)\n" +
            "R ............ Reset to healthy baseline\n" +
            "F ............ Inject a random fault\n" +
            "H ............ Open the Hospital window (cases, tests, pharmacy, procedures)\n" +
            "D ............ Defibrillate (only helps ventricular fibrillation)\n" +
            "C ............ Start / stop CPR\n" +
            "O ............ Start / stop oxygen therapy\n\n" +
            "Mouse\n" +
            "Click an organ box on the body ...... fail / degrade / restore that organ\n" +
            "Drag the timeline slider ............ pause and scrub back through the recorded history\n" +
            "System Layers checkboxes ............ show or hide organ callouts\n" +
            "Parameters .......................... change activity, metabolism or hydration\n",
            640, 400).Show(this);

    private void OnHelpModel(object? s, RoutedEventArgs e) =>
        new InfoWindow("How The Model Works",
            $"{_sim.Organs.Count} organs in {_systemGroups.Count} body systems.\n\n" +
            "Every organ has a CONDITION (set by faults), an INJURY (permanent damage from oxygen starvation, sepsis or\n" +
            "overdose), a PENALTY (reversible loss from toxins, acid, heat, low glucose, anemia or active disease) and\n" +
            "SUPPORT (how much of its function still arrives from the organs it needs).\n" +
            "  function = (condition - injury - penalty) x support\n" +
            $"  Nominal >= {Thresholds.NominalFrom:0}%   Degraded >= {Thresholds.DegradedFrom:0}%   Critical >= {Thresholds.OfflineBelow:0}%   Offline below that\n\n" +
            "Chains of dependency (example): Hypothalamus -> Pituitary -> Adrenal Glands -> Kidneys -> bones -> Legs.\n" +
            "Hover any organ in the status cards to see what it needs and what needs it; click it to fail or restore it.\n\n" +
            "Whole-body links (recalculated every tick)\n" +
            "  Heart, blood volume, veins, spleen, skin     -> stroke volume -> cardiac output\n" +
            "  Arteries, spinal cord, adrenals, sepsis      -> vessel tone -> blood pressure\n" +
            "  Airway, diaphragm, rib cage, brainstem       -> breathing -> SpO2\n" +
            "  Output x capillaries x SpO2 x blood          -> OXYGEN DELIVERY\n" +
            "  Oxygen delivery < 60% of demand              -> every organ slowly loses function (brainstem first)\n" +
            "  Liver + kidneys + bowel                      -> clear toxins; toxins poison brain, ear and nerves\n" +
            "  Kidneys + liver + lungs vs lactate and CO2   -> acid level (pH) -> weakens heart, brain and muscles\n" +
            "  Hypothalamus, skin, muscles, environment     -> body temperature (fever, hypothermia)\n" +
            "  Marrow, spleen, thymus, lymph nodes          -> immunity; barriers (skin, gut, lungs, urinary) let infection in\n" +
            "  Infection > 30%                              -> sepsis: low pressure, fever, organ injury\n" +
            "  Marrow + kidney hormone                      -> red blood cells -> oxygen capacity\n" +
            "  Pancreas + digestion + liver                 -> energy; low energy hurts brain and muscles\n\n" +
            "Diseases, medicines and cases (HOSPITAL menu)\n" +
            "  A disease grows by itself, damages organs and body chemistry, and is pushed back only by the right medicine.\n" +
            "  Each medicine dose adds 1.0 to its level, which fades with time; a level above the safe limit is an overdose.\n" +
            "  Procedures: oxygen, ventilator, CPR, dialysis, defibrillation (ventricular fibrillation only), transfusion.\n" +
            "  Patient cases are scored on survival, cure, diagnosis, correct treatment and speed.\n\n" +
            "Compensation: when oxygen delivery drops, the brainstem (with the spinal cord and adrenals)\n" +
            "raises heart rate and breathing rate. Without them there is no compensation.\n\n" +
            "Reference values (resting adult): heart rate 72 bpm, stroke volume 70 mL, BP 120/80 mmHg,\n" +
            "respiratory rate 16/min, tidal volume 500 mL, SpO2 98%, GFR 110 mL/min, temperature 37 °C, pH 7.40.\n" +
            "Cite your textbook or WHO source for these, and note that timings are accelerated for demonstration.\n\n" +
            "This is an educational simplification, not a medical device.",
            860, 720).Show(this);

    private void OnAbout(object? s, RoutedEventArgs e) =>
        new InfoWindow("About BioSim",
            $"{Title}\n\n" +
            "Educational whole-body physiological simulation showing how organs depend on each other\n" +
            "and what happens when one goes offline.\n\n" +
            $".NET runtime : {Environment.Version}\n" +
            $"Operating sys: {RuntimeInformation.OSDescription}\n" +
            $"Avalonia     : {typeof(Application).Assembly.GetName().Version}\n" +
            $"Organs       : {_sim.Organs.Count} in {_sim.Organs.Select(o => o.System).Distinct().Count()} systems\n" +
            $"Recorded     : {_sim.History.Count} history samples\n",
            560, 340).Show(this);
}

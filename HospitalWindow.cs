using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace VirtualMan;

/// <summary>The doctor's workspace: patient cases, investigations, pharmacy, procedures, disease lab and career.</summary>
public sealed class HospitalWindow : Window
{
    private static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Menlo, monospace");

    private readonly Simulation _sim;
    private readonly CaseManager _cases;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    private readonly TextBlock _summary = new() { FontSize = 12, FontWeight = FontWeight.Bold };
    private readonly TextBlock _caseText = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13 };
    private readonly TextBlock _caseStatus = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Ui.Brush("#63B3ED") };
    private readonly TextBlock _labText = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly TextBlock _activeDrugs = new() { FontSize = 11.5, FontFamily = Mono };
    private readonly TextBlock _activeDiseases = new() { FontSize = 11.5, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _diseaseList = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _careerText = new() { FontSize = 13, FontFamily = Mono };
    private readonly ComboBox _caseBox = new() { MinWidth = 420 };
    private readonly ComboBox _diagBox = new() { MinWidth = 320 };
    private readonly TabControl _tabs = new();
    private readonly Dictionary<string, Button> _procButtons = new();
    private readonly List<string> _labLog = new();

    public HospitalWindow(Simulation sim, CaseManager cases)
    {
        _sim = sim;
        _cases = cases;

        Title = "BioSim — Hospital";
        Width = 1120;
        Height = 780;
        Background = Ui.Brush("#141923");
        Foreground = Ui.Brush("#E1E4E8");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _tabs.ItemsSource = new List<TabItem>
        {
            new() { Header = "Patient Case", Content = BuildCaseTab() },
            new() { Header = "Investigations", Content = BuildLabTab() },
            new() { Header = "Pharmacy", Content = BuildPharmacyTab() },
            new() { Header = "Procedures", Content = BuildProceduresTab() },
            new() { Header = "Disease Lab", Content = BuildDiseaseTab() },
            new() { Header = "Career", Content = BuildCareerTab() },
        };
        _tabs.SelectedIndex = 0;

        var footer = new Border { Background = Ui.Brush("#1A202C"), Padding = new Thickness(12, 8), Child = _summary };
        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        Grid.SetRow(footer, 1);
        root.Children.Add(_tabs);
        root.Children.Add(footer);
        Content = root;

        Opened += (_, _) => { Refresh(); _timer.Start(); };
        Closed += (_, _) => _timer.Stop();
        _timer.Tick += (_, _) => Refresh();
    }

    public void ShowCaseTab() => _tabs.SelectedIndex = 0;

    // ------------------------------------------------------------------ small UI helpers

    private static TextBlock Head(string text) => new()
    {
        Text = text, FontSize = 12, FontWeight = FontWeight.Bold,
        Foreground = Ui.Brush("#A0AEC0"), Margin = new Thickness(0, 10, 0, 2),
    };

    private static TextBlock Note(string text) => new()
    {
        Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Ui.Brush("#A0AEC0"),
    };

    private static StackPanel Row(params Control[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var i in items) row.Children.Add(i);
        return row;
    }

    private static Button Btn(string text, string color, Action onClick)
    {
        var b = new Button { Content = text, Padding = new Thickness(14, 6), Background = Ui.Brush(color), Foreground = Brushes.White, FontWeight = FontWeight.Bold };
        b.Click += (_, _) => onClick();
        return b;
    }

    private static Button Tile(string title, string sub, string tip, Action onClick, string color = "#2D3748", double width = 190)
    {
        var content = new StackPanel { Spacing = 2 };
        content.Children.Add(new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeight.Bold, TextTrimming = TextTrimming.CharacterEllipsis });
        content.Children.Add(new TextBlock { Text = sub, FontSize = 10, Foreground = Ui.Brush("#CBD5E0"), TextTrimming = TextTrimming.CharacterEllipsis });
        var b = new Button
        {
            Width = width, Height = 54, Margin = new Thickness(4), Padding = new Thickness(10, 4),
            Background = Ui.Brush(color), Foreground = Brushes.White,
            HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center,
            Content = content,
        };
        ToolTip.SetTip(b, tip);
        b.Click += (_, _) => onClick();
        return b;
    }

    private static Border Card(Control child) => new()
    {
        Background = Ui.Brush("#1B222D"), BorderBrush = Ui.Brush("#2D3748"), BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6), Padding = new Thickness(12), Child = child,
    };

    // ------------------------------------------------------------------ tabs

    private Control BuildCaseTab()
    {
        _caseBox.ItemsSource = Training.Cases.Select(c => $"[Level {c.Difficulty}]  {c.Title}").ToList();
        _caseBox.SelectedIndex = 0;
        _diagBox.ItemsSource = Pathology.Diseases.Select(d => d.Name).ToList();
        _diagBox.PlaceholderText = "Choose a diagnosis";

        var stack = new StackPanel { Margin = new Thickness(16), Spacing = 8 };
        stack.Children.Add(Head("PATIENT CASES"));
        stack.Children.Add(Note("Pick a patient, examine, order tests, diagnose and treat. You are scored on survival, cure, diagnosis, the right treatment, speed — and you lose points for overdoses and wrong shocks."));
        stack.Children.Add(Row(_caseBox, Btn("Start case", "#2B6CB0", () =>
        {
            if (_caseBox.SelectedIndex >= 0) _cases.Start(Training.Cases[_caseBox.SelectedIndex]);
            Refresh();
        })));

        stack.Children.Add(Head("BRIEFING"));
        stack.Children.Add(Card(_caseText));
        stack.Children.Add(_caseStatus);

        stack.Children.Add(Head("YOUR DIAGNOSIS"));
        stack.Children.Add(Row(_diagBox, Btn("Record diagnosis", "#38A169", () =>
        {
            if (_diagBox.SelectedIndex >= 0) _cases.SetDiagnosis(Pathology.Diseases[_diagBox.SelectedIndex].Id);
            Refresh();
        })));
        stack.Children.Add(Btn("End case and get my score", "#C53030", () => { _cases.EndNow(); Refresh(); }));
        stack.Children.Add(Note("Tip: use the Investigations tab to find the diagnosis, the Pharmacy and Procedures tabs to treat. Run the simulation at a higher speed for slow diseases such as tuberculosis."));
        return new ScrollViewer { Content = stack };
    }

    private Control BuildLabTab()
    {
        var stack = new StackPanel { Margin = new Thickness(16), Spacing = 4 };
        stack.Children.Add(Head("ORDER A TEST (each test takes simulated time; the patient keeps changing meanwhile)"));
        foreach (var group in Lab.All.GroupBy(t => t.Group))
        {
            stack.Children.Add(Head(group.Key.ToUpperInvariant()));
            var wrap = new WrapPanel();
            foreach (var test in group)
            {
                var t = test;
                wrap.Children.Add(Tile(t.Name, $"takes {t.Seconds:0} s", t.Name, () =>
                {
                    string result = _sim.OrderTest(t);
                    _labLog.Insert(0, $"[{Simulation.FormatTime(_sim.Time)}]  {t.Name}\n    {result}");
                    _labText.Text = string.Join("\n\n", _labLog);
                }, "#2C5282"));
            }
            stack.Children.Add(wrap);
        }
        stack.Children.Add(Head("RESULTS (newest first)"));
        stack.Children.Add(Card(_labText));
        _labText.Text = "No tests ordered yet.";
        return new ScrollViewer { Content = stack };
    }

    private Control BuildPharmacyTab()
    {
        var left = new StackPanel { Margin = new Thickness(16), Spacing = 4 };
        left.Children.Add(Head("CLICK A MEDICINE TO GIVE ONE DOSE (a second dose on top of the first can overdose the patient)"));
        foreach (var group in Pharmacy.All.GroupBy(m => m.Category))
        {
            left.Children.Add(Head(group.Key.ToUpperInvariant()));
            var wrap = new WrapPanel();
            foreach (var med in group)
            {
                var m = med;
                wrap.Children.Add(Tile(m.Name, $"{m.Dose} ({m.Route})",
                    $"{m.Use}\n\nWarning: {m.Warning}", () => _sim.GiveMedicine(m), "#276749", 230));
            }
            left.Children.Add(wrap);
        }

        var right = new StackPanel { Spacing = 6 };
        right.Children.Add(Head("DISEASES IN THE PATIENT"));
        right.Children.Add(_activeDiseases);
        right.Children.Add(Head("MEDICINE LEVELS"));
        right.Children.Add(_activeDrugs);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,330") };
        var leftScroll = new ScrollViewer { Content = left };
        var rightPanel = new Border { Margin = new Thickness(0, 16, 16, 16), Child = Card(right) };
        Grid.SetColumn(rightPanel, 1);
        grid.Children.Add(leftScroll);
        grid.Children.Add(rightPanel);
        return grid;
    }

    private Control BuildProceduresTab()
    {
        var stack = new StackPanel { Margin = new Thickness(16), Spacing = 8 };
        stack.Children.Add(Head("LIFE SUPPORT"));

        foreach (var (name, desc) in new[]
        {
            (Rx.Oxygen, "Raises blood oxygen when the lungs are weak. Cannot help if no air is moving."),
            (Rx.Ventilator, "A machine breathes for the patient (airway obstruction, brainstem or diaphragm failure)."),
            (Rx.Cpr, "Chest compressions give about 30% of normal blood flow when the heart is not pumping. Harmful on a beating heart."),
            (Rx.Dialysis, "A machine filters toxins and acid when the kidneys fail; it can lower blood pressure."),
        })
        {
            string procedure = name;
            var button = new Button { Padding = new Thickness(14, 8), Width = 420, HorizontalContentAlignment = HorizontalAlignment.Left, Foreground = Brushes.White, FontWeight = FontWeight.Bold };
            button.Click += (_, _) => _sim.SetProcedure(procedure, !_sim.IsOn(procedure));
            _procButtons[procedure] = button;
            stack.Children.Add(Row(button, new TextBlock { Text = desc, Width = 560, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Ui.Brush("#A0AEC0"), VerticalAlignment = VerticalAlignment.Center }));
        }

        stack.Children.Add(Head("EMERGENCY ACTIONS"));
        stack.Children.Add(Row(
            Btn("Defibrillate (shock)", "#D69E2E", () => _sim.Defibrillate()),
            new TextBlock { Text = "Only helps ventricular fibrillation. A shock on any other rhythm injures the heart.", FontSize = 12, Foreground = Ui.Brush("#A0AEC0"), VerticalAlignment = VerticalAlignment.Center }));
        stack.Children.Add(Row(
            Btn("Transfuse 1 unit of blood", "#C53030", () => _sim.Transfuse()),
            new TextBlock { Text = "Restores blood volume and red cells after bleeding or severe anaemia.", FontSize = 12, Foreground = Ui.Brush("#A0AEC0"), VerticalAlignment = VerticalAlignment.Center }));
        return new ScrollViewer { Content = stack };
    }

    private Control BuildDiseaseTab()
    {
        var stack = new StackPanel { Margin = new Thickness(16), Spacing = 4 };
        stack.Children.Add(Head("INJECT A DISEASE (sandbox). It grows on its own, damages organs and chemistry, and only the right treatment reverses it."));
        foreach (var group in Pathology.Diseases.GroupBy(d => d.Category))
        {
            stack.Children.Add(Head(group.Key.ToUpperInvariant()));
            var wrap = new WrapPanel();
            foreach (var disease in group)
            {
                var d = disease;
                wrap.Children.Add(Tile(d.Name, d.Description,
                    $"{d.Description}\n\nSymptoms: {d.Symptoms}\n\n{d.Teaching}", () => _sim.InjectDisease(d), "#742A2A", 300));
            }
            stack.Children.Add(wrap);
        }
        stack.Children.Add(Head("ACTIVE"));
        stack.Children.Add(Card(_diseaseList));
        stack.Children.Add(Btn("Clear all diseases", "#4A5568", () => _sim.ClearDiseases()));
        return new ScrollViewer { Content = stack };
    }

    private Control BuildCareerTab()
    {
        var stack = new StackPanel { Margin = new Thickness(16), Spacing = 8 };
        stack.Children.Add(Head("YOUR DOCTOR CAREER"));
        stack.Children.Add(Card(_careerText));
        stack.Children.Add(Note("Experience points = score x case level. Your rank and best scores are saved on this computer."));
        return new ScrollViewer { Content = stack };
    }

    // ------------------------------------------------------------------ live refresh

    private static string Bars(double level)
    {
        int n = Math.Min(8, (int)Math.Round(level * 2));
        return "[" + new string('#', n) + new string('.', 8 - n) + "]";
    }

    private void Refresh()
    {
        var v = _sim.Vitals;
        _summary.Text = $"{_sim.SystemState.ToUpperInvariant()}   HR {v.HeartRate:0}   BP {v.Systolic:0}/{v.Diastolic:0}   SpO2 {v.SpO2:0}%   RR {v.RespRate:0}   Temp {v.Temperature:0.0} °C   pH {v.Ph:0.00}   Sim time {Simulation.FormatTime(_sim.Time)}"
                       + (_sim.IsFibrillating ? "   *** VENTRICULAR FIBRILLATION ***" : "");
        _summary.Foreground = Ui.Brush(Ui.StateColor(_sim.SystemState));

        // case
        var active = _cases.Active;
        if (active != null)
        {
            _caseText.Text = active.Title + "\n\n" + active.Briefing;
            string diagnosis = _cases.Diagnosis == null ? "not recorded yet" : Pathology.Find(_cases.Diagnosis)?.Name ?? "";
            _caseStatus.Text = $"Case running — elapsed {Simulation.FormatTime(_cases.Elapsed)} of {Simulation.FormatTime(active.TimeLimit)}.   Your diagnosis: {diagnosis}";
        }
        else
        {
            var last = _cases.LastResult;
            _caseText.Text = last == null
                ? "No active case. Choose a patient above and press Start case."
                : $"Last case: {last.Title}\nOutcome: {last.Outcome}   Score {last.Score}/100 (grade {last.Grade})   +{last.Xp} XP";
            _caseStatus.Text = "";
        }

        // disease and medicine lists
        string diseases = _sim.Diseases.Count == 0
            ? "No active disease."
            : string.Join("\n", _sim.Diseases.Select(d => $"{Pathology.Find(d.Key)?.Name}  —  {d.Value:0}%"));
        _activeDiseases.Text = diseases;
        _diseaseList.Text = diseases;
        _activeDrugs.Text = _sim.Drugs.Count == 0
            ? "No medicine in the body."
            : string.Join("\n", _sim.Drugs.OrderBy(d => d.Key).Select(d => $"{Bars(d.Value)} {d.Value:0.0}  {d.Key}"));

        // procedures
        foreach (var (name, button) in _procButtons)
        {
            bool on = _sim.IsOn(name);
            button.Content = on ? $"{name}: ON  (click to stop)" : $"{name}: OFF  (click to start)";
            button.Background = Ui.Brush(on ? "#2F855A" : "#4A5568");
        }

        // career
        var c = _cases.Career;
        var best = c.BestScores.Count == 0
            ? "  none yet"
            : string.Join("\n", c.BestScores.OrderBy(b => b.Key).Select(b => $"  {Training.Cases.FirstOrDefault(x => x.Id == b.Key)?.Title ?? b.Key,-44} {b.Value,3}/100"));
        _careerText.Text = $"Rank            : {c.Rank}\nExperience      : {c.TotalXp} XP   (next: {c.NextRank})\nCases completed : {c.CasesCompleted}\n\nBest scores\n{best}";
    }
}

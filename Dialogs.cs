using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace VirtualMan;

/// <summary>Simple read-only text window (help, about, reports).</summary>
public sealed class InfoWindow : Window
{
    public InfoWindow(string title, string text, double width = 680, double height = 520)
    {
        Title = title;
        Width = width;
        Height = height;
        Background = Ui.Brush("#141923");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = new ScrollViewer
        {
            Content = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(18),
                FontSize = 12.5,
                FontFamily = new FontFamily("Cascadia Mono, Consolas, Menlo, monospace"),
                Foreground = Ui.Brush("#E1E4E8"),
            }
        };
    }
}

/// <summary>Live charts drawn from the simulation history.</summary>
public sealed class AnalyticsWindow : Window
{
    private const int WindowPoints = 600;   // newest 10 minutes of simulated time

    private sealed class Line
    {
        public string Name = "";
        public string Color = "";
        public Func<Snapshot, double> Select = _ => 0;
    }

    private sealed class ChartPanel
    {
        public ChartControl Chart = new();
        public List<Line> Lines = new();
    }

    private readonly Simulation _sim;
    private readonly List<ChartPanel> _panels = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    public AnalyticsWindow(Simulation sim)
    {
        _sim = sim;
        Title = "BioSim — Live Analytics";
        Width = 1250;
        Height = 720;
        Background = Ui.Brush("#141923");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var grid = new UniformGrid { Rows = 2, Columns = 3, Margin = new Thickness(8) };

        grid.Children.Add(MakePanel("Circulation", autoScale: true, 0, 100, new List<Line>
        {
            new() { Name = "Heart rate (bpm)", Color = "#FC8181", Select = s => s.HeartRate },
            new() { Name = "Systolic (mmHg)", Color = "#F6AD55", Select = s => s.Systolic },
            new() { Name = "Diastolic (mmHg)", Color = "#ECC94B", Select = s => s.Diastolic },
        }));

        grid.Children.Add(MakePanel("Oxygenation", autoScale: false, 0, 110, new List<Line>
        {
            new() { Name = "SpO2 (%)", Color = "#63B3ED", Select = s => s.SpO2 },
            new() { Name = "O2 delivery (% of demand)", Color = "#68D391", Select = s => s.OxygenDelivery * 100 },
        }));

        var organLines = new List<Line>();
        string[] colors = { "#FC8181", "#63B3ED", "#68D391", "#B794F4", "#ECC94B", "#F6AD55" };
        string[] keyOrgans = { "Heart", "Lungs", "Brainstem", "Kidneys", "Liver", "Pancreas" };
        for (int i = 0; i < keyOrgans.Length; i++)
        {
            int idx = _sim.Organs.FindIndex(o => o.Name == keyOrgans[i]);
            if (idx < 0) continue;
            organLines.Add(new Line { Name = keyOrgans[i], Color = colors[i], Select = s => idx < s.Efficiency.Length ? s.Efficiency[idx] : 0 });
        }
        grid.Children.Add(MakePanel("Organ function (%)", autoScale: false, 0, 100, organLines));

        grid.Children.Add(MakePanel("Metabolic load (%)", autoScale: false, 0, 100, new List<Line>
        {
            new() { Name = "Blood toxins", Color = "#FC8181", Select = s => s.Toxins },
            new() { Name = "Acid level", Color = "#F6AD55", Select = s => s.Acid },
            new() { Name = "Energy supply", Color = "#68D391", Select = s => s.Energy },
        }));

        grid.Children.Add(MakePanel("Body temperature (°C)", autoScale: false, 25, 45, new List<Line>
        {
            new() { Name = "Temperature", Color = "#F6AD55", Select = s => s.Temperature },
        }));

        grid.Children.Add(MakePanel("Infection & blood (%)", autoScale: false, 0, 110, new List<Line>
        {
            new() { Name = "Infection load", Color = "#FC8181", Select = s => s.Infection },
            new() { Name = "Red cells", Color = "#63B3ED", Select = s => s.Rbc * 100 },
        }));

        Content = grid;
        Opened += (_, _) => { Refresh(); _timer.Start(); };
        Closed += (_, _) => _timer.Stop();
        _timer.Tick += (_, _) => Refresh();
    }

    private Control MakePanel(string title, bool autoScale, double min, double max, List<Line> lines)
    {
        var panel = new ChartPanel { Lines = lines };
        panel.Chart = new ChartControl { AutoScale = autoScale, Min = min, Max = max, MaxPoints = WindowPoints };
        foreach (var l in lines)
            panel.Chart.Series.Add(new ChartSeries(l.Name, Ui.Brush(l.Color)));
        _panels.Add(panel);

        var legend = new WrapPanel { Margin = new Thickness(0, 2, 0, 6) };
        foreach (var l in lines)
            legend.Children.Add(new TextBlock
            {
                Text = "● " + l.Name,
                FontSize = 11,
                Margin = new Thickness(0, 0, 14, 0),
                Foreground = Ui.Brush(l.Color),
            });

        var inner = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        var heading = new TextBlock { Text = title.ToUpperInvariant(), FontSize = 12, FontWeight = FontWeight.Bold, Foreground = Ui.Brush("#E1E4E8") };
        Grid.SetRow(legend, 1);
        Grid.SetRow(panel.Chart, 2);
        inner.Children.Add(heading);
        inner.Children.Add(legend);
        inner.Children.Add(panel.Chart);

        return new Border
        {
            Margin = new Thickness(6),
            Padding = new Thickness(10),
            Background = Ui.Brush("#1B222D"),
            BorderBrush = Ui.Brush("#2D3748"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = inner,
        };
    }

    private void Refresh()
    {
        var history = _sim.History;
        int start = Math.Max(0, history.Count - WindowPoints);
        foreach (var panel in _panels)
        {
            for (int i = 0; i < panel.Lines.Count; i++)
            {
                var values = panel.Chart.Series[i].Values;
                values.Clear();
                for (int k = start; k < history.Count; k++)
                    values.Add(panel.Lines[i].Select(history[k]));
            }
            panel.Chart.InvalidateVisual();
        }
    }
}

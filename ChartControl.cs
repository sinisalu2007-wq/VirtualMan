using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace VirtualMan;

/// <summary>Small cached-brush helper so the UI can update every frame without allocating.</summary>
internal static class Ui
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = new();

    public static SolidColorBrush Brush(string hex)
    {
        if (!Cache.TryGetValue(hex, out var b))
            Cache[hex] = b = new SolidColorBrush(Color.Parse(hex));
        return b;
    }

    public static string StatusColor(OrganStatus s) => s switch
    {
        OrganStatus.Nominal => "#68D391",
        OrganStatus.Degraded => "#ECC94B",
        OrganStatus.Critical => "#F6AD55",
        _ => "#FC8181",
    };

    public static string StateColor(string state) => state switch
    {
        "Nominal" => "#48BB78",
        "Degraded" => "#ECC94B",
        "Critical" => "#F6AD55",
        _ => "#FC8181",
    };

    public static string SeverityColor(Severity s) => s switch
    {
        Severity.Success => "#68D391",
        Severity.Warning => "#F6AD55",
        Severity.Fatal => "#FC8181",
        Severity.Action => "#63B3ED",
        _ => "#E1E4E8",
    };
}

public sealed class ChartSeries
{
    public string Name { get; }
    public IBrush Brush { get; }
    public List<double> Values { get; } = new();

    public ChartSeries(string name, IBrush brush) { Name = name; Brush = brush; }
}

/// <summary>Draws one or more line series. Used by the ECG monitor and the analytics window.</summary>
public sealed class ChartControl : Control
{
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.Parse("#2D3748")), 1);

    public List<ChartSeries> Series { get; } = new();
    public double Min { get; set; }
    public double Max { get; set; } = 100;
    public bool AutoScale { get; set; }
    public int GridLines { get; set; } = 4;
    /// <summary>Show only the newest N points (0 = all).</summary>
    public int MaxPoints { get; set; }
    public double LineThickness { get; set; } = 1.6;

    public ChartControl() { ClipToBounds = true; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 4 || h <= 4) return;

        for (int i = 0; i <= GridLines; i++)
        {
            double gy = h * i / GridLines;
            context.DrawLine(GridPen, new Point(0, gy), new Point(w, gy));
        }

        double min = Min, max = Max;
        if (AutoScale)
        {
            var all = Series.SelectMany(s => s.Values).ToList();
            if (all.Count > 0)
            {
                min = Math.Min(0, all.Min());
                max = Math.Max(min + 1, all.Max() * 1.1);
            }
        }
        double range = Math.Max(1e-9, max - min);

        foreach (var s in Series)
        {
            var vals = s.Values;
            int start = MaxPoints > 0 && vals.Count > MaxPoints ? vals.Count - MaxPoints : 0;
            int n = vals.Count - start;
            if (n < 2) continue;

            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                for (int i = 0; i < n; i++)
                {
                    double v = Math.Min(max, Math.Max(min, vals[start + i]));
                    var p = new Point(w * i / (n - 1), h - 4 - (v - min) / range * (h - 8));
                    if (i == 0) g.BeginFigure(p, false);
                    else g.LineTo(p);
                }
                g.EndFigure(false);
            }
            context.DrawGeometry(null, new Pen(s.Brush, LineThickness), geo);
        }
    }
}

/// <summary>Generates a Lead II style waveform whose rate follows the simulated heart rate.</summary>
public sealed class EcgGenerator
{
    public const int SampleRate = 100;
    private readonly Random _rng = new();
    private double _phase;
    private double _carry;

    public int Capacity { get; }
    public List<double> Buffer { get; } = new();

    public EcgGenerator(int capacity)
    {
        Capacity = capacity;
        for (int i = 0; i < capacity; i++) Buffer.Add(0);
    }

    public void Advance(double seconds, double heartRate, bool fibrillation = false)
    {
        _carry += seconds * SampleRate;
        int n = (int)_carry;
        _carry -= n;
        for (int i = 0; i < n; i++)
        {
            double v;
            if (fibrillation)
            {
                _phase += 0.05;                                   // chaotic, fast, irregular waves
                v = 0.35 * Math.Sin(_phase * 37) + 0.25 * Math.Sin(_phase * 71 + 1.3) + (_rng.NextDouble() - 0.5) * 0.25;
            }
            else if (heartRate <= 0)
            {
                v = (_rng.NextDouble() - 0.5) * 0.02;            // asystole: flat line + tiny noise
            }
            else
            {
                _phase += heartRate / 60.0 / SampleRate;
                _phase -= Math.Floor(_phase);
                v = Template(_phase) + (_rng.NextDouble() - 0.5) * 0.015;
            }
            Buffer.Add(v);
        }
        if (Buffer.Count > Capacity) Buffer.RemoveRange(0, Buffer.Count - Capacity);
    }

    private static double Gauss(double x, double mu, double sigma) =>
        Math.Exp(-Math.Pow((x - mu) / sigma, 2));

    /// <summary>One heartbeat (P, Q, R, S, T waves) over phase 0..1.</summary>
    private static double Template(double p) =>
          0.12 * Gauss(p, 0.18, 0.025)    // P
        - 0.12 * Gauss(p, 0.285, 0.012)   // Q
        + 1.00 * Gauss(p, 0.30, 0.012)    // R
        - 0.25 * Gauss(p, 0.315, 0.012)   // S
        + 0.25 * Gauss(p, 0.55, 0.05);    // T
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace VirtualMan;

public enum OrganStatus { Nominal, Degraded, Critical, Offline }
public enum Severity { Info, Success, Warning, Fatal, Action }

/// <summary>Function levels (% of normal) that decide an organ's status.</summary>
public static class Thresholds
{
    public const double NominalFrom = 80;
    public const double DegradedFrom = 40;
    public const double OfflineBelow = 10;

    public static OrganStatus Classify(double pct) =>
        pct >= NominalFrom ? OrganStatus.Nominal :
        pct >= DegradedFrom ? OrganStatus.Degraded :
        pct >= OfflineBelow ? OrganStatus.Critical :
        OrganStatus.Offline;
}

/// <summary>Reference values for a healthy adult at rest. Cite your textbook / WHO source in the report.</summary>
public static class Physiology
{
    public const double RestingHeartRate = 72;          // bpm
    public const double StrokeVolumeMl = 70;            // mL per beat
    public const double RestingRespRate = 16;           // breaths / min
    public const double TidalVolumeMl = 500;            // mL
    public const double NormalSpO2 = 98;                // %
    public const double SystolicBp = 120;               // mmHg
    public const double DiastolicBp = 80;               // mmHg
    public const double NormalGfr = 110;                // mL / min
    public const double NormalTemperature = 37.0;       // deg C
    public const double NormalPh = 7.40;
    public const double MaxHeartRate = 190;             // bpm
    public const double IschemiaThreshold = 0.6;        // O2 delivery ratio below which tissue is injured
    public const double ToxinProduction = 0.65;         // % / s at baseline metabolism
    public const double ToxinClearanceCapacity = 1.0;   // % / s at full liver + kidney + bowel function
    public const double RedCellTurnover = 1.0 / 600.0;  // fraction of red cells replaced per simulated second (accelerated)

    public static double NormalCardiacOutput => RestingHeartRate * StrokeVolumeMl / 1000.0; // L / min
}

/// <summary>"This organ needs Source": a share (Weight) of its function comes from the source organ.</summary>
public sealed class Link
{
    public string SourceName { get; }
    public double Weight { get; }
    public Organ? Source { get; set; }

    public Link(string sourceName, double weight) { SourceName = sourceName; Weight = weight; }
}

public sealed class Organ
{
    public string Name { get; }
    public string System { get; }
    public string Layer { get; }
    public bool Vital { get; }
    /// <summary>% of function lost per second when oxygen delivery is zero.</summary>
    public double IschemiaSensitivity { get; }
    public string FailureName { get; }
    public string FailureMessage { get; }

    /// <summary>Intrinsic health set by faults (0-100).</summary>
    public double Condition { get; set; } = 100;
    /// <summary>Accumulated damage from oxygen starvation and sepsis (0-100).</summary>
    public double Injury { get; set; }
    /// <summary>Functional loss caused by the blood chemistry (toxins, acid, heat, low glucose, anemia).</summary>
    public double Penalty { get; set; }
    /// <summary>Share of function still supplied by the organs this one needs (0-1).</summary>
    public double Support { get; set; } = 1;
    public double SupportTarget { get; set; } = 1;
    /// <summary>0 = dependencies act instantly; otherwise they act gradually over about this many seconds.</summary>
    public double SupportTau { get; private set; }
    /// <summary>Current effective function (0-100).</summary>
    public double Efficiency { get; set; } = 100;

    public List<Link> Links { get; } = new();
    /// <summary>Live readings shown next to the organ (for example HR, GFR, Toxins).</summary>
    public Dictionary<string, string> Metrics { get; } = new();
    public OrganStatus Status => Thresholds.Classify(Efficiency);

    public Organ(string name, string system, string layer, double ischemia, bool vital, string failureName, string failureMessage)
    {
        Name = name; System = system; Layer = layer; IschemiaSensitivity = ischemia;
        Vital = vital; FailureName = failureName; FailureMessage = failureMessage;
    }

    public Organ Needs(string source, double weight) { Links.Add(new Link(source, weight)); return this; }
    public Organ Slow(double seconds) { SupportTau = seconds; return this; }
}

public sealed class Parameter
{
    public string Name { get; }
    public string[] Options { get; }
    public double[] Values { get; }
    public int DefaultIndex { get; }
    public string Effect { get; }
    public int Index { get; set; }
    public double Value => Values[Index];

    public Parameter(string name, string[] options, double[] values, int defaultIndex, string effect)
    {
        Name = name; Options = options; Values = values; DefaultIndex = defaultIndex; Effect = effect;
        Index = defaultIndex;
    }
}

public sealed class Vitals
{
    public double HeartRate, Systolic, Diastolic, CardiacOutput, RespRate, TidalVolume,
                  SpO2, OxygenDelivery, Gfr, Toxins, Energy,
                  Temperature, Ph, Acid, Infection, Hemoglobin;
}

public sealed class SimEvent
{
    public DateTime Wall { get; init; }
    public double SimTime { get; init; }
    public Severity Severity { get; init; }
    public string Text { get; init; } = "";
}

public sealed record Scenario(string Category, string Name, string Description,
                              (string Organ, double Condition)[] Faults,
                              (string Param, int Index)[]? Params = null);

public sealed record ActionRecord(double Time, string Kind, string Name);

public sealed class Snapshot
{
    public double Time { get; set; }
    public double[] Condition { get; set; } = Array.Empty<double>();
    public double[] Injury { get; set; } = Array.Empty<double>();
    public double[] Support { get; set; } = Array.Empty<double>();
    public double[] Efficiency { get; set; } = Array.Empty<double>();
    public int[] ParameterIndexes { get; set; } = Array.Empty<int>();
    public double HeartRate { get; set; }
    public double RespRate { get; set; }
    public double SpO2 { get; set; }
    public double Systolic { get; set; }
    public double Diastolic { get; set; }
    public double CardiacOutput { get; set; }
    public double Gfr { get; set; }
    public double Toxins { get; set; }
    public double Energy { get; set; }
    public double OxygenDelivery { get; set; }
    public double Temperature { get; set; }
    public double Acid { get; set; }
    public double Infection { get; set; }
    public double Rbc { get; set; }
    public Dictionary<string, double> Drugs { get; set; } = new();
    public Dictionary<string, double> Diseases { get; set; } = new();
    public bool Vfib { get; set; }
    public bool Oxygen { get; set; }
    public bool Ventilator { get; set; }
    public bool Cpr { get; set; }
    public bool Dialysis { get; set; }
}

public sealed class SessionFile
{
    public string App { get; set; } = "BioSim";
    public string SavedAt { get; set; } = "";
    public string[] OrganNames { get; set; } = Array.Empty<string>();
    public Snapshot State { get; set; } = new();
}

public sealed class Simulation
{
    public const string ActivityParam = "Activity Level";
    public const string MetabolicParam = "Metabolic Rate";
    public const string HydrationParam = "Hydration";
    public const string EnvironmentParam = "Environment";

    /// <summary>Simulated seconds advanced by one manual STEP.</summary>
    public const double StepSeconds = 5.0;
    private const double SubStep = 0.5;

    public List<Organ> Organs { get; } = new();
    public List<Parameter> Parameters { get; } = new();
    public List<Snapshot> History { get; } = new();
    public Vitals Vitals { get; } = new();
    public double Time { get; private set; }
    public string Gait { get; private set; } = "Normal Nominal Gait";
    public string SystemState { get; private set; } = "Nominal";
    public bool IsScrubbed => _scrubbed;
    public event Action<SimEvent>? EventRaised;

    private static (string Organ, double Condition)[] F(params (string Organ, double Condition)[] faults) => faults;

    public static readonly Scenario[] Scenarios =
    {
        // heart, blood and vessels
        new("Heart & Blood", "Cardiac Arrest", "Heart stops pumping.", F(("Heart", 0))),
        new("Heart & Blood", "Heart Failure (weak pump)", "Heart works at 35%.", F(("Heart", 35))),
        new("Heart & Blood", "Hemorrhage (50% blood loss)", "Half of the blood volume is lost.", F(("Blood", 50))),
        new("Heart & Blood", "Spleen Rupture", "Spleen lost and 25% of blood lost.", F(("Spleen", 0), ("Blood", 75))),
        // breathing
        new("Breathing", "Respiratory Failure", "Lungs stop exchanging gas.", F(("Lungs", 0))),
        new("Breathing", "Airway Obstruction (choking)", "Voice box blocked.", F(("Larynx", 0))),
        new("Breathing", "Diaphragm Paralysis", "Main breathing muscle stops.", F(("Diaphragm", 0))),
        // digestion, liver, kidneys
        new("Digestion, Liver & Kidneys", "Renal Failure", "Kidneys stop filtering.", F(("Kidneys", 0))),
        new("Digestion, Liver & Kidneys", "Urinary Obstruction", "Urethra blocked; pressure backs up to the kidneys.", F(("Urethra", 0))),
        new("Digestion, Liver & Kidneys", "Hepatic Failure", "Liver stops detoxifying.", F(("Liver", 0))),
        new("Digestion, Liver & Kidneys", "Hepatorenal Syndrome", "Liver and kidneys fail together.", F(("Liver", 0), ("Kidneys", 0))),
        new("Digestion, Liver & Kidneys", "Biliary Obstruction", "Gallbladder and bile drainage fail.", F(("Gallbladder", 0))),
        new("Digestion, Liver & Kidneys", "Pancreatic Failure", "Pancreas stops making insulin and enzymes.", F(("Pancreas", 0))),
        new("Digestion, Liver & Kidneys", "Bowel Obstruction", "Large intestine and anus blocked.", F(("Large Intestine", 0), ("Anus", 0))),
        // brain, nerves, senses
        new("Brain, Nerves & Senses", "Stroke (cerebral)", "Cerebrum works at 30%.", F(("Cerebrum", 30))),
        new("Brain, Nerves & Senses", "Cerebellar Stroke", "Coordination centre lost.", F(("Cerebellum", 0))),
        new("Brain, Nerves & Senses", "Central Brain Death", "Brainstem goes offline.", F(("Brainstem", 0))),
        new("Brain, Nerves & Senses", "Spinal Cord Injury", "Cord severed: paralysis, no autonomic control.", F(("Spinal Cord", 0))),
        new("Brain, Nerves & Senses", "Peripheral Neuropathy", "Nerves work at 20%.", F(("Peripheral Nerves", 20))),
        new("Brain, Nerves & Senses", "Vestibular Loss", "Inner ear balance offline.", F(("Ear", 0))),
        new("Brain, Nerves & Senses", "Blindness", "Eyes offline.", F(("Eyes", 0))),
        // hormones and immunity
        new("Hormones & Immunity", "Hypothalamic Injury", "Control centre lost: pituitary, thyroid, adrenals and gonads follow.", F(("Hypothalamus", 0))),
        new("Hormones & Immunity", "Pituitary Failure", "Master gland lost: thyroid, adrenals and gonads follow.", F(("Pituitary", 0))),
        new("Hormones & Immunity", "Thyroid Failure", "Metabolism drops.", F(("Thyroid", 0))),
        new("Hormones & Immunity", "Adrenal Crisis", "No cortisol or aldosterone.", F(("Adrenal Glands", 0))),
        new("Hormones & Immunity", "Bone Marrow Failure", "No new blood cells: anemia and weak immunity.", F(("Bone Marrow", 0))),
        new("Hormones & Immunity", "Immune Collapse", "All immune organs nearly offline.", F(("Bone Marrow", 5), ("Spleen", 5), ("Thymus", 5), ("Lymph Nodes", 5))),
        // bones, muscles, skin
        new("Bones, Muscles & Skin", "Severe Burns", "Only 15% of the skin barrier remains.", F(("Skin", 15))),
        new("Bones, Muscles & Skin", "Pelvic Fracture", "Pelvis works at 15%.", F(("Pelvis", 15))),
        new("Bones, Muscles & Skin", "Muscle Failure", "Muscles work at 10%.", F(("Skeletal Muscles", 10))),
        new("Bones, Muscles & Skin", "Locomotor Failure", "Legs cannot bear weight.", F(("Legs", 0))),
        // environment
        new("Environment", "Heat Stroke", "Hot environment while temperature control is damaged.", F(("Hypothalamus", 30)), new[] { (EnvironmentParam, 3) }),
        new("Environment", "Hypothermia", "Cold environment with a damaged skin barrier.", F(("Skin", 40)), new[] { (EnvironmentParam, 0) }),
        new("Environment", "Dehydration Crisis", "Dehydrated with weakened kidneys.", F(("Kidneys", 60)), new[] { (HydrationParam, 0) }),
    };

    private readonly Dictionary<string, Organ> _byName = new();

    // slowly-changing physiological state (the "hubs" that link organs together)
    private double _hr, _rr, _spo2, _oxygenDelivery, _toxins, _acid, _temp, _infection, _rbc;
    private double _nextRecord = 1;
    private bool _scrubbed;
    private readonly Dictionary<string, OrganStatus> _lastStatus = new();
    private readonly Dictionary<string, bool> _alarms = new();
    private string _lastGait = "";
    private string _lastState = "";

    // treatment and disease state
    private readonly Dictionary<string, double> _drugs = new();       // medicine -> level
    private readonly Dictionary<string, double> _diseases = new();    // disease id -> severity (0-100)
    private readonly HashSet<string> _toxicFlags = new();
    private bool _vfib, _oxygenTherapy, _ventilator, _cpr, _dialysis;
    private double _vfibRefractory;

    public IReadOnlyDictionary<string, double> Drugs => _drugs;
    public IReadOnlyDictionary<string, double> Diseases => _diseases;
    public List<ActionRecord> Actions { get; } = new();
    public int AdverseEvents { get; private set; }
    public int WrongShocks { get; private set; }
    public bool IsFibrillating => _vfib;
    public bool CprOn => _cpr;
    /// <summary>Brain death or irreversible heart injury.</summary>
    public bool IsDead => O("Brainstem").Status == OrganStatus.Offline || O("Heart").Injury >= 95;

    public Simulation()
    {
        Organs.AddRange(Anatomy.Build());
        foreach (var o in Organs) _byName[o.Name] = o;

        foreach (var o in Organs)
            foreach (var l in o.Links)
            {
                if (!_byName.TryGetValue(l.SourceName, out var source))
                    throw new InvalidOperationException($"{o.Name} needs unknown organ '{l.SourceName}'.");
                l.Source = source;
            }

        foreach (var sc in Scenarios)
            foreach (var f in sc.Faults)
                if (!_byName.ContainsKey(f.Organ))
                    throw new InvalidOperationException($"Scenario '{sc.Name}' uses unknown organ '{f.Organ}'.");

        Parameters.Add(new Parameter(ActivityParam, new[] { "Resting", "Light", "Moderate", "Intense" },
            new[] { 1.0, 1.6, 2.6, 4.0 }, 0, "Raises oxygen demand, heart rate, breathing rate and body heat."));
        Parameters.Add(new Parameter(MetabolicParam, new[] { "Suppressed", "Normal", "Elevated" },
            new[] { 0.7, 1.0, 1.5 }, 1, "Scales oxygen demand and how fast toxins and acid are produced."));
        Parameters.Add(new Parameter(HydrationParam, new[] { "Dehydrated", "Normal", "Well hydrated" },
            new[] { 0.6, 1.0, 1.1 }, 1, "Dehydration lowers blood volume, stroke volume and kidney filtration (GFR)."));
        Parameters.Add(new Parameter(EnvironmentParam, new[] { "Cold (5°C)", "Cool (15°C)", "Comfortable (22°C)", "Hot (38°C)" },
            new[] { 5.0, 15.0, 22.0, 38.0 }, 2, "Cold or heat challenges the body's temperature control."));

        Reset(false);
    }

    // ------------------------------------------------------------------ public API

    public Organ Get(string name) => _byName[name];

    public void Raise(Severity severity, string text) =>
        EventRaised?.Invoke(new SimEvent { Wall = DateTime.Now, SimTime = Time, Severity = severity, Text = text });

    public void Advance(double dt)
    {
        if (dt <= 0) return;
        if (_scrubbed) TruncateAfterCurrent();

        double remaining = dt;
        while (remaining > 1e-9)
        {
            double d = Math.Min(SubStep, remaining);
            Time += d;
            Compute(d);
            remaining -= d;
            RecordHistory();
        }
        DetectEvents(false);
    }

    public void Step()
    {
        Advance(StepSeconds);
        Raise(Severity.Info, $"Tick processed (+{StepSeconds:0}s). Systemic State: {SystemState} | Gait: {Gait}");
    }

    public void Reset(bool log = true)
    {
        foreach (var o in Organs) { o.Condition = 100; o.Injury = 0; o.Penalty = 0; o.Support = 1; o.SupportTarget = 1; o.Efficiency = 100; }
        foreach (var p in Parameters) p.Index = p.DefaultIndex;
        _toxins = 0;
        _acid = 0;
        _infection = 0;
        _rbc = 1;
        _temp = Physiology.NormalTemperature;
        _hr = Physiology.RestingHeartRate;
        _rr = Physiology.RestingRespRate;
        _spo2 = Physiology.NormalSpO2;
        _oxygenDelivery = 1.0;
        Vitals.Energy = 100;
        _drugs.Clear(); _diseases.Clear(); _toxicFlags.Clear();
        _vfib = _oxygenTherapy = _ventilator = _cpr = _dialysis = false;
        _vfibRefractory = 0;
        Actions.Clear();
        AdverseEvents = 0; WrongShocks = 0;
        Time = 0;
        _scrubbed = false;
        _nextRecord = 1;
        History.Clear();
        Compute(0);
        History.Add(Capture(0));
        DetectEvents(true);
        if (log) Raise(Severity.Success, "[SYSTEM]: All body systems and organs fully restored to baseline.");
    }

    public void SetCondition(Organ o, double pct)
    {
        if (_scrubbed) TruncateAfterCurrent();
        o.Condition = Math.Clamp(pct, 0, 100);
        Raise(Severity.Action, pct <= 0
            ? $"[USER]: Fault injected — {o.Name} forced offline (0% function)."
            : $"[USER]: Fault injected — {o.Name} degraded to {pct:0}% function.");
        Refresh();
    }

    public void RestoreOrgan(Organ o)
    {
        if (_scrubbed) TruncateAfterCurrent();
        o.Condition = 100; o.Injury = 0;
        Raise(Severity.Action, $"[USER]: {o.Name} restored to full function.");
        Refresh();
    }

    public void RestoreAll()
    {
        if (_scrubbed) TruncateAfterCurrent();
        foreach (var o in Organs) { o.Condition = 100; o.Injury = 0; }
        _toxins = 0; _acid = 0; _infection = 0; _rbc = 1;
        _temp = Physiology.NormalTemperature;
        Raise(Severity.Action, "[USER]: All organs restored to full function.");
        Refresh();
    }

    public void ApplyScenario(Scenario s)
    {
        if (_scrubbed) TruncateAfterCurrent();
        foreach (var (organ, condition) in s.Faults) Get(organ).Condition = condition;
        if (s.Params != null)
            foreach (var (name, index) in s.Params)
            {
                var p = Param(name);
                p.Index = Math.Clamp(index, 0, p.Options.Length - 1);
            }
        Raise(Severity.Action, $"[USER]: Scenario started — {s.Name}: {s.Description}");
        Refresh();
    }

    public void InjectRandomFault()
    {
        var o = Organs[Random.Shared.Next(Organs.Count)];
        double[] levels = { 0, 25, 50 };
        SetCondition(o, levels[Random.Shared.Next(levels.Length)]);
    }

    public void SetParameter(Parameter p, int index)
    {
        if (index < 0 || index >= p.Options.Length || index == p.Index) return;
        if (_scrubbed) TruncateAfterCurrent();
        p.Index = index;
        Raise(Severity.Action, $"[USER]: {p.Name} set to {p.Options[index]}.");
        Refresh();
    }

    // ------------------------------------------------------------------ medicine, procedures, diseases

    public double DiseaseSeverity(string id) => _diseases.TryGetValue(id, out var s) ? s : 0;

    public void ResetCounters() { AdverseEvents = 0; WrongShocks = 0; Actions.Clear(); }

    public void InjectDisease(DiseaseDef d, double? severity = null, bool silent = false)
    {
        if (_scrubbed) TruncateAfterCurrent();
        if (_diseases.ContainsKey(d.Id)) return;
        _diseases[d.Id] = Math.Clamp(severity ?? d.Start, 1, 100);
        if (!silent) Raise(Severity.Warning, $"[DISEASE]: {d.Name} — onset. Symptoms: {d.Symptoms}");
        Refresh();
    }

    public void CureDisease(string id)
    {
        if (_scrubbed) TruncateAfterCurrent();
        if (_diseases.Remove(id)) Raise(Severity.Action, $"[USER]: {Pathology.Find(id)?.Name ?? id} cleared.");
        Refresh();
    }

    public void ClearDiseases()
    {
        if (_scrubbed) TruncateAfterCurrent();
        _diseases.Clear();
        Raise(Severity.Action, "[USER]: All diseases cleared.");
        Refresh();
    }

    public void GiveMedicine(Medicine m)
    {
        if (_scrubbed) TruncateAfterCurrent();
        double level = Math.Min(4.0, Lv(m.Name) + 1.0);
        _drugs[m.Name] = level;
        Actions.Add(new ActionRecord(Time, "Medicine", m.Name));
        Raise(Severity.Action, $"[RX]: {m.Name} {m.Dose} ({m.Route}) given.");
        if (level > m.Toxic && _toxicFlags.Add(m.Name))
        {
            AdverseEvents++;
            Raise(Severity.Warning, $"[ADVERSE]: {m.Name} is now above the safe level — {m.Warning}");
        }
        Refresh();
    }

    public bool IsOn(string procedure) => procedure switch
    {
        Rx.Oxygen => _oxygenTherapy,
        Rx.Ventilator => _ventilator,
        Rx.Cpr => _cpr,
        Rx.Dialysis => _dialysis,
        _ => false,
    };

    public void SetProcedure(string procedure, bool on)
    {
        if (_scrubbed) TruncateAfterCurrent();
        if (IsOn(procedure) == on) return;
        switch (procedure)
        {
            case Rx.Oxygen: _oxygenTherapy = on; break;
            case Rx.Ventilator: _ventilator = on; break;
            case Rx.Cpr: _cpr = on; break;
            case Rx.Dialysis: _dialysis = on; break;
            default: return;
        }
        if (on) Actions.Add(new ActionRecord(Time, "Procedure", procedure));
        Raise(Severity.Action, on ? $"[PROCEDURE]: {procedure} started." : $"[PROCEDURE]: {procedure} stopped.");
        Refresh();
    }

    public void Defibrillate()
    {
        if (_scrubbed) TruncateAfterCurrent();
        Actions.Add(new ActionRecord(Time, "Procedure", Rx.Defib));
        if (_vfib)
        {
            if (O("Heart").Condition - O("Heart").Injury >= 30)
            {
                _vfib = false;
                _vfibRefractory = 120;     // a restored rhythm is stable for a while, so there is time to treat the cause
                Raise(Severity.Success, "[DEFIB]: Shock delivered — sinus rhythm restored.");
            }
            else Raise(Severity.Warning, "[DEFIB]: Shock delivered but the heart muscle is too damaged — rhythm persists.");
        }
        else
        {
            WrongShocks++;
            O("Heart").Injury = Math.Min(100, O("Heart").Injury + 3);
            Raise(Severity.Warning, "[DEFIB]: No shockable rhythm — shock not indicated and it injured the heart.");
        }
        Refresh();
    }

    public void Transfuse()
    {
        if (_scrubbed) TruncateAfterCurrent();
        O("Blood").Condition = Math.Min(100, O("Blood").Condition + 25);
        _rbc = Math.Min(1.2, _rbc + 0.2);
        Actions.Add(new ActionRecord(Time, "Procedure", Rx.Transfusion));
        Raise(Severity.Action, "[PROCEDURE]: One unit of blood transfused.");
        Refresh();
    }

    /// <summary>Takes simulated time, then reports what the test finds in the patient right now.</summary>
    public string OrderTest(Investigation test)
    {
        Actions.Add(new ActionRecord(Time, "Test", test.Name));
        Advance(test.Seconds);
        string result = test.Run(this);
        Raise(Severity.Info, $"[LAB] {test.Name}: {result}");
        return result;
    }

    /// <summary>Recalculate everything for the current instant (no time passes).</summary>
    public void Refresh()
    {
        Compute(0);
        DetectEvents(false);
    }

    /// <summary>Jump back to a recorded moment without logging (timeline scrubbing).</summary>
    public void RestoreSnapshot(int index)
    {
        if (History.Count == 0) return;
        index = Math.Clamp(index, 0, History.Count - 1);
        ApplySnapshot(History[index], null);
        Time = History[index].Time;
        _scrubbed = true;
        Compute(0);
        DetectEvents(true);
    }

    // ------------------------------------------------------------------ session files

    public string ExportSession() => JsonSerializer.Serialize(new SessionFile
    {
        SavedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
        OrganNames = Organs.Select(o => o.Name).ToArray(),
        State = Capture(Time),
    }, new JsonSerializerOptions { WriteIndented = true });

    public bool ImportSession(string json, out string error)
    {
        error = "";
        try
        {
            var file = JsonSerializer.Deserialize<SessionFile>(json);
            if (file?.State == null || file.OrganNames.Length == 0) { error = "not a BioSim session file"; return false; }
            ApplySnapshot(file.State, file.OrganNames);
            Time = file.State.Time;
            _scrubbed = false;
            History.Clear();
            Compute(0);
            History.Add(Capture(Time));
            _nextRecord = Math.Floor(Time) + 1;
            DetectEvents(true);
            Raise(Severity.Success, $"[FILE]: Session loaded (simulation time {FormatTime(Time)}).");
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }

    // ------------------------------------------------------------------ reports

    /// <summary>One-organ description: what it needs, what needs it, and what happens if it fails.</summary>
    public string Describe(Organ o)
    {
        var sb = new StringBuilder();
        sb.Append(o.Name).Append(o.Vital ? "  [vital]" : "").Append("  —  ").AppendLine(o.System);
        if (o.Links.Count > 0)
            sb.AppendLine("Needs: " + string.Join(", ", o.Links.Select(l => $"{l.SourceName} {l.Weight * 100:0}%")));
        var feeds = Organs.Where(x => x.Links.Any(l => l.SourceName == o.Name)).Select(x => x.Name).ToList();
        if (feeds.Count > 0) sb.AppendLine("Supports: " + string.Join(", ", feeds));
        sb.Append("If it fails: ").Append(o.FailureName).Append(" — ").Append(o.FailureMessage);
        return sb.ToString();
    }

    public string BuildDependencyMap()
    {
        var sb = new StringBuilder();
        sb.AppendLine("ORGAN DEPENDENCY MAP");
        sb.AppendLine("'Needs' = organs this organ gets part of its function from.");
        sb.AppendLine("'Supports' = organs that lose function when this organ fails.");
        foreach (var g in Organs.GroupBy(o => o.System))
        {
            sb.AppendLine();
            sb.AppendLine(g.Key.ToUpperInvariant());
            foreach (var o in g)
            {
                sb.AppendLine($"  {o.Name}{(o.Vital ? "  [vital]" : "")}");
                if (o.Links.Count > 0)
                    sb.AppendLine("      needs    : " + string.Join(", ", o.Links.Select(l => $"{l.SourceName} ({l.Weight * 100:0}%)")));
                var feeds = Organs.Where(x => x.Links.Any(l => l.SourceName == o.Name)).Select(x => x.Name).ToList();
                if (feeds.Count > 0)
                    sb.AppendLine("      supports : " + string.Join(", ", feeds));
                sb.AppendLine($"      if failed: {o.FailureName} — {o.FailureMessage}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("WHOLE-BODY COUPLINGS (recalculated every tick)");
        sb.AppendLine("  Heart + Blood + Veins + Spleen + Skin     -> stroke volume -> cardiac output");
        sb.AppendLine("  Arteries + Spinal Cord + Adrenals + sepsis -> vascular tone -> blood pressure");
        sb.AppendLine("  Brainstem + Spinal Cord + Adrenals        -> heart-rate compensation when oxygen delivery falls");
        sb.AppendLine("  Larynx + Trachea + Nose + Bronchi         -> airflow");
        sb.AppendLine("  Diaphragm + Muscles + Rib Cage + Brainstem -> breathing depth and rate -> ventilation");
        sb.AppendLine("  Ventilation x Lungs                       -> SpO2");
        sb.AppendLine("  Cardiac output x Capillaries x SpO2 x Blood -> OXYGEN DELIVERY (vs. demand)");
        sb.AppendLine("  Oxygen delivery < 60% of demand           -> EVERY organ slowly loses function (brainstem fastest)");
        sb.AppendLine("  Thyroid + temperature + metabolic setting -> whole-body metabolic rate");
        sb.AppendLine("  Liver + Kidneys (+ Gallbladder, bowel)    -> clear TOXINS; toxins poison brain, ear and nerves");
        sb.AppendLine("  Kidneys + Liver + Lungs vs. lactate + CO2 -> ACID level (pH); acid weakens heart, brain and muscles");
        sb.AppendLine("  Hypothalamus + Skin + Muscles + environment + infection -> BODY TEMPERATURE");
        sb.AppendLine("  Bone Marrow + Spleen + Thymus + Lymph Nodes -> immunity; Skin / gut / lung / urinary barriers let infection in");
        sb.AppendLine("  Infection > 30%                           -> sepsis: low blood pressure, fever, organ injury");
        sb.AppendLine("  Bone Marrow + Kidney (EPO)                -> red blood cells -> oxygen-carrying capacity");
        sb.AppendLine("  Pancreas + digestive organs + Liver       -> ENERGY; low energy hurts brain and muscles");
        return sb.ToString();
    }

    public string BuildReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"BioSim organ summary — simulation time {FormatTime(Time)}");
        foreach (var g in Organs.GroupBy(o => o.System))
        {
            sb.AppendLine(new string('-', 84));
            sb.AppendLine(g.Key.ToUpperInvariant());
            sb.AppendLine($"  {"Organ",-18}{"Function",9}  {"Status",-10}{"Fault",7}{"Injury",8}{"Support",9}");
            foreach (var o in g)
                sb.AppendLine($"  {o.Name,-18}{o.Efficiency,8:0}%  {o.Status,-10}{100 - o.Condition,6:0}%{o.Injury,7:0}%{o.Support * 100,8:0}%");
        }
        sb.AppendLine(new string('-', 84));
        var v = Vitals;
        sb.AppendLine($"Heart rate      : {v.HeartRate:0} bpm");
        sb.AppendLine($"Blood pressure  : {v.Systolic:0}/{v.Diastolic:0} mmHg");
        sb.AppendLine($"Cardiac output  : {v.CardiacOutput:0.0} L/min");
        sb.AppendLine($"Respiratory rate: {v.RespRate:0} /min   Tidal volume: {v.TidalVolume:0} mL");
        sb.AppendLine($"SpO2            : {v.SpO2:0} %");
        sb.AppendLine($"O2 delivery     : {v.OxygenDelivery * 100:0} % of demand");
        sb.AppendLine($"Hemoglobin      : {v.Hemoglobin:0} % of normal red-cell mass");
        sb.AppendLine($"Temperature     : {v.Temperature:0.0} °C");
        sb.AppendLine($"Blood pH        : {v.Ph:0.00}");
        sb.AppendLine($"GFR             : {v.Gfr:0} mL/min");
        sb.AppendLine($"Blood toxins    : {v.Toxins:0} %");
        sb.AppendLine($"Infection load  : {v.Infection:0} %");
        sb.AppendLine($"Energy supply   : {v.Energy:0} %");
        sb.AppendLine();
        sb.AppendLine($"Systemic state  : {SystemState}");
        sb.AppendLine($"Gait            : {Gait}");
        sb.AppendLine("Active diseases : " + (_diseases.Count == 0 ? "none" : string.Join(", ", _diseases.Select(d => $"{Pathology.Find(d.Key)?.Name} {d.Value:0}%"))));
        sb.AppendLine("Active medicines: " + (_drugs.Count == 0 ? "none" : string.Join(", ", _drugs.Select(d => $"{d.Key} {d.Value:0.0}"))));
        sb.AppendLine();
        sb.AppendLine("Parameters: " + string.Join(" | ", Parameters.Select(p => $"{p.Name}: {p.Options[p.Index]}")));
        return sb.ToString();
    }

    public string BuildCsv()
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append("time_s,heart_rate_bpm,systolic_mmHg,diastolic_mmHg,spo2_pct,resp_rate_per_min,cardiac_output_L_min,gfr_mL_min,toxins_pct,energy_pct,o2_delivery_pct,temperature_C,acid_pct,infection_pct,red_cells_pct");
        foreach (var o in Organs) sb.Append(',').Append(o.Name.ToLowerInvariant().Replace(' ', '_')).Append("_pct");
        sb.AppendLine();
        foreach (var s in History)
        {
            sb.Append(string.Join(',', new[]
            {
                s.Time, s.HeartRate, s.Systolic, s.Diastolic, s.SpO2, s.RespRate, s.CardiacOutput,
                s.Gfr, s.Toxins, s.Energy, s.OxygenDelivery * 100, s.Temperature, s.Acid, s.Infection, s.Rbc * 100
            }.Select(x => x.ToString("0.##", inv))));
            foreach (var e in s.Efficiency) sb.Append(',').Append(e.ToString("0.##", inv));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string FormatTime(double seconds)
    {
        long t = (long)Math.Max(0, seconds);
        return $"{t / 3600:00}:{t % 3600 / 60:00}:{t % 60:00}";
    }

    // ------------------------------------------------------------------ physiology

    private Parameter Param(string name) => Parameters.First(p => p.Name == name);
    private Organ O(string name) => _byName[name];
    private double E(string name) => _byName[name].Efficiency / 100.0;
    private static double Clamp(double v, double lo, double hi) => Math.Min(hi, Math.Max(lo, v));

    private double Lv(string drug) => _drugs.TryGetValue(drug, out var l) ? l : 0;                 // medicine level (0-4)
    private double Th(string drug) => Math.Min(Lv(drug), 1.0);                                      // therapeutic strength 0..1
    private double Over(string drug)                                                                 // amount above the safe level
    {
        var m = Pharmacy.Find(drug);
        return m == null ? 0 : Math.Max(0, Lv(drug) - m.Toxic);
    }
    private double Sev(string diseaseId) => _diseases.TryGetValue(diseaseId, out var s) ? s / 100.0 : 0;   // 0..1

    private static double BpScale(double ratio, double highGain, double lowExponent) =>
        ratio >= 1 ? 1 + (Math.Min(ratio, 2.5) - 1) * highGain : Math.Pow(Math.Max(ratio, 0), lowExponent);

    private double ImmuneStrength()
    {
        double immune = 0.40 * E("Bone Marrow") + 0.20 * E("Spleen") + 0.20 * E("Thymus") + 0.20 * E("Lymph Nodes");
        return immune * (1 - 0.25 * Th(Rx.Hydrocortisone));      // steroids suppress immunity
    }

    // ---- diseases -------------------------------------------------------------------------

    private sealed class DiseaseFx
    {
        public double Infection, RbcLoss, Acid, Toxin, Fluid, Obstruction, Hyper;
        public double Vaso = 1, Energy = 1;
        public readonly Dictionary<string, double> Penalty = new();
        public readonly Dictionary<string, double> Injury = new();
    }

    private DiseaseFx _fx = new();

    private DiseaseFx AggregateDiseases()
    {
        var fx = new DiseaseFx();
        double open = 1;
        foreach (var (id, severity) in _diseases)
        {
            var d = Pathology.Find(id);
            if (d == null) continue;
            double f = severity / 100.0;
            fx.Infection += d.InfectionLoad * f;
            fx.RbcLoss += d.RbcLoss * f;
            fx.Acid += d.AcidLoad * f;
            fx.Toxin += d.ToxinLoad * f;
            fx.Fluid += d.FluidLoss * f;
            fx.Hyper += d.Hypertension * f;
            fx.Vaso *= 1 - d.Vasodilation * f;
            fx.Energy *= 1 - d.EnergyLoss * f;
            open *= 1 - d.Obstruction * f;
            foreach (var (organ, penalty, injury) in d.Impacts)
            {
                fx.Penalty[organ] = fx.Penalty.GetValueOrDefault(organ) + penalty * f;
                fx.Injury[organ] = fx.Injury.GetValueOrDefault(organ) + injury * f;
            }
        }
        fx.Obstruction = 1 - open;
        return fx;
    }

    /// <summary>Diseases grow by themselves and are pushed back by the right medicines.</summary>
    private void UpdateDiseases(double dt)
    {
        if (_diseases.Count == 0) return;
        foreach (var id in _diseases.Keys.ToList())
        {
            var d = Pathology.Find(id);
            if (d == null) { _diseases.Remove(id); continue; }
            double s = _diseases[id];
            if (dt > 0)
            {
                double growth = d.Growth * s * (1 - s / 100.0);
                if (d.Infectious) growth *= Clamp(1.25 - ImmuneStrength(), 0.25, 1.25);   // a strong immune system slows it
                double cure = 0;
                foreach (var (drug, rate) in d.Cures) cure += rate * Math.Min(Lv(drug), 1.5);
                if (id == "aki" && _dialysis) cure += 0.2;
                s = Clamp(s + (growth - cure) * dt, 0, 100);
            }
            if (s < 0.5)
            {
                _diseases.Remove(id);
                Raise(Severity.Success, $"[DISEASE]: {d.Name} resolved. Any permanent organ injury remains.");
            }
            else _diseases[id] = s;
        }
    }

    private void UpdateDrugs(double dt)
    {
        foreach (var name in _drugs.Keys.ToList())
        {
            var m = Pharmacy.Find(name);
            if (m == null) { _drugs.Remove(name); continue; }
            double level = _drugs[name];
            if (dt > 0) level *= Math.Exp(-dt / m.Tau);
            if (level < 0.02) { _drugs.Remove(name); _toxicFlags.Remove(name); continue; }
            _drugs[name] = level;
            if (level <= m.Toxic) _toxicFlags.Remove(name);
        }
    }

    // ---- blood chemistry --------------------------------------------------------------------

    private static readonly string[] BrainTissue = { "Cerebrum", "Cerebellum", "Brainstem" };
    private static readonly string[] MuscleTissue = { "Skeletal Muscles", "Diaphragm" };

    /// <summary>The blood chemistry, diseases and drug side effects from the previous moment damage the organs.</summary>
    private void ApplyHubPenalties()
    {
        foreach (var o in Organs) o.Penalty = 0;

        double toxin = Math.Max(0, _toxins - 30) * 0.8;
        double acid = Math.Max(0, _acid - 25);
        double heat = Math.Max(0, _temp - 40) * 12;
        double cold = Math.Max(0, 30 - _temp) * 6;
        double lowGlucose = Math.Max(0, 35 - Vitals.Energy);

        foreach (var n in BrainTissue)
            O(n).Penalty += toxin + acid * 0.5 + heat + lowGlucose * 0.8;
        foreach (var n in MuscleTissue)
            O(n).Penalty += acid * 0.3 + lowGlucose * 0.5;
        O("Heart").Penalty += acid * 0.7 + heat * 0.5 + cold;
        O("Ear").Penalty += toxin;
        O("Peripheral Nerves").Penalty += toxin * 0.5;
        O("Blood").Penalty += (1 - Math.Min(_rbc, 1.0)) * 100;          // anemia

        // diseases: reversible loss of function while the disease is active
        foreach (var (organ, pen) in _fx.Penalty) O(organ).Penalty += pen;

        // medicine side effects
        double overload = Math.Max(0, Lv(Rx.Saline) - 1.5) * Math.Max(0, 1.2 - E("Heart")) * (1 - 0.6 * Th(Rx.Furosemide));
        O("Lungs").Penalty += overload * 20;                                       // fluid overload floods the lungs
        O("Stomach").Penalty += Over(Rx.Aspirin) * 15;
        O("Kidneys").Penalty += Over(Rx.Gentamicin) * 30 + Over(Rx.Noradrenaline) * 20;
        O("Ear").Penalty += Over(Rx.Gentamicin) * 30;
        O("Liver").Penalty += Over(Rx.AntiTb) * 15;
        O("Heart").Penalty += Over(Rx.Adrenaline) * 30 + Over(Rx.Salbutamol) * 10;
    }

    /// <summary>Function = (condition - injury - penalty) x support from the organs it needs.</summary>
    private void UpdateEfficiencies(double dt)
    {
        // several passes so a failure can travel down a chain of dependencies within one tick
        for (int pass = 0; pass < 8; pass++)
        {
            foreach (var o in Organs)
            {
                double target = 1.0;
                foreach (var l in o.Links) target -= l.Weight * (1 - l.Source!.Efficiency / 100.0);
                target = Clamp(target, 0, 1);
                o.SupportTarget = target;
                double support = o.SupportTau > 0 ? o.Support : target;
                o.Efficiency = Clamp(o.Condition - o.Injury - o.Penalty, 0, 100) * support;
            }
        }
        foreach (var o in Organs)
        {
            if (o.SupportTau > 0)
            {
                if (dt > 0) o.Support += (o.SupportTarget - o.Support) * (1 - Math.Exp(-dt / o.SupportTau));
            }
            else o.Support = o.SupportTarget;
        }
    }

    private void Compute(double dt)
    {
        UpdateDiseases(dt);
        _fx = AggregateDiseases();
        UpdateDrugs(dt);
        ApplyHubPenalties();
        UpdateEfficiencies(dt);

        double activity = Param(ActivityParam).Value;
        double metabolicSetting = Param(MetabolicParam).Value;
        double hydration = Param(HydrationParam).Value;
        double ambient = Param(EnvironmentParam).Value;
        double offline = Thresholds.OfflineBelow / 100.0;

        double adr = Th(Rx.Adrenaline), nor = Th(Rx.Noradrenaline), bb = Th(Rx.Metoprolol), sal = Th(Rx.Salbutamol);

        // ---- hormones set the whole-body metabolic rate; fever speeds it up, cold slows it down
        double thyroid = Math.Max(E("Thyroid"), 0.8 * Th(Rx.Levothyroxine));
        double adrenal = Math.Max(E("Adrenal Glands"), 0.7 * Th(Rx.Hydrocortisone));
        double metabolic = metabolicSetting * (0.5 + 0.5 * thyroid) * (1 + 0.08 * (_temp - Physiology.NormalTemperature));
        double demand = Math.Max(0.2, activity * metabolic * (1 - 0.1 * bb));    // oxygen demand multiplier

        // ---- nervous control
        double heartEff = E("Heart"), brainEff = E("Brainstem"), spinal = E("Spinal Cord");
        bool heartAlive = heartEff >= offline;
        bool brainDrive = brainEff >= offline;                             // brainstem drives heart rate and breathing
        double autonomic = brainDrive ? 0.5 + 0.5 * spinal : 0;            // sympathetic outflow runs down the spinal cord

        // ---- ventricular fibrillation: a severe heart attack or deep acidosis makes the heart quiver instead of pump
        if (dt > 0 && _vfibRefractory > 0) _vfibRefractory -= dt;
        if (dt > 0 && !_vfib && _vfibRefractory <= 0 && heartAlive && (Sev("mi") >= 0.85 || _acid >= 70))
        {
            _vfib = true;
            Raise(Severity.Fatal, "[ECG]: Ventricular fibrillation — no effective pulse. Start CPR and defibrillate!");
        }
        bool pumping = heartAlive && !_vfib;

        // ---- heart rate compensates for poor oxygen delivery (needs brainstem, spinal cord and adrenals)
        double compensation = Clamp((1 - _oxygenDelivery) * 1.2 * (0.4 + 0.6 * adrenal) * autonomic, 0, 0.8);
        double targetHr = brainDrive
            ? Physiology.RestingHeartRate * (1 + (demand - 1) * 0.55) * (1 + compensation)
            : Physiology.RestingHeartRate * 0.8;                           // intrinsic pacemaker only
        targetHr *= (1 + 0.5 * adr) * (1 + 0.15 * sal) * (1 - 0.3 * bb);
        targetHr *= 1 - Clamp((36 - _temp) * 0.06, 0, 0.7);                // cold slows the heart
        targetHr = Math.Min(targetHr, Physiology.MaxHeartRate);

        double mechanicalFlow = 0;                                         // share of normal output made by chest compressions
        if (pumping) _hr += (targetHr - _hr) * (1 - Math.Exp(-dt / 3.0));
        else if (_cpr) { _hr = 100; mechanicalFlow = 0.30; }
        else _hr = 0;
        if (dt > 0 && _cpr && pumping)                                     // compressions on a beating heart hurt
            O("Rib Cage").Injury = Math.Min(100, O("Rib Cage").Injury + 0.5 * dt);

        // ---- stroke volume: heart strength x filling (blood volume, veins, spleen reservoir, fluid loss, fluids given)
        double bloodEff = E("Blood");
        double fluids = Th(Rx.Saline) + 0.6 * Th(Rx.Ors);
        double preload = Math.Min(1.0, 0.55 + 0.45 * hydration)
                         * (0.35 + 0.65 * bloodEff)
                         * (0.7 + 0.3 * E("Veins"))
                         * (0.85 + 0.15 * E("Spleen"))
                         * (0.75 + 0.25 * E("Skin"));
        preload *= 1 + 0.25 * fluids;
        preload *= 1 - 0.25 * Th(Rx.Furosemide);
        preload *= 1 - 0.5 * Math.Min(1.0, _fx.Fluid);                    // diarrhoea / vomiting
        if (_dialysis) preload *= 0.92;
        preload = Clamp(preload, 0, 1.25);
        double strokeVolume = Physiology.StrokeVolumeMl * (pumping ? heartEff : mechanicalFlow) * preload;
        double co = _hr * strokeVolume / 1000.0;
        double coRatio = co / Physiology.NormalCardiacOutput;

        // ---- blood pressure: output x vessel tone (sepsis and allergy dilate vessels; vasopressors tighten them)
        double sepsis = Clamp((_infection - 30) / 70.0, 0, 1);
        double vascTone = E("Arteries") * (0.5 + 0.5 * spinal) * (0.7 + 0.3 * adrenal)
                          * (1 - 0.5 * sepsis) * _fx.Vaso * (1 + _fx.Hyper);
        vascTone += 0.5 * adr + 0.6 * nor;
        vascTone *= 1 - 0.3 * Th(Rx.Amlodipine);
        vascTone = Clamp(vascTone, 0, 1.6);

        // ---- breathing: airway x pump mechanics x brainstem drive
        double lungEff = E("Lungs");
        double obstruction = Clamp(_fx.Obstruction * (1 - 0.9 * adr - 0.9 * sal - 0.3 * Th(Rx.Hydrocortisone)), 0, 1);
        if (_diseases.ContainsKey("asthma")) obstruction = Clamp(obstruction + 0.4 * bb, 0, 1);   // beta-blockers tighten asthmatic airways
        double airway = Math.Min(E("Larynx"), E("Trachea")) * (0.5 + 0.5 * E("Nose")) * (0.7 + 0.3 * E("Bronchi")) * (1 - obstruction);
        double mechanics = (0.7 * E("Diaphragm") + 0.3 * E("Skeletal Muscles")) * (0.6 + 0.4 * E("Rib Cage"));
        double ventDemand = 1 + (demand - 1) * 0.5;
        double hypoxicDrive = brainDrive ? Clamp((1 - _oxygenDelivery) * 0.8, 0, 0.6) : 0;
        double acidDrive = brainDrive ? Clamp(_acid / 100.0 * 0.8, 0, 0.6) : 0;   // acidosis makes us breathe faster
        double targetRr = brainDrive ? Physiology.RestingRespRate * ventDemand * (1 + hypoxicDrive + acidDrive) : 0;
        if (!brainDrive) _rr = 0;
        else _rr += (targetRr - _rr) * (1 - Math.Exp(-dt / 2.0));

        double tidal = Physiology.TidalVolumeMl * airway * (0.1 + 0.9 * mechanics);
        if (_ventilator) { _rr = 14; tidal = Physiology.TidalVolumeMl * 0.9; }          // the machine breathes for the patient
        double ventRatio = _rr * tidal / (Physiology.RestingRespRate * Physiology.TidalVolumeMl * ventDemand);
        if (_ventilator) ventRatio = Math.Max(ventRatio, 0.95);
        double oxygenation = Math.Min(1.0, Math.Max(0, ventRatio)) * Math.Pow(lungEff, 0.8);
        if (_oxygenTherapy) oxygenation = Math.Min(1.0, oxygenation + 0.35 * Math.Min(1.0, ventRatio));
        double spo2Target = Physiology.NormalSpO2 * (0.45 + 0.55 * Math.Pow(oxygenation, 0.35));
        _spo2 += (spo2Target - _spo2) * (1 - Math.Exp(-dt / 20.0));
        double ventDeficit = Clamp(1 - ventRatio, 0, 1);                   // CO2 is retained when ventilation is short

        // ---- oxygen delivery = flow x capillary exchange x saturation x blood capacity, relative to demand
        double perfusionQuality = 0.4 + 0.6 * E("Capillaries");
        double oxTarget = Clamp(coRatio * perfusionQuality * (_spo2 / Physiology.NormalSpO2) * (0.2 + 0.8 * bloodEff)
                                / Math.Pow(demand, 0.7), 0, 1.2);
        _oxygenDelivery += (oxTarget - _oxygenDelivery) * (1 - Math.Exp(-dt / 2.0));

        // ---- infection: barriers let it in, the immune system and antibiotics remove it
        double immune = ImmuneStrength();
        double giBarrier = (E("Stomach") + E("Small Intestine") + E("Large Intestine")) / 3.0;
        double respBarrier = (E("Nose") + E("Bronchi") + lungEff) / 3.0;
        double urinaryBarrier = (E("Bladder") + E("Urethra")) / 2.0;
        double exposure = 0.10 + 0.60 * (1 - E("Skin")) + 0.15 * (1 - giBarrier) + 0.10 * (1 - respBarrier) + 0.10 * (1 - urinaryBarrier);
        double antibiotic = 0.6 * Th(Rx.Amoxicillin) + 0.9 * Th(Rx.Ceftriaxone) + 0.8 * Th(Rx.Gentamicin);
        if (dt > 0)
            _infection = Clamp(_infection + (exposure - 0.5 * immune - 0.6 * antibiotic + 0.02 * _infection * (1 - immune)) * dt, 0, 100);
        _infection = Math.Max(_infection, Math.Min(100, _fx.Infection * 100));    // an infectious disease keeps the load up

        // ---- body temperature: regulated by hypothalamus, skin and muscles against environment, exertion and fever
        double regulation = 0.5 * E("Hypothalamus") + 0.3 * E("Skin") + 0.2 * E("Skeletal Muscles");
        double fever = Math.Min(4.0, Math.Max(0, _infection - 10) * 0.05);
        fever = Math.Max(0, fever - 2.0 * Th(Rx.Paracetamol));                    // antipyretic
        double tempTarget = Physiology.NormalTemperature
                            + ((ambient - 22) * 0.3 + (activity - 1) * 1.0) * (1 - regulation)
                            + fever;
        _temp += (tempTarget - _temp) * (1 - Math.Exp(-dt / 30.0));

        // ---- oxygen starvation, sepsis, disease and drug toxicity injure organs (permanent damage)
        if (dt > 0)
        {
            double deficit = Math.Max(0, 1 - _oxygenDelivery / Physiology.IschemiaThreshold);
            foreach (var o in Organs)
            {
                double rate = o.IschemiaSensitivity * deficit + 0.05 * sepsis;
                if (_fx.Injury.TryGetValue(o.Name, out var diseaseInjury)) rate += diseaseInjury;
                if (o.Name == "Liver") rate += 0.06 * Over(Rx.Paracetamol);
                if (rate > 0) o.Injury = Math.Min(100, o.Injury + rate * dt);
            }
            double bleed = 0.01 * Over(Rx.Heparin) + 0.05 * Over(Rx.Alteplase) + 0.005 * Over(Rx.Aspirin);
            if (bleed > 0) O("Blood").Condition = Math.Max(0, O("Blood").Condition - bleed * dt);
        }
        UpdateEfficiencies(0);

        // ---- kidneys, liver and bowel: filtration and toxin balance
        double kidneyEff = E("Kidneys"), liverEff = E("Liver");
        double gfr = Physiology.NormalGfr * kidneyEff
                     * Math.Min(1.0, _oxygenDelivery / 0.8)
                     * Math.Min(1.0, 0.4 + 0.6 * hydration + 0.3 * fluids);
        if (_dialysis) gfr = Math.Max(gfr, 0.4 * Physiology.NormalGfr);           // the machine filters the blood
        double gfrFraction = Math.Min(1.0, gfr / Physiology.NormalGfr);
        double bowel = (E("Large Intestine") + E("Anus")) / 2.0;
        double liverClear = liverEff * (0.8 + 0.2 * E("Gallbladder"));            // blocked bile ducts slow the liver
        double clearance = 0.45 * liverClear + 0.45 * gfrFraction + 0.10 * bowel;
        double production = Physiology.ToxinProduction * metabolic * (1 + 0.5 * (1 - E("Anus"))) + _fx.Toxin;
        if (dt > 0)
            _toxins = Clamp(_toxins + (production - Physiology.ToxinClearanceCapacity * clearance) * dt, 0, 100);

        // ---- acid-base: lactate (no oxygen) + CO2 (poor ventilation) + disease vs. kidneys, liver and lungs
        double acidProduction = 0.25 * metabolic
                                + Math.Max(0, Physiology.IschemiaThreshold - _oxygenDelivery) / Physiology.IschemiaThreshold * 1.2
                                + ventDeficit * 0.8
                                + _fx.Acid
                                + Over(Rx.Adrenaline) * 0.3;
        double acidClearance = 0.55 * gfrFraction + 0.30 * liverClear + 0.15 * Math.Min(1.0, ventRatio);
        if (dt > 0)
            _acid = Clamp(_acid + (acidProduction - 0.5 * acidClearance) * dt, 0, 100);

        // ---- red blood cells: marrow makes them, kidney hormone (EPO) drives it, iron feeds it; some diseases destroy them
        double rbcProduction = Physiology.RedCellTurnover * E("Bone Marrow") * (0.4 + 0.6 * kidneyEff) * (1 + Th(Rx.Iron));
        if (dt > 0)
            _rbc = Clamp(_rbc + (rbcProduction - (Physiology.RedCellTurnover + _fx.RbcLoss) * _rbc) * dt, 0, 1.2);

        // ---- energy: digestion x pancreas (insulin) x liver stores x oxygen; dextrose and insulin act directly
        double intake = 0.15 * E("Mouth") + 0.10 * E("Teeth") + 0.05 * E("Tongue") + 0.10 * E("Salivary Glands")
                        + 0.10 * E("Esophagus") + 0.20 * E("Stomach") + 0.30 * E("Small Intestine");
        double insulinSupply = Math.Max(E("Pancreas"), 0.8 * Th(Rx.Insulin));
        double energy = Clamp(Math.Pow(insulinSupply, 0.7) * (0.25 + 0.75 * intake)
                              * (0.7 + 0.3 * liverEff) * (0.8 + 0.2 * adrenal)
                              * Math.Min(1.0, _oxygenDelivery / 0.6), 0, 1) * 100;
        energy *= _fx.Energy;
        energy = Math.Min(100, energy + 40 * Th(Rx.Dextrose));
        energy *= 1 - 0.6 * Math.Min(1.0, Over(Rx.Insulin));                      // insulin overdose: hypoglycaemia
        energy = Clamp(energy, 0, 100);

        // ---- publish
        var v = Vitals;
        v.HeartRate = _hr;
        v.RespRate = _rr;
        v.TidalVolume = _rr > 0 ? tidal : 0;
        v.SpO2 = _spo2;
        v.CardiacOutput = co;
        v.Systolic = Physiology.SystolicBp * BpScale(coRatio, 0.3, 0.9) * (0.45 + 0.55 * vascTone);
        v.Diastolic = Physiology.DiastolicBp * BpScale(coRatio, 0.15, 0.6) * (0.35 + 0.65 * vascTone);
        v.OxygenDelivery = _oxygenDelivery;
        v.Gfr = gfr;
        v.Toxins = _toxins;
        v.Energy = energy;
        v.Temperature = _temp;
        v.Acid = _acid;
        v.Ph = Physiology.NormalPh - 0.006 * _acid;
        v.Infection = _infection;
        v.Hemoglobin = _rbc * 100;

        O("Heart").Metrics["HR"] = $"{_hr:0} bpm";
        O("Arteries").Metrics["BP"] = $"{v.Systolic:0}/{v.Diastolic:0}";
        O("Blood").Metrics["Red cells"] = $"{v.Hemoglobin:0}%";
        O("Lungs").Metrics["SpO2"] = $"{_spo2:0}%";
        O("Diaphragm").Metrics["Tidal"] = $"{v.TidalVolume:0} mL";
        O("Liver").Metrics["Toxins"] = $"{_toxins:0}%";
        O("Pancreas").Metrics["Energy"] = $"{energy:0}%";
        O("Kidneys").Metrics["GFR"] = $"{gfr:0} mL/min";
        O("Hypothalamus").Metrics["Temp"] = $"{_temp:0.0} °C";
        O("Thyroid").Metrics["Metabolism"] = $"{metabolic * 100:0}%";
        O("Lymph Nodes").Metrics["Infection"] = $"{_infection:0}%";
        O("Cerebrum").Metrics["pH"] = $"{v.Ph:0.00}";

        Gait = DetermineGait();
        SystemState = DetermineSystemState();
    }

    private string DetermineGait()
    {
        var v = Vitals;
        if (_vfib) return "Collapsed [Ventricular Fibrillation]: Unconscious, no pulse";
        if (O("Brainstem").Status == OrganStatus.Offline) return "Collapsed [Central Brain Death]: No voluntary movement";
        if (O("Cerebrum").Status == OrganStatus.Offline) return "Collapsed [Cerebral Failure]: Unconscious";
        if (v.HeartRate < 1) return "Collapsed [Cardiac Arrest]: No effective circulation";
        if (v.SpO2 < 70) return "Collapsed [Severe Hypoxia]: Loss of consciousness";
        if (v.Ph < 7.0) return "Collapsed [Severe Acidosis]: Loss of consciousness";
        if (v.Temperature > 41.5) return "Collapsed [Heat Stroke]: Loss of consciousness";
        if (v.Temperature < 30) return "Collapsed [Severe Hypothermia]: Loss of consciousness";
        if (O("Spinal Cord").Status == OrganStatus.Offline) return "Paralysed [Spinal Cord Injury]: Cannot walk";
        if (O("Legs").Status == OrganStatus.Offline) return "Immobile [Locomotor Failure]: Cannot walk";
        if (O("Skeletal Muscles").Status == OrganStatus.Offline) return "Immobile [Muscle Failure]: Cannot walk";
        if (v.Toxins >= 60) return "Impaired [Severe Uremia]: Confused, cannot walk steadily";
        if (O("Cerebellum").Status != OrganStatus.Nominal) return "Impaired [Cerebellar Ataxia]: Wide, staggering gait";
        if (O("Peripheral Nerves").Status != OrganStatus.Nominal) return "Impaired [Neuropathy]: Weak, high-stepping gait";
        if (O("Pelvis").Status != OrganStatus.Nominal || O("Long Bones").Status != OrganStatus.Nominal || O("Spine").Status != OrganStatus.Nominal)
            return "Impaired [Skeletal Fault]: Painful limp";
        if (O("Legs").Status != OrganStatus.Nominal || O("Skeletal Muscles").Status != OrganStatus.Nominal)
            return "Impaired [Weak Legs]: Walking with Limp";
        if (O("Ear").Status != OrganStatus.Nominal) return "Impaired [Ataxia]: Walking with Limp (Vestibular Balance Offline)";
        if (O("Eyes").Status != OrganStatus.Nominal) return "Impaired [Vision Loss]: Cautious, shuffling gait";
        if (v.Toxins > 30) return "Impaired [Mild Uremia]: Unsteady Dizzy Gait";
        if (v.SpO2 < 90) return "Impaired [Hypoxemia]: Breathless, Slow Gait";
        if (E("Blood") < 0.6) return "Impaired [Anemia]: Pale, breathless, fatigued gait";
        if (v.Energy < 30) return "Impaired [Energy Deficit]: Slow, Fatigued Gait";
        if (v.Temperature < 35) return "Impaired [Hypothermia]: Shivering, stiff gait";
        if (v.Infection > 40) return "Impaired [Sepsis]: Weak, feverish gait";
        if (v.Temperature > 38.5) return "Impaired [Fever]: Weak, sweating gait";
        return "Normal Nominal Gait";
    }

    private string DetermineSystemState()
    {
        if (Organs.Any(o => o.Vital && o.Status == OrganStatus.Offline)) return "Fatal";
        if (Organs.Any(o => o.Status is OrganStatus.Critical or OrganStatus.Offline)) return "Critical";
        if (Organs.Any(o => o.Status == OrganStatus.Degraded)) return "Degraded";
        return "Nominal";
    }

    // ------------------------------------------------------------------ events

    private void DetectEvents(bool silent)
    {
        foreach (var o in Organs)
        {
            var status = o.Status;
            if (!silent && _lastStatus.TryGetValue(o.Name, out var prev) && prev != status)
            {
                if (status == OrganStatus.Offline)
                {
                    if (o.Vital) Raise(Severity.Fatal, $"[FATAL EXCEPTION]: FATAL [{o.FailureName}]: {o.FailureMessage}");
                    else Raise(Severity.Warning, $"[ORGAN OFFLINE]: {o.Name} — {o.FailureName}: {o.FailureMessage}");
                }
                else if (status > prev)
                {
                    Raise(Severity.Warning, status == OrganStatus.Critical
                        ? $"[CRITICAL]: {o.Name} at {o.Efficiency:0}% — {o.FailureName} imminent."
                        : $"[DEGRADED]: {o.Name} functioning at {o.Efficiency:0}%.");
                }
                else
                {
                    Raise(Severity.Success, status == OrganStatus.Nominal
                        ? $"[RECOVERY]: {o.Name} back to nominal ({o.Efficiency:0}%)."
                        : $"[RECOVERY]: {o.Name} improving — {status} ({o.Efficiency:0}%).");
                }
            }
            _lastStatus[o.Name] = status;
        }

        var v = Vitals;
        Alarm("tachy", v.HeartRate > 150, Severity.Warning, $"[ALARM]: Tachycardia — heart rate {v.HeartRate:0} bpm.", "[ALARM CLEARED]: Heart rate below tachycardia range.", silent);
        Alarm("brady", v.HeartRate > 0 && v.HeartRate < 50, Severity.Warning, $"[ALARM]: Bradycardia — heart rate {v.HeartRate:0} bpm.", "[ALARM CLEARED]: Heart rate back in range.", silent);
        Alarm("hypox", v.SpO2 < 90, Severity.Warning, $"[ALARM]: Hypoxemia — SpO2 {v.SpO2:0}%.", "[ALARM CLEARED]: SpO2 back above 90%.", silent);
        Alarm("hypot", v.HeartRate > 0 && v.Systolic < 90, Severity.Warning, $"[ALARM]: Hypotension — BP {v.Systolic:0}/{v.Diastolic:0} mmHg.", "[ALARM CLEARED]: Blood pressure back in range.", silent);
        Alarm("tox", v.Toxins > 30, Severity.Warning, $"[ALARM]: Toxin accumulation at {v.Toxins:0}% — nervous system affected.", "[ALARM CLEARED]: Toxin level back below 30%.", silent);
        Alarm("acid", v.Ph < 7.25, Severity.Warning, $"[ALARM]: Acidosis — blood pH {v.Ph:0.00}. Heart and brain weakened.", "[ALARM CLEARED]: Blood pH back above 7.25.", silent);
        Alarm("fever", v.Temperature > 38.5, Severity.Warning, $"[ALARM]: Hyperthermia — body temperature {v.Temperature:0.0} °C.", "[ALARM CLEARED]: Temperature back below 38.5 °C.", silent);
        Alarm("cold", v.Temperature < 35, Severity.Warning, $"[ALARM]: Hypothermia — body temperature {v.Temperature:0.0} °C.", "[ALARM CLEARED]: Temperature back above 35 °C.", silent);
        Alarm("sepsis", v.Infection > 40, Severity.Warning, $"[ALARM]: Sepsis — infection load {v.Infection:0}%. Vessels dilate, organs injured.", "[ALARM CLEARED]: Infection load back below 40%.", silent);
        Alarm("anemia", E("Blood") < 0.6, Severity.Warning, "[ALARM]: Severe anemia / blood loss — oxygen-carrying capacity low.", "[ALARM CLEARED]: Blood capacity back above 60%.", silent);
        Alarm("glucose", v.Energy < 30, Severity.Warning, $"[ALARM]: Energy deficit ({v.Energy:0}%) — brain and muscles starved of fuel.", "[ALARM CLEARED]: Energy supply back above 30%.", silent);

        if (!silent && _lastGait != "" && _lastGait != Gait)
            Raise(Gait.StartsWith("Normal") ? Severity.Success : Severity.Warning, $"[GAIT]: {Gait}");
        _lastGait = Gait;

        if (!silent && _lastState != "" && _lastState != SystemState)
            Raise(SystemState == "Nominal" ? Severity.Success : SystemState == "Fatal" ? Severity.Fatal : Severity.Warning,
                  $"[SYSTEM]: Systemic state {_lastState} → {SystemState}.");
        _lastState = SystemState;
    }

    private void Alarm(string key, bool active, Severity severity, string onText, string offText, bool silent)
    {
        bool was = _alarms.TryGetValue(key, out var w) && w;
        if (!silent)
        {
            if (active && !was) Raise(severity, onText);
            else if (!active && was) Raise(Severity.Info, offText);
        }
        _alarms[key] = active;
    }

    // ------------------------------------------------------------------ history

    private void RecordHistory()
    {
        while (_nextRecord <= Time + 1e-9)
        {
            History.Add(Capture(_nextRecord));
            _nextRecord += 1;
        }
    }

    private void TruncateAfterCurrent()
    {
        History.RemoveAll(s => s.Time > Time + 1e-9);
        _nextRecord = Math.Floor(Time) + 1;
        _scrubbed = false;
    }

    private Snapshot Capture(double time) => new()
    {
        Time = time,
        Condition = Organs.Select(o => o.Condition).ToArray(),
        Injury = Organs.Select(o => o.Injury).ToArray(),
        Support = Organs.Select(o => o.Support).ToArray(),
        Efficiency = Organs.Select(o => o.Efficiency).ToArray(),
        ParameterIndexes = Parameters.Select(p => p.Index).ToArray(),
        HeartRate = Vitals.HeartRate,
        RespRate = Vitals.RespRate,
        SpO2 = Vitals.SpO2,
        Systolic = Vitals.Systolic,
        Diastolic = Vitals.Diastolic,
        CardiacOutput = Vitals.CardiacOutput,
        Gfr = Vitals.Gfr,
        Toxins = Vitals.Toxins,
        Energy = Vitals.Energy,
        OxygenDelivery = Vitals.OxygenDelivery,
        Temperature = _temp,
        Acid = _acid,
        Infection = _infection,
        Rbc = _rbc,
        Drugs = new Dictionary<string, double>(_drugs),
        Diseases = new Dictionary<string, double>(_diseases),
        Vfib = _vfib,
        Oxygen = _oxygenTherapy,
        Ventilator = _ventilator,
        Cpr = _cpr,
        Dialysis = _dialysis,
    };

    /// <param name="names">organ names the arrays are ordered by; null = current organ order</param>
    private void ApplySnapshot(Snapshot s, string[]? names)
    {
        for (int i = 0; i < s.Condition.Length; i++)
        {
            string name = names != null ? (i < names.Length ? names[i] : "") : (i < Organs.Count ? Organs[i].Name : "");
            if (!_byName.TryGetValue(name, out var organ)) continue;
            organ.Condition = s.Condition[i];
            organ.Injury = i < s.Injury.Length ? s.Injury[i] : 0;
            organ.Support = i < s.Support.Length ? s.Support[i] : 1;
        }
        for (int i = 0; i < Parameters.Count && i < s.ParameterIndexes.Length; i++)
            Parameters[i].Index = Math.Clamp(s.ParameterIndexes[i], 0, Parameters[i].Options.Length - 1);
        _hr = s.HeartRate;
        _rr = s.RespRate;
        _spo2 = s.SpO2 > 0 ? s.SpO2 : Physiology.NormalSpO2;
        _oxygenDelivery = s.OxygenDelivery;
        _toxins = s.Toxins;
        _acid = s.Acid;
        _infection = s.Infection;
        _temp = s.Temperature > 0 ? s.Temperature : Physiology.NormalTemperature;
        _rbc = s.Rbc > 0 ? s.Rbc : 1;
        Vitals.Energy = s.Energy > 0 ? s.Energy : 100;
        _drugs.Clear();
        foreach (var kv in s.Drugs) if (Pharmacy.Find(kv.Key) != null) _drugs[kv.Key] = kv.Value;
        _diseases.Clear();
        foreach (var kv in s.Diseases) if (Pathology.Find(kv.Key) != null) _diseases[kv.Key] = kv.Value;
        _toxicFlags.Clear();
        _vfib = s.Vfib; _oxygenTherapy = s.Oxygen; _ventilator = s.Ventilator; _cpr = s.Cpr; _dialysis = s.Dialysis;
    }
}

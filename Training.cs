using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VirtualMan;

/// <summary>A training patient: a briefing, the hidden disease(s), and what an ideal doctor would do.</summary>
public sealed record MedicalCase(string Id, string Title, int Difficulty, string Briefing,
                                 (string Id, double Severity)[] Diseases, string Diagnosis,
                                 string[] Ideal, double TimeLimit, string Debrief);

public static class Training
{
    public static readonly MedicalCase[] Cases =
    {
        new("asthma", "Wheezing and cannot speak", 1,
            "A 12-year-old boy is brought in from football. He is wheezing loudly and can barely finish a sentence.\nHistory of asthma. Oxygen saturation is falling.",
            new[] { ("asthma", 40.0) }, "asthma",
            new[] { Rx.Salbutamol, Rx.Oxygen, Rx.Hydrocortisone }, 300,
            "Salbutamol opens the airways within minutes. Oxygen supports him meanwhile and steroids stop the attack returning. Beta-blockers would have made it worse."),
        new("pneumonia", "Child with fever and fast breathing", 1,
            "A 4-year-old girl has had fever and cough for three days. She is breathing fast and looks tired.",
            new[] { ("pneumonia", 25.0) }, "pneumonia",
            new[] { Rx.Amoxicillin, Rx.Oxygen }, 900,
            "Bacterial pneumonia is treated with an antibiotic; oxygen is given if saturation is low."),
        new("malaria", "Traveller with chills and fever", 1,
            "A 30-year-old returns from a rural district with cyclic fever, chills and headache. She looks pale.",
            new[] { ("malaria", 25.0) }, "malaria",
            new[] { Rx.Antimalarial, Rx.Paracetamol }, 1200,
            "A rapid test confirms malaria. Artemisinin combination therapy cures it; paracetamol relieves the fever. Antibiotics do nothing against the parasite."),
        new("gastro", "Severe diarrhoea after a feast", 1,
            "A 25-year-old has passed watery stool for two days and is now very thirsty, dizzy and weak.",
            new[] { ("gastro", 40.0) }, "gastro",
            new[] { Rx.Ors, Rx.Saline }, 900,
            "Rehydration is the treatment: oral salts if he can drink, intravenous saline when he is shocked."),
        new("mi", "Chest pain at the market", 2,
            "A 55-year-old man has crushing chest pain spreading to the left arm, with sweating and nausea.\nTime matters: heart muscle is dying.",
            new[] { ("mi", 40.0) }, "mi",
            new[] { Rx.Aspirin, Rx.Alteplase, Rx.Heparin }, 420,
            "Heart attack: antiplatelet, anticoagulant and early clot-busting limit the damage. If it reaches ventricular fibrillation you need CPR and a shock."),
        new("stroke", "Sudden weakness and slurred speech", 2,
            "A 62-year-old woman suddenly cannot move her right arm and her speech is slurred. Onset was 40 minutes ago.",
            new[] { ("stroke", 35.0) }, "stroke",
            new[] { Rx.Alteplase, Rx.Aspirin }, 420,
            "Ischaemic stroke: thrombolysis within the time window, then aspirin. A CT scan first excludes bleeding."),
        new("pe", "Breathless after a long flight", 2,
            "A 40-year-old man develops sudden chest pain and breathlessness after a 12-hour flight. One calf is swollen.",
            new[] { ("pe", 35.0) }, "pe",
            new[] { Rx.Heparin, Rx.Oxygen }, 480,
            "Pulmonary embolism: anticoagulation stops the clot growing and oxygen supports gas exchange."),
        new("anaphylaxis", "Wasp sting and a swelling throat", 2,
            "A 20-year-old was stung by a wasp ten minutes ago. His lips and tongue are swelling and he is wheezing and dizzy.",
            new[] { ("anaphylaxis", 40.0) }, "anaphylaxis",
            new[] { Rx.Adrenaline, Rx.Hydrocortisone }, 240,
            "Anaphylaxis kills in minutes. Adrenaline is first-line and cannot be delayed; steroids are supportive only."),
        new("apap", "Overdose of pain tablets", 2,
            "A 19-year-old swallowed a large number of paracetamol tablets six hours ago. She now has nausea and abdominal pain.",
            new[] { ("apap", 30.0) }, "apap",
            new[] { Rx.Acetylcysteine }, 1500,
            "Paracetamol poisoning damages the liver silently. The antidote acetylcysteine works best when given early."),
        new("tb", "Chronic cough and night sweats", 2,
            "A 35-year-old has coughed for six weeks, loses weight and drenches his bed at night.\n(Tip: run the simulation at 30x - treatment takes months.)",
            new[] { ("tb", 40.0) }, "tb",
            new[] { Rx.AntiTb }, 12000,
            "Tuberculosis needs a multi-drug regimen for months. Stopping early lets it return."),
        new("sepsis", "Fever, low blood pressure and confusion", 3,
            "A 58-year-old man has a hot, flushed skin, a fast pulse and a blood pressure of 85/50. He is confused.",
            new[] { ("sepsis", 35.0) }, "sepsis",
            new[] { Rx.Ceftriaxone, Rx.Saline, Rx.Noradrenaline }, 600,
            "Septic shock: antibiotics, fluids, and a vasopressor if the pressure stays low."),
        new("meningitis", "Fever, headache and a stiff neck", 3,
            "A 17-year-old has a severe headache, fever, vomiting and hurts when she looks at light. Her neck is stiff.",
            new[] { ("meningitis", 30.0) }, "meningitis",
            new[] { Rx.Ceftriaxone, Rx.Hydrocortisone }, 600,
            "Bacterial meningitis: do not wait for the scan - give antibiotics at once, with a steroid to reduce brain swelling."),
        new("dka", "Thirsty, drowsy, breathing deeply", 3,
            "A 16-year-old with newly noticed weight loss is drowsy, breathing deeply and fast, and smells of acetone.",
            new[] { ("dka", 35.0) }, "dka",
            new[] { Rx.Insulin, Rx.Saline }, 900,
            "Diabetic ketoacidosis: insulin and fluids. The deep breathing is the lungs blowing off acid."),
        new("hiv", "Weight loss and repeated infections", 3,
            "A 32-year-old has lost weight, has oral thrush, and now a chest infection that will not clear.",
            new[] { ("hiv", 30.0), ("pneumonia", 15.0) }, "hiv",
            new[] { Rx.Art, Rx.Amoxicillin }, 6000,
            "HIV weakens immunity so ordinary infections become dangerous. Treat the infection and start antiretroviral therapy."),
        new("arrest", "Collapse in the clinic", 3,
            "A 60-year-old man collapses in the waiting room: no pulse, not breathing.",
            new[] { ("mi", 90.0) }, "mi",
            new[] { Rx.Cpr, Rx.Defib, Rx.Adrenaline, Rx.Aspirin }, 300,
            "Ventricular fibrillation: start CPR immediately, shock as soon as the defibrillator is ready, give adrenaline, then treat the cause (the heart attack) with aspirin and clot-busting drugs."),
    };
}

public sealed class CaseResult
{
    public string Title { get; set; } = "";
    public string Outcome { get; set; } = "";
    public int Score { get; set; }
    public string Grade { get; set; } = "";
    public int Xp { get; set; }
    public List<string> Lines { get; } = new();
    public string Debrief { get; set; } = "";
    public string Rank { get; set; } = "";

    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Title.ToUpperInvariant());
        sb.AppendLine($"Outcome: {Outcome}");
        sb.AppendLine();
        foreach (var l in Lines) sb.AppendLine(l);
        sb.AppendLine(new string('-', 50));
        sb.AppendLine($"SCORE {Score}/100   GRADE {Grade}   +{Xp} XP");
        sb.AppendLine($"Rank: {Rank}");
        sb.AppendLine();
        sb.AppendLine("WHAT TO LEARN");
        sb.AppendLine(Debrief);
        return sb.ToString();
    }
}

/// <summary>Your doctor career: experience points and rank, saved on this computer.</summary>
public sealed class Career
{
    private static readonly (int Xp, string Title)[] Ranks =
    {
        (0, "Medical Intern"), (300, "Resident Doctor"), (800, "Specialist"), (1600, "Consultant"), (3000, "Chief of Medicine"),
    };

    public int TotalXp { get; set; }
    public int CasesCompleted { get; set; }
    public Dictionary<string, int> BestScores { get; set; } = new();

    [JsonIgnore]
    public string Rank => Ranks.Last(r => TotalXp >= r.Xp).Title;

    [JsonIgnore]
    public string NextRank
    {
        get
        {
            var next = Ranks.FirstOrDefault(r => r.Xp > TotalXp);
            return next.Title == null ? "highest rank reached" : $"{next.Title} at {next.Xp} XP";
        }
    }

    private static string FilePath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BioSim", "career.json");

    public static Career Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Career>(File.ReadAllText(FilePath)) ?? new Career();
        }
        catch { /* a damaged file just starts a new career */ }
        return new Career();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* saving is best-effort */ }
    }
}

/// <summary>Runs one patient case at a time: starts it, watches it, and scores it.</summary>
public sealed class CaseManager
{
    private readonly Simulation _sim;
    private double _stableSince = -1;

    public Career Career { get; } = Career.Load();
    public MedicalCase? Active { get; private set; }
    public string? Diagnosis { get; private set; }
    public double StartTime { get; private set; }
    public CaseResult? LastResult { get; private set; }
    public event Action<CaseResult>? CaseFinished;

    public CaseManager(Simulation sim) { _sim = sim; }

    public double Elapsed => Active == null ? 0 : _sim.Time - StartTime;
    public double TimeLeft => Active == null ? 0 : Math.Max(0, Active.TimeLimit - Elapsed);

    public void Start(MedicalCase c)
    {
        _sim.Reset(false);
        foreach (var (id, severity) in c.Diseases)
        {
            var def = Pathology.Find(id);
            if (def != null) _sim.InjectDisease(def, severity, true);
        }
        Active = c;
        Diagnosis = null;
        StartTime = _sim.Time;
        _stableSince = -1;
        _sim.ResetCounters();
        _sim.Raise(Severity.Action, $"[CASE]: {c.Title} — {c.Briefing.Replace('\n', ' ')}");
        _sim.Refresh();
    }

    public void SetDiagnosis(string diseaseId)
    {
        if (Active == null) return;
        Diagnosis = diseaseId;
        var name = Pathology.Find(diseaseId)?.Name ?? diseaseId;
        _sim.Actions.Add(new ActionRecord(_sim.Time, "Diagnosis", name));
        _sim.Raise(Severity.Action, $"[CASE]: Working diagnosis recorded — {name}.");
    }

    public void EndNow() => Finish("Ended by the doctor");

    /// <summary>Call regularly. Ends the case when the patient dies, is stabilised, or time runs out.</summary>
    public void Tick()
    {
        if (Active == null) return;
        if (_sim.IsDead) { Finish("The patient died"); return; }

        var v = _sim.Vitals;
        bool stable = _sim.Diseases.Count == 0 && v.SpO2 >= 92 && v.Systolic >= 90 && v.HeartRate > 0 && _sim.SystemState != "Fatal";
        if (stable)
        {
            if (_stableSince < 0) _stableSince = _sim.Time;
            if (_sim.Time - _stableSince >= 30) { Finish("Patient stabilised"); return; }
        }
        else _stableSince = -1;

        if (Elapsed > Active.TimeLimit) Finish("Time limit reached");
    }

    private void Finish(string outcome)
    {
        var c = Active;
        if (c == null) return;
        Active = null;

        bool alive = !_sim.IsDead && _sim.SystemState != "Fatal";
        bool resolved = c.Diseases.All(d => !_sim.Diseases.ContainsKey(d.Id));

        var r = new CaseResult { Title = c.Title, Outcome = outcome, Debrief = c.Debrief };

        int survival = alive ? 30 : 0;
        int cured = resolved ? 10 : 0;
        int diagnosis = Diagnosis == c.Diagnosis ? 20 : 0;

        var given = _sim.Actions.Where(a => a.Kind is "Medicine" or "Procedure").Select(a => a.Name).ToHashSet();
        int idealGiven = c.Ideal.Count(given.Contains);
        int treatment = (int)Math.Round(25.0 * idealGiven / c.Ideal.Length);

        var firstIdeal = _sim.Actions.Where(a => a.Kind is "Medicine" or "Procedure" && c.Ideal.Contains(a.Name))
                                     .Select(a => a.Time - StartTime).DefaultIfEmpty(-1).Min();
        int speed = 0;
        if (firstIdeal >= 0)
            speed = firstIdeal <= 0.25 * c.TimeLimit ? 15 : firstIdeal <= 0.5 * c.TimeLimit ? 10 : firstIdeal <= 0.75 * c.TimeLimit ? 5 : 0;

        int penalties = _sim.AdverseEvents * 5 + _sim.WrongShocks * 5;
        int total = Math.Max(0, Math.Min(100, survival + cured + diagnosis + treatment + speed - penalties));
        if (!alive) total = Math.Min(total, 40);

        r.Lines.Add($"Patient alive and not in a fatal state ... {survival,3} / 30");
        r.Lines.Add($"Disease cured ............................ {cured,3} / 10");
        r.Lines.Add($"Correct diagnosis ........................ {diagnosis,3} / 20   (you chose: {(Diagnosis == null ? "none" : Pathology.Find(Diagnosis)?.Name)})");
        r.Lines.Add($"Right treatments ({idealGiven}/{c.Ideal.Length}) ................... {treatment,3} / 25");
        r.Lines.Add($"Speed of first correct treatment ........ {speed,3} / 15");
        r.Lines.Add($"Penalties ({_sim.AdverseEvents} overdoses, {_sim.WrongShocks} wrong shocks) ....... -{penalties}");
        r.Lines.Add("Ideal treatment: " + string.Join(", ", c.Ideal));
        r.Lines.Add("Hidden diagnosis: " + (Pathology.Find(c.Diagnosis)?.Name ?? c.Diagnosis));

        r.Score = total;
        r.Grade = total >= 90 ? "A" : total >= 75 ? "B" : total >= 60 ? "C" : total >= 40 ? "D" : "F";
        r.Xp = total * c.Difficulty;

        Career.TotalXp += r.Xp;
        Career.CasesCompleted++;
        if (!Career.BestScores.TryGetValue(c.Id, out var best) || total > best) Career.BestScores[c.Id] = total;
        Career.Save();
        r.Rank = Career.Rank;

        LastResult = r;
        _sim.Raise(alive ? Severity.Success : Severity.Fatal, $"[CASE]: {c.Title} finished — {outcome}. Score {total}/100 ({r.Grade}).");
        CaseFinished?.Invoke(r);
    }
}

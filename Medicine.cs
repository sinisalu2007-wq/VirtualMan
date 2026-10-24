using System;
using System.Collections.Generic;
using System.Linq;

namespace VirtualMan;

/// <summary>Names of medicines and procedures, so the engine, diseases and cases always agree.</summary>
public static class Rx
{
    // medicines
    public const string Aspirin = "Aspirin", Heparin = "Heparin", Alteplase = "Alteplase",
        Metoprolol = "Metoprolol", Amlodipine = "Amlodipine", Furosemide = "Furosemide",
        Adrenaline = "Adrenaline", Noradrenaline = "Noradrenaline",
        Salbutamol = "Salbutamol", Hydrocortisone = "Hydrocortisone",
        Amoxicillin = "Amoxicillin", Ceftriaxone = "Ceftriaxone", Gentamicin = "Gentamicin",
        Antimalarial = "Artemether-Lumefantrine", AntiTb = "Anti-TB regimen (RHZE)", Art = "Antiretroviral therapy (ART)",
        Insulin = "Insulin", Dextrose = "Dextrose 50%", Levothyroxine = "Levothyroxine", Iron = "Iron + folic acid",
        Saline = "Normal saline", Ors = "Oral rehydration salts",
        Paracetamol = "Paracetamol", Acetylcysteine = "Acetylcysteine";

    // procedures
    public const string Oxygen = "Oxygen therapy", Ventilator = "Mechanical ventilation",
        Cpr = "CPR (chest compressions)", Dialysis = "Dialysis",
        Defib = "Defibrillation", Transfusion = "Blood transfusion";
}

/// <summary>One medicine. Each dose adds 1.0 to its level; the level fades with time constant Tau (seconds).
/// A level above Toxic means overdose.</summary>
public sealed record Medicine(string Name, string Category, string Route, string Dose,
                              double Tau, double Toxic, string Use, string Warning);

public static class Pharmacy
{
    public static readonly Medicine[] All =
    {
        new(Rx.Aspirin, "Heart & Clots", "Oral", "300 mg once", 900, 2.5,
            "Antiplatelet: slows clot growth in heart attack and stroke.", "Stomach irritation and bleeding when overdosed."),
        new(Rx.Heparin, "Heart & Clots", "IV", "5,000 units bolus", 600, 2.0,
            "Anticoagulant: stops clots growing (pulmonary embolism, heart attack).", "Bleeding when overdosed."),
        new(Rx.Alteplase, "Heart & Clots", "IV", "Weight-based infusion", 300, 1.3,
            "Thrombolytic 'clot buster': dissolves the clot in heart attack, stroke or pulmonary embolism.", "Serious bleeding if repeated. Best given early."),

        new(Rx.Metoprolol, "Blood Pressure & Fluid", "IV / oral", "5 mg", 900, 2.0,
            "Beta-blocker: slows the heart and lowers its workload.", "Dangerous in asthma and in shock (heart rate cannot compensate)."),
        new(Rx.Amlodipine, "Blood Pressure & Fluid", "Oral", "5 mg", 1200, 2.5,
            "Lowers blood pressure (hypertensive crisis).", "Too much causes hypotension; harmful in shock."),
        new(Rx.Furosemide, "Blood Pressure & Fluid", "IV", "40 mg", 600, 2.0,
            "Diuretic: removes fluid (fluid overload, heart failure).", "Worsens dehydration and low blood pressure."),

        new(Rx.Adrenaline, "Emergency & Shock", "IM / IV", "0.5 mg IM (allergy) or 1 mg IV (arrest)", 120, 1.5,
            "Raises heart rate and vessel tone, opens the airways. First-line in anaphylaxis.", "Overdose strains the heart and raises acid levels."),
        new(Rx.Noradrenaline, "Emergency & Shock", "IV infusion", "Titrated to blood pressure", 90, 1.5,
            "Vasopressor: tightens vessels to lift blood pressure in septic shock.", "Starves kidneys and gut of blood flow when excessive."),

        new(Rx.Salbutamol, "Breathing", "Nebulised", "2.5 mg", 600, 2.0,
            "Bronchodilator: opens the airways in asthma.", "Fast heart rate and tremor with repeated doses."),
        new(Rx.Hydrocortisone, "Breathing", "IV", "100 mg", 1800, 3.0,
            "Corticosteroid: calms inflammation and replaces adrenal hormones.", "Suppresses the immune system."),

        new(Rx.Amoxicillin, "Infection", "Oral", "500 mg", 900, 3.0,
            "Antibiotic for chest and other common bacterial infections.", "Does not cover meningitis or severe sepsis well."),
        new(Rx.Ceftriaxone, "Infection", "IV", "2 g", 1500, 3.0,
            "Broad-spectrum antibiotic for pneumonia, sepsis and meningitis.", "Needs the correct diagnosis; useless against malaria, TB and viruses."),
        new(Rx.Gentamicin, "Infection", "IV", "5 mg/kg", 1200, 1.5,
            "Antibiotic for severe bacterial sepsis.", "Toxic to the kidneys and inner ear when overdosed."),
        new(Rx.Antimalarial, "Infection", "Oral", "4 tablets", 1500, 3.0,
            "Artemisinin combination therapy for malaria.", "Only works on malaria parasites."),
        new(Rx.AntiTb, "Infection", "Oral", "Daily fixed-dose combination", 3000, 2.0,
            "Four-drug regimen for tuberculosis (takes months in real life).", "Can injure the liver when overdosed."),
        new(Rx.Art, "Infection", "Oral", "1 tablet daily", 4000, 3.0,
            "Antiretroviral therapy: suppresses HIV so the immune system recovers.", "Must be taken continuously; the virus returns if stopped."),

        new(Rx.Insulin, "Diabetes, Thyroid & Blood", "SC / IV", "4-6 units rapid-acting", 600, 1.5,
            "Replaces missing insulin: treats diabetic ketoacidosis.", "Overdose causes dangerously low blood sugar."),
        new(Rx.Dextrose, "Diabetes, Thyroid & Blood", "IV", "25 g", 300, 3.0,
            "Raises blood sugar quickly (hypoglycaemia).", "Short-lived effect."),
        new(Rx.Levothyroxine, "Diabetes, Thyroid & Blood", "Oral", "50 mcg", 5000, 3.0,
            "Replaces thyroid hormone (hypothyroidism).", "Works slowly."),
        new(Rx.Iron, "Diabetes, Thyroid & Blood", "Oral", "1 tablet", 4000, 3.5,
            "Rebuilds red blood cells in iron-deficiency anaemia.", "Works slowly."),

        new(Rx.Saline, "Fluids", "IV", "500 mL bolus", 900, 3.0,
            "Restores blood volume (dehydration, bleeding, sepsis).", "Too much fluid floods the lungs when the heart is weak."),
        new(Rx.Ors, "Fluids", "Oral", "200 mL", 900, 4.0,
            "Oral rehydration salts for diarrhoeal dehydration.", "Needs a working gut."),

        new(Rx.Paracetamol, "Pain, Fever & Antidotes", "Oral", "1 g", 1000, 2.5,
            "Lowers fever and relieves pain.", "Repeated overdosing damages the liver."),
        new(Rx.Acetylcysteine, "Pain, Fever & Antidotes", "IV", "Weight-based infusion", 1500, 3.0,
            "Antidote for paracetamol poisoning.", "Only helps paracetamol toxicity."),
    };

    private static readonly Dictionary<string, Medicine> ByName = All.ToDictionary(m => m.Name);
    public static Medicine? Find(string name) => ByName.TryGetValue(name, out var m) ? m : null;
}

/// <summary>A disease you can inject. Severity (0-100) grows by itself, damages organs and body chemistry,
/// and is pushed back down by the right medicines (Cures: medicine -> severity removed per second).</summary>
public sealed class DiseaseDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public string Symptoms { get; init; } = "";
    public string Teaching { get; init; } = "";
    /// <summary>Logistic growth per second while untreated.</summary>
    public double Growth { get; init; }
    public double Start { get; init; } = 20;
    /// <summary>Immune system slows these diseases down.</summary>
    public bool Infectious { get; init; }

    // whole-body effects at 100% severity
    public double InfectionLoad { get; init; }   // systemic infection floor (0-1 of the infection scale)
    public double RbcLoss { get; init; }         // extra red-cell loss per second
    public double AcidLoad { get; init; }
    public double ToxinLoad { get; init; }
    public double EnergyLoss { get; init; }      // fraction of energy supply lost
    public double Vasodilation { get; init; }    // fraction of vessel tone lost
    public double Obstruction { get; init; }     // fraction of airflow blocked
    public double Hypertension { get; init; }    // extra vessel tone
    public double FluidLoss { get; init; }       // fluid lost (diarrhoea, vomiting)

    /// <summary>(organ, penalty % at 100% severity - reversible, injury %/s at 100% severity - permanent)</summary>
    public List<(string Organ, double Penalty, double Injury)> Impacts { get; } = new();
    public Dictionary<string, double> Cures { get; } = new();
}

public static class Pathology
{
    public static readonly DiseaseDef[] Diseases =
    {
        new()
        {
            Id = "mi", Name = "Heart Attack (myocardial infarction)", Category = "Heart & Vessels",
            Description = "A coronary artery is blocked and heart muscle starts to die.",
            Symptoms = "Crushing chest pain, sweating, nausea, breathlessness. Severe cases go into ventricular fibrillation.",
            Teaching = "Time is muscle. Clot-busting drugs and antiplatelets work best early. Ventricular fibrillation needs CPR and a shock.",
            Growth = 0.02, Start = 40,
            Impacts = { ("Heart", 90.0, 0.20) },
            Cures = { [Rx.Alteplase] = 0.8, [Rx.Heparin] = 0.15, [Rx.Aspirin] = 0.10, [Rx.Metoprolol] = 0.05 },
        },
        new()
        {
            Id = "htn", Name = "Hypertensive Crisis", Category = "Heart & Vessels",
            Description = "Dangerously high blood pressure strains the heart, brain, kidneys and eyes.",
            Symptoms = "Severe headache, blurred vision, chest discomfort.",
            Teaching = "Lower pressure gradually. Beta-blockers and calcium-channel blockers help; do not give them to a patient in shock.",
            Growth = 0.01, Start = 20, Hypertension = 0.6,
            Impacts = { ("Heart", 40.0, 0.03), ("Kidneys", 20.0, 0.01), ("Cerebrum", 15.0, 0.01), ("Eyes", 20.0, 0.0) },
            Cures = { [Rx.Amlodipine] = 0.25, [Rx.Metoprolol] = 0.10 },
        },
        new()
        {
            Id = "anaphylaxis", Name = "Anaphylaxis (severe allergy)", Category = "Heart & Vessels",
            Description = "A severe allergic reaction: vessels dilate and the airways swell shut.",
            Symptoms = "Swelling of face and throat, wheeze, low blood pressure, collapse.",
            Teaching = "Adrenaline first, immediately. Steroids and bronchodilators are only supportive.",
            Growth = 0.07, Start = 40, Vasodilation = 0.6, Obstruction = 0.6,
            Impacts = { ("Bronchi", 50.0, 0.0), ("Larynx", 30.0, 0.0) },
            Cures = { [Rx.Adrenaline] = 2.0, [Rx.Hydrocortisone] = 0.2, [Rx.Salbutamol] = 0.3 },
        },
        new()
        {
            Id = "stroke", Name = "Ischemic Stroke", Category = "Brain & Nerves",
            Description = "A clot blocks an artery of the brain.",
            Symptoms = "Sudden face droop, arm weakness, slurred speech, confusion.",
            Teaching = "Clot-busting treatment must be given quickly after onset; aspirin helps prevent extension.",
            Growth = 0.02, Start = 35,
            Impacts = { ("Cerebrum", 85.0, 0.15), ("Cerebellum", 20.0, 0.02) },
            Cures = { [Rx.Alteplase] = 0.8, [Rx.Aspirin] = 0.12 },
        },
        new()
        {
            Id = "meningitis", Name = "Bacterial Meningitis", Category = "Brain & Nerves",
            Description = "Bacteria infect the membranes around the brain.",
            Symptoms = "Fever, severe headache, stiff neck, light sensitivity, confusion.",
            Teaching = "Give a broad-spectrum antibiotic immediately; steroids reduce brain swelling.",
            Growth = 0.012, Start = 20, Infectious = true, InfectionLoad = 0.7,
            Impacts = { ("Cerebrum", 60.0, 0.05), ("Brainstem", 25.0, 0.03), ("Hypothalamus", 30.0, 0.0) },
            Cures = { [Rx.Ceftriaxone] = 0.30, [Rx.Amoxicillin] = 0.08, [Rx.Hydrocortisone] = 0.05 },
        },
        new()
        {
            Id = "pneumonia", Name = "Pneumonia", Category = "Lungs & Airways",
            Description = "Bacteria infect the lungs.",
            Symptoms = "Fever, productive cough, fast breathing, low oxygen.",
            Teaching = "Antibiotics cure it; oxygen supports the patient meanwhile.",
            Growth = 0.008, Start = 25, Infectious = true, InfectionLoad = 0.6,
            Impacts = { ("Lungs", 70.0, 0.01), ("Bronchi", 20.0, 0.0) },
            Cures = { [Rx.Amoxicillin] = 0.25, [Rx.Ceftriaxone] = 0.35 },
        },
        new()
        {
            Id = "asthma", Name = "Asthma Attack", Category = "Lungs & Airways",
            Description = "The small airways tighten and narrow.",
            Symptoms = "Wheeze, cannot speak a full sentence, chest tightness.",
            Teaching = "Salbutamol opens the airways within minutes; steroids stop the attack returning. Beta-blockers are dangerous.",
            Growth = 0.05, Start = 30, Obstruction = 0.85,
            Impacts = { ("Bronchi", 80.0, 0.0), ("Lungs", 10.0, 0.0) },
            Cures = { [Rx.Salbutamol] = 1.5, [Rx.Hydrocortisone] = 0.4, [Rx.Adrenaline] = 1.0 },
        },
        new()
        {
            Id = "pe", Name = "Pulmonary Embolism", Category = "Lungs & Airways",
            Description = "A clot lodges in the lung arteries.",
            Symptoms = "Sudden breathlessness and chest pain, often after a long flight or immobility.",
            Teaching = "Anticoagulation stops the clot growing; clot-busters are for massive embolism.",
            Growth = 0.02, Start = 35,
            Impacts = { ("Lungs", 60.0, 0.05), ("Heart", 20.0, 0.05) },
            Cures = { [Rx.Heparin] = 0.30, [Rx.Alteplase] = 0.8 },
        },
        new()
        {
            Id = "tb", Name = "Tuberculosis", Category = "Infections",
            Description = "A slow bacterial infection of the lungs.",
            Symptoms = "Chronic cough, night sweats, weight loss.",
            Teaching = "Needs months of a multi-drug regimen (run the simulation at high speed).",
            Growth = 0.0008, Start = 10, Infectious = true, InfectionLoad = 0.3, EnergyLoss = 0.2,
            Impacts = { ("Lungs", 60.0, 0.002), ("Lymph Nodes", 10.0, 0.0) },
            Cures = { [Rx.AntiTb] = 0.03 },
        },
        new()
        {
            Id = "malaria", Name = "Malaria", Category = "Infections",
            Description = "Plasmodium parasites infect and destroy red blood cells.",
            Symptoms = "Cyclic fever and chills, headache, anaemia, enlarged spleen.",
            Teaching = "Artemisinin combination therapy cures it. Untreated severe malaria reaches the brain.",
            Growth = 0.006, Start = 15, Infectious = true, InfectionLoad = 0.5, RbcLoss = 0.004,
            Impacts = { ("Spleen", 30.0, 0.0), ("Liver", 10.0, 0.0), ("Cerebrum", 40.0, 0.01) },
            Cures = { [Rx.Antimalarial] = 0.30 },
        },
        new()
        {
            Id = "hiv", Name = "HIV / AIDS", Category = "Infections",
            Description = "The virus slowly destroys the immune system.",
            Symptoms = "Weight loss, recurrent infections, swollen lymph nodes.",
            Teaching = "Lifelong antiretroviral therapy suppresses the virus and lets immunity recover.",
            Growth = 0.0004, Start = 10, EnergyLoss = 0.1,
            Impacts = { ("Thymus", 50.0, 0.0), ("Lymph Nodes", 50.0, 0.0), ("Bone Marrow", 30.0, 0.0), ("Spleen", 20.0, 0.0) },
            Cures = { [Rx.Art] = 0.03 },
        },
        new()
        {
            Id = "sepsis", Name = "Bacterial Sepsis", Category = "Infections",
            Description = "A bloodstream infection with whole-body inflammation.",
            Symptoms = "Fever, fast pulse, low blood pressure, confusion.",
            Teaching = "Antibiotics within the hour, fluids, and a vasopressor if pressure stays low.",
            Growth = 0.01, Start = 35, Infectious = true, InfectionLoad = 1.0, Vasodilation = 0.5,
            Impacts = { ("Kidneys", 30.0, 0.02), ("Liver", 20.0, 0.01), ("Lungs", 15.0, 0.0) },
            Cures = { [Rx.Ceftriaxone] = 0.30, [Rx.Gentamicin] = 0.30, [Rx.Amoxicillin] = 0.10 },
        },
        new()
        {
            Id = "gastro", Name = "Severe Gastroenteritis (cholera-like)", Category = "Digestion & Metabolism",
            Description = "Profuse diarrhoea and vomiting drain the body of fluid.",
            Symptoms = "Watery diarrhoea, vomiting, thirst, sunken eyes, low blood pressure.",
            Teaching = "Rehydration is the treatment. Oral salts when the patient can drink, IV fluids when shocked.",
            Growth = 0.006, Start = 40, Infectious = true, InfectionLoad = 0.4, FluidLoss = 0.8,
            Impacts = { ("Small Intestine", 50.0, 0.0), ("Large Intestine", 40.0, 0.0), ("Kidneys", 20.0, 0.0) },
            Cures = { [Rx.Ors] = 0.30, [Rx.Saline] = 0.35, [Rx.Ceftriaxone] = 0.10 },
        },
        new()
        {
            Id = "dka", Name = "Diabetic Ketoacidosis", Category = "Digestion & Metabolism",
            Description = "Without insulin the body burns fat and the blood turns acid.",
            Symptoms = "Thirst, deep fast breathing, fruity breath, drowsiness, very high blood sugar.",
            Teaching = "Insulin and fluids. The acidosis is why the patient breathes so deeply.",
            Growth = 0.01, Start = 35, AcidLoad = 0.6, EnergyLoss = 0.5,
            Impacts = { ("Pancreas", 70.0, 0.0), ("Kidneys", 10.0, 0.0) },
            Cures = { [Rx.Insulin] = 0.35, [Rx.Saline] = 0.05 },
        },
        new()
        {
            Id = "apap", Name = "Paracetamol Poisoning", Category = "Digestion & Metabolism",
            Description = "Too much paracetamol overwhelms the liver.",
            Symptoms = "Nausea, then right-sided abdominal pain, jaundice, confusion.",
            Teaching = "The antidote acetylcysteine works best when given early.",
            Growth = 0.004, Start = 30, ToxinLoad = 0.4,
            Impacts = { ("Liver", 85.0, 0.05) },
            Cures = { [Rx.Acetylcysteine] = 0.30 },
        },
        new()
        {
            Id = "aki", Name = "Acute Kidney Injury", Category = "Digestion & Metabolism",
            Description = "The kidneys suddenly stop filtering.",
            Symptoms = "Little urine, swelling, confusion, nausea.",
            Teaching = "Restore volume if the cause is dehydration; dialysis bridges severe failure.",
            Growth = 0.006, Start = 25,
            Impacts = { ("Kidneys", 80.0, 0.03) },
            Cures = { [Rx.Saline] = 0.10 },
        },
        new()
        {
            Id = "hypothyroid", Name = "Hypothyroidism", Category = "Hormones & Blood",
            Description = "The thyroid gland stops making enough hormone.",
            Symptoms = "Tiredness, cold intolerance, slow pulse, weight gain.",
            Teaching = "Daily levothyroxine replaces the hormone; the metabolic rate normalises over time.",
            Growth = 0.0006, Start = 15,
            Impacts = { ("Thyroid", 90.0, 0.0) },
            Cures = { [Rx.Levothyroxine] = 0.03 },
        },
        new()
        {
            Id = "anemia", Name = "Iron-deficiency Anaemia", Category = "Hormones & Blood",
            Description = "Not enough iron to make hemoglobin.",
            Symptoms = "Pallor, tiredness, breathlessness on effort.",
            Teaching = "Iron and folic acid rebuild the red cells over weeks.",
            Growth = 0.0006, Start = 15, RbcLoss = 0.0012,
            Impacts = { ("Bone Marrow", 30.0, 0.0) },
            Cures = { [Rx.Iron] = 0.03 },
        },
    };

    private static readonly Dictionary<string, DiseaseDef> ById = Diseases.ToDictionary(d => d.Id);
    public static DiseaseDef? Find(string id) => ById.TryGetValue(id, out var d) ? d : null;
}

/// <summary>A test the doctor can order. Taking it costs simulated time, and the result reflects the patient's real state.</summary>
public sealed record Investigation(string Name, string Group, double Seconds, Func<Simulation, string> Run);

public static class Lab
{
    private static string Positive(bool b) => b ? "POSITIVE" : "negative";

    public static readonly Investigation[] All =
    {
        new("Vital signs & pulse oximetry", "Bedside", 10, s =>
        {
            var v = s.Vitals;
            return $"HR {v.HeartRate:0} bpm | BP {v.Systolic:0}/{v.Diastolic:0} mmHg | SpO2 {v.SpO2:0}% | RR {v.RespRate:0}/min | Temp {v.Temperature:0.0} °C | State: {s.SystemState}";
        }),
        new("12-lead ECG", "Bedside", 15, s =>
        {
            var v = s.Vitals;
            if (s.IsFibrillating) return "Ventricular fibrillation - chaotic rhythm, no pulse. SHOCKABLE: CPR + defibrillation.";
            if (v.HeartRate < 1) return "Asystole - flat line. Not shockable.";
            var parts = new List<string>
            {
                v.HeartRate > 100 ? $"Sinus tachycardia {v.HeartRate:0} bpm" :
                v.HeartRate < 50 ? $"Sinus bradycardia {v.HeartRate:0} bpm" : $"Normal sinus rhythm {v.HeartRate:0} bpm",
            };
            if (s.DiseaseSeverity("mi") > 10) parts.Add("ST-segment elevation in the anterior leads (acute heart attack)");
            if (s.DiseaseSeverity("pe") > 10) parts.Add("signs of right-heart strain");
            if (s.DiseaseSeverity("htn") > 10) parts.Add("left-ventricular hypertrophy pattern");
            return string.Join("; ", parts);
        }),
        new("Blood glucose", "Bedside", 10, s =>
        {
            double g = s.DiseaseSeverity("dka") > 10 ? 8 + s.DiseaseSeverity("dka") * 0.25 : s.Vitals.Energy < 30 ? 2.4 : 5.2;
            return $"{g:0.0} mmol/L" + (g > 11 ? " - very high" : g < 3.5 ? " - LOW (hypoglycaemia)" : " - normal range");
        }),
        new("Malaria rapid test", "Bedside", 20, s => $"Malaria antigen {Positive(s.DiseaseSeverity("malaria") > 3)}"),

        new("Full blood count", "Blood tests", 25, s =>
        {
            var v = s.Vitals;
            double marrow = s.Get("Bone Marrow").Efficiency / 100.0;
            double wbc = Math.Max(0.5, 7 + v.Infection * 0.15 - (1 - marrow) * 5 - s.DiseaseSeverity("hiv") * 0.03);
            return $"Hemoglobin {14 * v.Hemoglobin / 100.0:0.0} g/dL ({v.Hemoglobin:0}% of normal) | WBC {wbc:0.0} x10^9/L" +
                   (wbc > 12 ? " (raised - infection)" : wbc < 4 ? " (low - weak immunity)" : "");
        }),
        new("Arterial blood gas", "Blood tests", 20, s =>
        {
            var v = s.Vitals;
            double vent = v.RespRate * v.TidalVolume / (Physiology.RestingRespRate * Physiology.TidalVolumeMl);
            double pco2 = Math.Min(100, Math.Max(20, 40 / Math.Max(0.3, vent)));
            double hco3 = Math.Max(5, 24 - v.Acid * 0.15);
            return $"pH {v.Ph:0.00} | pCO2 {pco2:0} mmHg | HCO3 {hco3:0} mmol/L | SpO2 {v.SpO2:0}% | Lactate {1 + v.Acid * 0.08:0.0} mmol/L";
        }),
        new("Urea & creatinine", "Blood tests", 25, s =>
        {
            var v = s.Vitals;
            double creat = 0.9 + (1 - Math.Min(1.0, v.Gfr / Physiology.NormalGfr)) * 4;
            return $"Creatinine {creat:0.0} mg/dL | Urea {5 + v.Toxins * 0.3:0.0} mmol/L | GFR {v.Gfr:0} mL/min";
        }),
        new("Liver function (ALT)", "Blood tests", 25, s =>
        {
            double damage = 100 - s.Get("Liver").Efficiency;
            return $"ALT {25 + damage * 8:0} U/L" + (damage > 15 ? " - raised (liver injury)" : " - normal");
        }),
        new("Troponin (heart muscle damage)", "Blood tests", 25, s =>
        {
            double t = 0.01 + s.DiseaseSeverity("mi") * 0.2 + s.Get("Heart").Injury * 0.1;
            return $"Troponin {t:0.00} ng/mL" + (t > 0.04 ? " - RAISED (heart muscle injury)" : " - normal");
        }),
        new("CRP (inflammation)", "Blood tests", 20, s => $"CRP {3 + s.Vitals.Infection * 3:0} mg/L" + (s.Vitals.Infection > 15 ? " - raised (infection)" : " - normal")),
        new("Thyroid function (TSH)", "Blood tests", 25, s =>
            s.DiseaseSeverity("hypothyroid") > 5 || s.Get("Thyroid").Efficiency < 70 ? "TSH HIGH, free T4 low - underactive thyroid" : "TSH normal"),
        new("Ferritin (iron stores)", "Blood tests", 25, s => s.DiseaseSeverity("anemia") > 5 ? "Ferritin LOW - iron deficiency" : "Ferritin normal"),
        new("HIV test", "Blood tests", 25, s => $"HIV antibody {Positive(s.DiseaseSeverity("hiv") > 3)}"),

        new("Chest X-ray", "Imaging", 30, s =>
        {
            if (s.DiseaseSeverity("pneumonia") > 10) return "Dense consolidation in the lower lobe (pneumonia)";
            if (s.DiseaseSeverity("tb") > 10) return "Upper-lobe infiltrates with cavities (tuberculosis)";
            if (s.DiseaseSeverity("asthma") > 10) return "Hyper-inflated lungs";
            if (s.DiseaseSeverity("pe") > 10) return "Wedge-shaped opacity / often near-normal early (embolism)";
            return "Clear lung fields, normal heart size";
        }),
        new("CT head", "Imaging", 40, s =>
        {
            if (s.DiseaseSeverity("stroke") > 10) return "Hypodense area in the middle cerebral artery territory (ischaemic stroke)";
            if (s.DiseaseSeverity("meningitis") > 20) return "No mass; meningeal enhancement";
            if (s.Get("Cerebrum").Injury > 30) return "Diffuse hypoxic brain injury";
            return "Normal";
        }),

        new("Sputum GeneXpert (TB)", "Microbiology", 40, s => $"Mycobacterium tuberculosis {Positive(s.DiseaseSeverity("tb") > 3)}"),
        new("Lumbar puncture (CSF)", "Microbiology", 45, s =>
            s.DiseaseSeverity("meningitis") > 10 ? "Cloudy CSF, many white cells, high protein, low glucose - bacterial meningitis" : "Clear CSF, normal cells"),
    };
}

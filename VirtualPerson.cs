using System;
using System.Collections.Generic;
using System.Linq;
using VirtualMan.Interfaces;
using VirtualMan.Systems;

namespace VirtualMan;

public class VirtualPerson
{
    private readonly List<IBodySystem> _systems =
    [
        new CirculatorySystem(),
        new RespiratorySystem(),
        new DigestiveSystem(),
        new ExcretorySystem(),
        new NervousSystem(),
        new LocomotorSystem()
    ];

    public IReadOnlyList<IBodySystem> Systems => _systems.AsReadOnly();
    public bool IsAlive { get; private set; } = true;
    public string LastLog { get; private set; } = "System Initialized. All parameters nominal.";

    public void RunTick()
    {
        if (!IsAlive) return;
        try
        {
            // 1. Tick all individual systems and organs
            foreach (var system in _systems) system.UpdateSystem();

            // 2. FETCH KEY ORGANS FOR CAUSALITY CHECK
            var heart = FindOrgan("Heart") as HeartOrgan;
            var lungs = FindOrgan("Lungs");
            var liver = FindOrgan("Liver") as LiverOrgan;
            var kidneys = FindOrgan("Kidneys") as KidneyOrgan;
            var pancreas = FindOrgan("Pancreas") as PancreasOrgan;
            var stomach = FindOrgan("Stomach");
            var ear = FindOrgan("Ear");

            // --- PHYSIOLOGICAL RULE 1: DIGESTIVE & SENSORY STRAIN ON ENERGY ---
            if (ear != null && !ear.IsFunctional && pancreas != null)
            {
                pancreas.EnergyReserve = Math.Max(0, pancreas.EnergyReserve - 10);
            }
            if (stomach != null && !stomach.IsFunctional && pancreas != null)
            {
                pancreas.EnergyReserve = Math.Max(0, pancreas.EnergyReserve - 15);
            }

            // --- PHYSIOLOGICAL RULE 2: HEPATIC & RENAL TOXICITY (Uremic Shock) ---
            int totalToxins = (liver?.ToxicityLevel ?? 0) + (kidneys?.ToxinLevel ?? 0);
            if (totalToxins >= 100 && heart != null && heart.IsFunctional)
            {
                heart.Crash();
                LastLog = "CASCADE ALERT: Severe Hepatic/Renal Uremia induced a Secondary Cardiac Arrest!";
                IsAlive = false;
                return;
            }

            // --- PHYSIOLOGICAL RULE 3: MODS (Multiple Organ Dysfunction Syndrome) ---
            // --- PHYSIOLOGICAL RULE 3: MODS (Multiple Organ Dysfunction Syndrome) ---
            if ((heart != null && !heart.IsFunctional) || (lungs != null && !lungs.IsFunctional))
            {
                foreach (var organ in _systems.SelectMany(s => s.Organs))
                {
                    organ.Health = 0;
                }
                IsAlive = false;

                // Declare failReason clearly here
                string failReason = (heart != null && !heart.IsFunctional)
                ? "Circulatory Collapse (Heart)"
                : "Respiratory Arrest (Lungs)";

                LastLog = $"FATAL [MODS]: {failReason} caused 0% tissue perfusion across all systems.";
                return;
            }

            // 3. Evaluate Locomotor Gait
            var locomotor = _systems.OfType<LocomotorSystem>().FirstOrDefault();
            string gait = locomotor?.EvaluateWalk(_systems) ?? "Normal";

            if (!LastLog.Contains("CASCADE ALERT"))
            {
                LastLog = $"Tick processed. Systemic State: Nominal | Gait: {gait}";
            }
        }
        catch (Exception ex)
        {
            IsAlive = false;
            LastLog = $"[FATAL EXCEPTION]: {ex.Message}";
        }
    }

    public IOrgan? FindOrgan(string name) =>
        _systems.SelectMany(s => s.Organs).FirstOrDefault(o => o.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public void Reset()
    {
        IsAlive = true;
        foreach (var organ in _systems.SelectMany(s => s.Organs)) organ.Restore();
        
        var pancreas = FindOrgan("Pancreas") as PancreasOrgan;
        if (pancreas != null) pancreas.EnergyReserve = 100;
        
        var kidneys = FindOrgan("Kidneys") as KidneyOrgan;
        if (kidneys != null) kidneys.ToxinLevel = 0;

        var liver = FindOrgan("Liver") as LiverOrgan;
        if (liver != null) liver.ToxicityLevel = 0;

        LastLog = "All body systems and organs fully restored to baseline.";
    }
}

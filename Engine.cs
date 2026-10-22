using System;
using System.Collections.Generic;
using System.Linq;

namespace VirtualMan;

// ==========================================
// 1. CORE INTERFACES
// ==========================================
public interface IOrgan
{
    string Name { get; }
    bool IsVital { get; }
    int Health { get; set; }
    bool IsFunctional => Health > 0;
    void Tick();
    void Crash();
    void Restore();
}

public interface IBodySystem
{
    string SystemName { get; }
    bool IsSystemCritical { get; }
    IReadOnlyList<IOrgan> Organs { get; }
    void UpdateSystem();
    bool IsOperatingNormally();
}

// ==========================================
// 2. BASE ORGAN
// ==========================================
public abstract class BaseOrgan(string name, bool isVital) : IOrgan
{
    public string Name { get; } = name;
    public bool IsVital { get; } = isVital;
    public int Health { get; set; } = 100;
    public bool IsFunctional => Health > 0;

    public virtual void Crash()
    {
        Health = 0;
    }

    public virtual void Restore()
    {
        Health = 100;
    }

    abstract public void Tick();
}

// ==========================================
// 3. SPECIALIZED ORGANS & SYSTEMS
// ==========================================

// --- Circulatory System ---
public class HeartOrgan() : BaseOrgan("Heart", isVital: true)
{
    public override void Tick()
    {
        if (!IsFunctional)
            throw new Exception("FATAL [Cardiogenic Shock]: Heart stopped pumping. Complete circulatory collapse.");
    }
}

public class CirculatorySystem : IBodySystem
{
    public string SystemName => "Circulatory System";
    public bool IsSystemCritical => true;
    private readonly List<IOrgan> _organs = [new HeartOrgan()];
    public IReadOnlyList<IOrgan> Organs => _organs.AsReadOnly();
    public void UpdateSystem() { foreach (var o in _organs) o.Tick(); }
    public bool IsOperatingNormally() => _organs.TrueForAll(o => o.IsFunctional);
}

// --- Respiratory System ---
public class NoseOrgan() : BaseOrgan("Nose", isVital: false) { public override void Tick() { } }
public class LungOrgan() : BaseOrgan("Lungs", isVital: true)
{
    public override void Tick()
    {
        if (!IsFunctional) throw new Exception("FATAL [Respiratory Arrest]: Oxygen exchange ceased. Asphyxiation.");
    }
}

public class RespiratorySystem : IBodySystem
{
    public string SystemName => "Respiratory System";
    public bool IsSystemCritical => true;
    private readonly List<IOrgan> _organs = [new NoseOrgan(), new LungOrgan()];
    public IReadOnlyList<IOrgan> Organs => _organs.AsReadOnly();
    public void UpdateSystem() { foreach (var o in _organs) o.Tick(); }
    public bool IsOperatingNormally() => _organs.TrueForAll(o => o.IsFunctional);
}

// --- Digestive & Metabolic System (All Parts Included) ---
public class MouthOrgan : BaseOrgan { public MouthOrgan() : base("Mouth", isVital: false) { } override public void Tick() { } }
public class TeethOrgan : BaseOrgan { public TeethOrgan() : base("Teeth", isVital: false) { } override public void Tick() { } }
public class EsophagusOrgan : BaseOrgan { public EsophagusOrgan() : base("Esophagus", isVital: false) { } override public void Tick() { } }
public class StomachOrgan : BaseOrgan { public StomachOrgan() : base("Stomach", isVital: false) { } override public void Tick() { } }
public class IntestinesOrgan : BaseOrgan { public IntestinesOrgan() : base("Intestines", isVital: false) { } override public void Tick() { } }
public class AnusOrgan : BaseOrgan { public AnusOrgan() : base("Anus", isVital: false) { } override public void Tick() { } }

public class LiverOrgan : BaseOrgan
{
    public int ToxicityLevel { get; set; } = 0;
    public LiverOrgan() : base("Liver", isVital: false) { }
    override public void Tick()
    {
        if (!IsFunctional) ToxicityLevel = Math.Min(100, ToxicityLevel + 15);
    }
    public override void Restore()
    {
        base.Restore();
        ToxicityLevel = 0;
    }
}

public class PancreasOrgan : BaseOrgan
{
    public int EnergyReserve { get; set; } = 100;
    public PancreasOrgan() : base("Pancreas", isVital: false) { }
    public override void Crash() { base.Crash(); EnergyReserve = 0; }
    public override void Restore() { base.Restore(); EnergyReserve = 100; }
    override public void Tick() { }
}

public class DigestiveSystem : IBodySystem
{
    public string SystemName => "Digestive System";
    public bool IsSystemCritical => false;

    // Registering ALL parts of the digestive system here so they display on the UI category
    private readonly List<IOrgan> _organs =
    [
        new MouthOrgan(),
        new TeethOrgan(),
        new EsophagusOrgan(),
        new StomachOrgan(),
        new IntestinesOrgan(),
        new AnusOrgan(),
        new LiverOrgan(),
        new PancreasOrgan()
    ];

    public IReadOnlyList<IOrgan> Organs => _organs.AsReadOnly();
    public void UpdateSystem() { foreach (var o in _organs) o.Tick(); }
    public bool IsOperatingNormally() => _organs.TrueForAll(o => o.IsFunctional);
}

// --- Excretory System ---
public class KidneyOrgan() : BaseOrgan("Kidneys", isVital: false)
{
    public int ToxinLevel { get; set; } = 0;
    public override void Crash() { base.Crash(); ToxinLevel = 50; }
    public override void Restore() { base.Restore(); ToxinLevel = 0; }
    public override void Tick()
    {
        if (!IsFunctional) ToxinLevel = Math.Min(100, ToxinLevel + 20);
    }
}

public class ExcretorySystem : IBodySystem
{
    public string SystemName => "Excretory System";
    public bool IsSystemCritical => false;
    private readonly List<IOrgan> _organs = [new KidneyOrgan()];
    public IReadOnlyList<IOrgan> Organs => _organs.AsReadOnly();
    public void UpdateSystem() { foreach (var o in _organs) o.Tick(); }
    public bool IsOperatingNormally() => _organs.TrueForAll(o => o.IsFunctional);
}

// --- Nervous System ---
public class EarOrgan() : BaseOrgan("Ear", isVital: false) { public override void Tick() { } }
public class BrainstemOrgan() : BaseOrgan("Brainstem", isVital: true)
{
    public override void Tick()
    {
        if (!IsFunctional) throw new Exception("FATAL [Central Brain Death]: Brainstem core offline.");
    }
}

public class NervousSystem : IBodySystem
{
    public string SystemName => "Nervous System";
    public bool IsSystemCritical => true;
    private readonly List<IOrgan> _organs = [new BrainstemOrgan(), new EarOrgan()];
    public IReadOnlyList<IOrgan> Organs => _organs.AsReadOnly();
    public void UpdateSystem() { foreach (var o in _organs) o.Tick(); }
    public bool IsOperatingNormally() => _organs.TrueForAll(o => o.IsFunctional);
}

// --- Locomotor System & Causality Evaluator ---
public class LegOrgan() : BaseOrgan("Legs", isVital: false) { public override void Tick() { } }

public class LocomotorSystem : IBodySystem
{
    public string SystemName => "Locomotor System";
    public bool IsSystemCritical => false;
    private readonly List<IOrgan> _organs = [new LegOrgan()];
    public IReadOnlyList<IOrgan> Organs => _organs.AsReadOnly();
    public void UpdateSystem() { }
    public bool IsOperatingNormally() => _organs.TrueForAll(o => o.IsFunctional);

    public string EvaluateWalk(List<IBodySystem> systems)
    {
        var ear = systems.SelectMany(s => s.Organs).OfType<EarOrgan>().FirstOrDefault();
        var pancreas = systems.SelectMany(s => s.Organs).OfType<PancreasOrgan>().FirstOrDefault();
        var kidneys = systems.SelectMany(s => s.Organs).OfType<KidneyOrgan>().FirstOrDefault();

        // Cross-system cascading drains
        if (ear != null && !ear.IsFunctional && pancreas != null)
        {
            pancreas.EnergyReserve = Math.Max(0, pancreas.EnergyReserve - 15);
        }

        if (kidneys != null && kidneys.ToxinLevel >= 80)
            return "CRITICAL [Uremic Seizure]: Severe Spastic Gait (Extreme Toxicity)";
        if (ear != null && !ear.IsFunctional && pancreas?.EnergyReserve < 20)
            return "DEGRADED [Vertigo Exhaustion]: Severe Balance & Energy Collapse";
        if (ear != null && !ear.IsFunctional)
            return "Impaired [Ataxia]: Walking with Limp (Vestibular Balance Offline)";
        if (pancreas != null && !pancreas.IsFunctional)
            return "Impaired [Hypoglycemia]: Slow / Lethargic Gait";
        if (kidneys != null && kidneys.ToxinLevel > 40)
            return "Impaired [Mild Uremia]: Unsteady Dizzy Gait";

        return "Normal Nominal Gait";
    }
}

using System;
using System.Collections.Generic;
using VirtualMan.Core;
using VirtualMan.Interfaces;

namespace VirtualMan.Systems;

// --- Individual Anatomical Organs of the Digestive Tract ---
public class MouthOrgan : BaseOrgan 
{ 
    public MouthOrgan() : base("Mouth", isVital: false) { }
    public override void Tick() { } 
}

public class TeethOrgan : BaseOrgan 
{ 
    public TeethOrgan() : base("Teeth", isVital: false) { }
    public override void Tick() { } 
}

public class EsophagusOrgan : BaseOrgan 
{ 
    public EsophagusOrgan() : base("Esophagus", isVital: false) { }
    public override void Tick() { } 
}

public class StomachOrgan : BaseOrgan 
{ 
    public StomachOrgan() : base("Stomach", isVital: false) { }
    public override void Tick() { } 
}

public class IntestinesOrgan : BaseOrgan 
{ 
    public IntestinesOrgan() : base("Intestines", isVital: false) { }
    public override void Tick() { } 
}

public class AnusOrgan : BaseOrgan 
{ 
    public AnusOrgan() : base("Anus", isVital: false) { }
    public override void Tick() { } 
}

public class LiverOrgan : BaseOrgan 
{
    public int ToxicityLevel { get; set; } = 0;
    public LiverOrgan() : base("Liver", isVital: false) { }
    
    public override void Tick() 
    {
        // Cascading logic: If liver fails, toxins accumulate
        if (!IsFunctional) 
            ToxicityLevel = Math.Min(100, ToxicityLevel + 15);
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
    
    public override void Crash() 
    { 
        base.Crash(); 
        EnergyReserve = 0; 
    }
    
    public override void Restore() 
    { 
        base.Restore(); 
        EnergyReserve = 100; 
    }
    
    public override void Tick() { }
}

// --- The Master Digestive System Group ---
public class DigestiveSystem : IBodySystem
{
    public string SystemName => "Digestive System";
    public bool IsSystemCritical => false;

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

    public void UpdateSystem() 
    { 
        foreach (var organ in _organs) 
            organ.Tick(); 
    }

    public bool IsOperatingNormally() => _organs.TrueForAll(o => o.IsFunctional);
}
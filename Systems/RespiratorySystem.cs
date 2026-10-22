using System;
using System.Collections.Generic;
using VirtualMan.Core;
using VirtualMan.Interfaces;

namespace VirtualMan.Systems;

public class NoseOrgan() : BaseOrgan("Nose", isVital: false)
{
    public override void Tick() { }
}

public class LungOrgan() : BaseOrgan("Lungs", isVital: true)
{
    public override void Tick()
    {
        if (!IsFunctional)
            throw new Exception("FATAL: Respiratory failure! Lung collapse.");
    }
}

public class RespiratorySystem : IBodySystem
{
    public string SystemName => "Respiratory System";
    public bool IsSystemCritical => true;
    private readonly List<IOrgan> _organs = [new NoseOrgan(), new LungOrgan()];
    public IReadOnlyList<IOrgan> Organs => _organs.AsReadOnly();

    public void UpdateSystem()
    {
        foreach (var o in _organs) o.Tick();
    }

    public bool IsOperatingNormally() => _organs.TrueForAll(o => o.IsFunctional);
}

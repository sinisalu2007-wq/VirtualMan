using System;
using System.Collections.Generic;
using VirtualMan.Core;
using VirtualMan.Interfaces;

namespace VirtualMan.Systems;

public class EarOrgan() : BaseOrgan("Ear", isVital: false)
{
    public override void Tick() { }
}

public class BrainstemOrgan() : BaseOrgan("Brainstem", isVital: true)
{
    public override void Tick()
    {
        if (!IsFunctional)
            throw new Exception("FATAL: Central nervous system panic! Brainstem offline.");
    }
}

public class NervousSystem : IBodySystem
{
    public string SystemName => "Nervous System";
    public bool IsSystemCritical => true;
    private readonly List<IOrgan> _organs = [new BrainstemOrgan(), new EarOrgan()];
    public IReadOnlyList<IOrgan> Organs => _organs.AsReadOnly();

    public void UpdateSystem()
    {
        foreach (var o in _organs) o.Tick();
    }

    public bool IsOperatingNormally() => _organs.TrueForAll(o => o.IsFunctional);
}

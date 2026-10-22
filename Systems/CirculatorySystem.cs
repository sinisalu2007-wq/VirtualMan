using System;
using System.Collections.Generic;
using VirtualMan.Core;
using VirtualMan.Interfaces;

namespace VirtualMan.Systems;

public class HeartOrgan() : BaseOrgan("Heart", isVital: true)
{
    public override void Tick()
    {
        if (!IsFunctional)
            throw new Exception("FATAL: Circulatory collapse! Heart stopped pumping blood.");
    }
}

public class CirculatorySystem : IBodySystem
{
    public string SystemName => "Circulatory System";
    public bool IsSystemCritical => true;
    private readonly List<IOrgan> _organs = [new HeartOrgan()];
    public IReadOnlyList<IOrgan> Organs => _organs.AsReadOnly();

    public void UpdateSystem()
    {
        foreach (var o in _organs) o.Tick();
    }

    public bool IsOperatingNormally() => _organs.TrueForAll(o => o.IsFunctional);
}

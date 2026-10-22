using System.Collections.Generic;
using VirtualMan.Core;
using VirtualMan.Interfaces;

namespace VirtualMan.Systems;

public class KidneyOrgan() : BaseOrgan("Kidneys", isVital: false)
{
    public int ToxinLevel { get; set; } = 0;

    public override void Crash()
    {
        base.Crash();
        ToxinLevel = 85;
    }

    public override void Restore()
    {
        base.Restore();
        ToxinLevel = 0;
    }

    public override void Tick()
    {
        if (!IsFunctional) ToxinLevel += 5;
    }
}

public class ExcretorySystem : IBodySystem
{
    public string SystemName => "Excretory System";
    public bool IsSystemCritical => false;
    private readonly List<IOrgan> _organs = [new KidneyOrgan()];
    public IReadOnlyList<IOrgan> Organs => _organs.AsReadOnly();

    public void UpdateSystem()
    {
        foreach (var o in _organs) o.Tick();
    }

    public bool IsOperatingNormally() => _organs.TrueForAll(o => o.IsFunctional);
}

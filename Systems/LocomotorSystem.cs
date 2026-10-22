using System.Collections.Generic;
using System.Linq;
using VirtualMan.Core;
using VirtualMan.Interfaces;

namespace VirtualMan.Systems;

public class LegOrgan() : BaseOrgan("Legs", isVital: false)
{
    public override void Tick() { }
}

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

        if (ear != null && !ear.IsFunctional) 
            return "Walking with Limp (Ear/Vestibular Failure)";
    
        if (pancreas != null && !pancreas.IsFunctional) 
            return "Lethargic / Slow Walk (Pancreas Energy Drop)";
    
        if (kidneys != null && kidneys.ToxinLevel > 50) 
            return "Unsteady Walk (High Blood Toxicity)";
    
        return "Normal Gait";
    }
}

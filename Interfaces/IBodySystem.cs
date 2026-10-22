using System.Collections.Generic;

namespace VirtualMan.Interfaces;

public interface IBodySystem
{
    string SystemName { get; }
    bool IsSystemCritical { get; }
    IReadOnlyList<IOrgan> Organs { get; }
    void UpdateSystem();
    bool IsOperatingNormally();
}

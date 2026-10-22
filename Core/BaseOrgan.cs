using System;
using VirtualMan.Interfaces;

namespace VirtualMan.Core;

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

    public abstract void Tick();
}

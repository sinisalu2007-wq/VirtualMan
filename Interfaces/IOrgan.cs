namespace VirtualMan.Interfaces;

public interface IOrgan
{
    string Name { get; }
    bool IsVital { get; }
    int Health { get; }
    bool IsFunctional => Health > 0;
    void Tick();
    void Crash();
    void Restore();
}

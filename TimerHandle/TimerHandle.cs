using System;

namespace Timers;

/// <summary>
/// Identifies a timer of a <see cref="TimerManager"/>. The generation makes a stale handle (of a stopped or finished
/// timer) unable to touch a newer timer that reuses the same slot. <see cref="IsValid"/> only means "not None";
/// ask the manager whether the timer is still running.
/// </summary>
public readonly struct TimerHandle : IEquatable<TimerHandle>
{
    public static readonly TimerHandle None = default;

    public readonly int Slot;
    public readonly int Generation;

    internal TimerHandle(int slot, int generation)
    {
        Slot = slot;
        Generation = generation;
    }

    public bool IsValid => Generation != 0;

    public bool Equals(TimerHandle other) => Slot == other.Slot && Generation == other.Generation;

    public override bool Equals(object obj) => obj is TimerHandle other && Equals(other);

    public override int GetHashCode() => (Slot * 397) ^ Generation;

    public static bool operator ==(TimerHandle a, TimerHandle b) => a.Equals(b);

    public static bool operator !=(TimerHandle a, TimerHandle b) => !a.Equals(b);

    public override string ToString() => IsValid ? $"Timer {Slot}:{Generation}" : "Timer None";
}

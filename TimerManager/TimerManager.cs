using System;
using Godot;

namespace Timers;

/// <summary>
/// Game time timers with a static API: <c>TimerManager.Start(2f, OnDone)</c>. The autoload added by the Timers plugin
/// advances them every frame; it is pausable, so timers stop while the scene tree is paused.
/// Timers fire in order of fire time, then start order (a repeating timer keeps its first start order), so of two
/// timers ending together the one started first runs first. During a callback <see cref="Time"/> is that timer's fire
/// time, and timers started there count from it. Repeats fire at first fire time + k × interval, so no error builds up.
/// Timers outlive scenes: stop the ones whose callback belongs to a node when that node leaves the tree.
/// </summary>
public partial class TimerManager : Node
{
    private const int InitialCapacity = 64;

    private static TimerSlot[] _slots = new TimerSlot[InitialCapacity];
    private static int[] _heap = new int[InitialCapacity];
    private static int[] _freeSlots = new int[InitialCapacity];
    private static int _heapCount;
    private static int _freeCount;
    private static int _usedSlots;
    private static long _nextOrder;
    private static bool _advancing;
    private static TimerManager _instance;
    private static bool _warnedNotRunning;

    /// <summary>Game time in seconds advanced by the autoload; it does not move while the tree is paused.</summary>
    public static double Time { get; private set; }

    /// <summary>Running and paused timers.</summary>
    public static int Count { get; private set; }

    /// <summary>
    /// Runs <paramref name="callback"/> after <paramref name="delay"/> seconds, then every <paramref name="interval"/>
    /// seconds if it is above zero. A zero delay fires on the next frame, or right away when started from a callback.
    /// </summary>
    public static TimerHandle Start(float delay, Action callback, float interval = 0f)
    {
        if (callback == null)
        {
            GD.PushError("TimerManager.Start: callback is null.");
            return TimerHandle.None;
        }

        WarnIfNotRunning();

        int slot = AllocateSlot();
        ref TimerSlot timer = ref _slots[slot];
        timer.Callback = callback;
        timer.StartTime = Time + Math.Max(0f, delay);
        timer.FireTime = timer.StartTime;
        timer.Interval = Math.Max(0f, interval);
        timer.RepeatCount = 0;
        timer.Order = _nextOrder++;
        timer.Paused = false;
        timer.Active = true;

        HeapPush(slot);
        Count++;
        return new TimerHandle(slot, timer.Generation);
    }

    public static bool Stop(TimerHandle handle)
    {
        if (!TryGetSlot(handle, out int slot))
            return false;

        if (_slots[slot].HeapIndex >= 0)
            HeapRemoveAt(_slots[slot].HeapIndex);

        Release(slot);
        return true;
    }

    /// <summary>True while running or paused.</summary>
    public static bool IsActive(TimerHandle handle) => TryGetSlot(handle, out _);

    public static bool IsPaused(TimerHandle handle) => TryGetSlot(handle, out int slot) && _slots[slot].Paused;

    /// <summary>Seconds until the next fire; 0 if the timer is not active.</summary>
    public static float GetRemaining(TimerHandle handle)
    {
        if (!TryGetSlot(handle, out int slot))
            return 0f;

        ref TimerSlot timer = ref _slots[slot];
        return timer.Paused ? timer.PausedRemaining : (float)Math.Max(0d, timer.FireTime - Time);
    }

    public static bool Pause(TimerHandle handle)
    {
        if (!TryGetSlot(handle, out int slot) || _slots[slot].Paused)
            return false;

        ref TimerSlot timer = ref _slots[slot];
        timer.PausedRemaining = (float)Math.Max(0d, timer.FireTime - Time);
        timer.Paused = true;
        HeapRemoveAt(timer.HeapIndex);
        return true;
    }

    public static bool Resume(TimerHandle handle)
    {
        if (!TryGetSlot(handle, out int slot) || !_slots[slot].Paused)
            return false;

        ref TimerSlot timer = ref _slots[slot];
        timer.FireTime = Time + timer.PausedRemaining;
        // Shift the repeat origin so later repeats stay one interval apart from the resumed fire.
        timer.StartTime = timer.FireTime - timer.RepeatCount * (double)timer.Interval;
        timer.Paused = false;
        HeapPush(slot);
        return true;
    }

    /// <summary>Stops every timer; all handles become inactive.</summary>
    public static void Clear()
    {
        for (int slot = 0; slot < _usedSlots; slot++)
        {
            if (_slots[slot].Active)
                Release(slot);
        }

        _heapCount = 0;
    }

    public override void _EnterTree()
    {
        if (_instance != null && _instance != this)
        {
            GD.PushError("TimerManager: only one instance may run (the Timers plugin autoload); this one is removed.");
            QueueFree();
            return;
        }

        _instance = this;
    }

    public override void _ExitTree()
    {
        if (_instance == this)
            _instance = null;
    }

    public override void _Process(double delta)
    {
        if (_instance == this)
            Advance((float)delta);
    }

    /// <summary>Moves time forward and fires every due timer in order, each at its own fire time.</summary>
    private static void Advance(float delta)
    {
        if (_advancing)
            return;

        _advancing = true;
        double target = Time + Math.Max(0f, delta);

        try
        {
            while (_heapCount > 0)
            {
                int slot = _heap[0];
                ref TimerSlot timer = ref _slots[slot];
                if (timer.FireTime > target)
                    break;

                Time = Math.Max(Time, timer.FireTime);
                Action callback = timer.Callback;

                // State is written before the callback so it can stop, pause or restart this timer.
                if (timer.Interval > 0f)
                {
                    timer.RepeatCount++;
                    timer.FireTime = timer.StartTime + timer.RepeatCount * (double)timer.Interval;
                    SiftDown(0);
                }
                else
                {
                    HeapRemoveAt(0);
                    Release(slot);
                }

                callback();
            }

            Time = target;
        }
        finally
        {
            // A throwing callback must not leave the manager stuck; the remaining due timers fire next frame.
            _advancing = false;
        }
    }

    private static void WarnIfNotRunning()
    {
        if (_instance != null || _warnedNotRunning || Engine.IsEditorHint())
            return;

        _warnedNotRunning = true;
        GD.PushWarning("TimerManager: no running instance; timers will not fire. Enable the Timers plugin (Project Settings → Plugins).");
    }

    private static bool TryGetSlot(TimerHandle handle, out int slot)
    {
        slot = handle.Slot;
        return handle.IsValid
            && (uint)slot < (uint)_usedSlots
            && _slots[slot].Active
            && _slots[slot].Generation == handle.Generation;
    }

    private static int AllocateSlot()
    {
        if (_freeCount > 0)
            return _freeSlots[--_freeCount];

        if (_usedSlots == _slots.Length)
            Grow();

        int slot = _usedSlots++;
        _slots[slot].Generation = 1;
        _slots[slot].HeapIndex = -1;
        return slot;
    }

    private static void Release(int slot)
    {
        ref TimerSlot timer = ref _slots[slot];
        timer.Active = false;
        timer.Callback = null;
        timer.HeapIndex = -1;
        // Next generation right away, so the old handle stops matching; 0 is reserved for None.
        timer.Generation = timer.Generation == int.MaxValue ? 1 : timer.Generation + 1;
        _freeSlots[_freeCount++] = slot;
        Count--;
    }

    private static void Grow()
    {
        int capacity = _slots.Length * 2;
        Array.Resize(ref _slots, capacity);
        Array.Resize(ref _heap, capacity);
        Array.Resize(ref _freeSlots, capacity);
    }

    private static void HeapPush(int slot)
    {
        int index = _heapCount++;
        Place(index, slot);
        SiftUp(index);
    }

    private static void HeapRemoveAt(int index)
    {
        _slots[_heap[index]].HeapIndex = -1;
        int last = --_heapCount;
        if (index == last)
            return;

        Place(index, _heap[last]);
        SiftUp(index);
        SiftDown(index);
    }

    private static void SiftUp(int index)
    {
        int slot = _heap[index];
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (!Before(slot, _heap[parent]))
                break;

            Place(index, _heap[parent]);
            index = parent;
        }

        Place(index, slot);
    }

    private static void SiftDown(int index)
    {
        int slot = _heap[index];
        while (true)
        {
            int child = index * 2 + 1;
            if (child >= _heapCount)
                break;

            if (child + 1 < _heapCount && Before(_heap[child + 1], _heap[child]))
                child++;

            if (!Before(_heap[child], slot))
                break;

            Place(index, _heap[child]);
            index = child;
        }

        Place(index, slot);
    }

    private static void Place(int index, int slot)
    {
        _heap[index] = slot;
        _slots[slot].HeapIndex = index;
    }

    private static bool Before(int a, int b)
    {
        ref TimerSlot ta = ref _slots[a];
        ref TimerSlot tb = ref _slots[b];
        return ta.FireTime < tb.FireTime || (ta.FireTime == tb.FireTime && ta.Order < tb.Order);
    }

    private struct TimerSlot
    {
        public Action Callback;
        public double StartTime;
        public double FireTime;
        public float Interval;
        public float PausedRemaining;
        public long Order;
        public int RepeatCount;
        public int Generation;
        public int HeapIndex;
        public bool Paused;
        public bool Active;
    }
}

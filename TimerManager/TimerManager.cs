using System;
using Godot;

namespace Timers;

/// <summary>
/// One-shot and repeating timers driven by <see cref="Advance"/>. Timers fire in order of fire time, then start order
/// (a repeating timer keeps its first start order), so of two timers ending together the one started first runs first.
/// During a callback <see cref="Time"/> is that timer's fire time, and timers started there count from it.
/// Repeats fire at first fire time + k × interval, so no error builds up. Main thread only.
/// </summary>
public sealed class TimerManager
{
    private TimerSlot[] _slots;
    private int[] _heap;
    private int[] _freeSlots;
    private int _heapCount;
    private int _freeCount;
    private int _usedSlots;
    private long _nextOrder;
    private bool _advancing;

    public TimerManager(int capacity = 16)
    {
        capacity = Math.Max(1, capacity);
        _slots = new TimerSlot[capacity];
        _heap = new int[capacity];
        _freeSlots = new int[capacity];
    }

    /// <summary>Total advanced time in seconds.</summary>
    public double Time { get; private set; }

    /// <summary>Running and paused timers.</summary>
    public int Count { get; private set; }

    /// <summary>
    /// Runs <paramref name="callback"/> after <paramref name="delay"/> seconds, then every <paramref name="interval"/>
    /// seconds if it is above zero. A zero delay fires on the next <see cref="Advance"/>, or in the current one when
    /// started from a callback.
    /// </summary>
    public TimerHandle Start(float delay, Action callback, float interval = 0f)
    {
        if (callback == null)
        {
            GD.PushError("TimerManager.Start: callback is null.");
            return TimerHandle.None;
        }

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

    public bool Stop(TimerHandle handle)
    {
        if (!TryGetSlot(handle, out int slot))
            return false;

        if (_slots[slot].HeapIndex >= 0)
            HeapRemoveAt(_slots[slot].HeapIndex);

        Release(slot);
        return true;
    }

    /// <summary>True while running or paused.</summary>
    public bool IsActive(TimerHandle handle) => TryGetSlot(handle, out _);

    public bool IsPaused(TimerHandle handle) => TryGetSlot(handle, out int slot) && _slots[slot].Paused;

    /// <summary>Seconds until the next fire; 0 if the timer is not active.</summary>
    public float GetRemaining(TimerHandle handle)
    {
        if (!TryGetSlot(handle, out int slot))
            return 0f;

        ref TimerSlot timer = ref _slots[slot];
        return timer.Paused ? timer.PausedRemaining : (float)Math.Max(0d, timer.FireTime - Time);
    }

    public bool Pause(TimerHandle handle)
    {
        if (!TryGetSlot(handle, out int slot) || _slots[slot].Paused)
            return false;

        ref TimerSlot timer = ref _slots[slot];
        timer.PausedRemaining = (float)Math.Max(0d, timer.FireTime - Time);
        timer.Paused = true;
        HeapRemoveAt(timer.HeapIndex);
        return true;
    }

    public bool Resume(TimerHandle handle)
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

    /// <summary>Moves time forward and fires every due timer in order, each at its own fire time.</summary>
    public void Advance(float delta)
    {
        if (_advancing)
        {
            GD.PushError("TimerManager.Advance called from a timer callback; ignored.");
            return;
        }

        _advancing = true;
        double target = Time + Math.Max(0f, delta);

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
        _advancing = false;
    }

    /// <summary>Stops every timer; all handles become inactive.</summary>
    public void Clear()
    {
        for (int slot = 0; slot < _usedSlots; slot++)
        {
            if (_slots[slot].Active)
                Release(slot);
        }

        _heapCount = 0;
    }

    private bool TryGetSlot(TimerHandle handle, out int slot)
    {
        slot = handle.Slot;
        return handle.IsValid
            && (uint)slot < (uint)_usedSlots
            && _slots[slot].Active
            && _slots[slot].Generation == handle.Generation;
    }

    private int AllocateSlot()
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

    private void Release(int slot)
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

    private void Grow()
    {
        int capacity = _slots.Length * 2;
        Array.Resize(ref _slots, capacity);
        Array.Resize(ref _heap, capacity);
        Array.Resize(ref _freeSlots, capacity);
    }

    private void HeapPush(int slot)
    {
        int index = _heapCount++;
        Place(index, slot);
        SiftUp(index);
    }

    private void HeapRemoveAt(int index)
    {
        _slots[_heap[index]].HeapIndex = -1;
        int last = --_heapCount;
        if (index == last)
            return;

        Place(index, _heap[last]);
        SiftUp(index);
        SiftDown(index);
    }

    private void SiftUp(int index)
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

    private void SiftDown(int index)
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

    private void Place(int index, int slot)
    {
        _heap[index] = slot;
        _slots[slot].HeapIndex = index;
    }

    private bool Before(int a, int b)
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

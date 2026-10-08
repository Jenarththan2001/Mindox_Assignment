# Reflection — EFEM Scheduler Simulation

## What the Simulation Does

Simulates 25 wafers travelling through an Equipment Front End Module (EFEM):
LoadPort 1 → PreHeatBuffer (3 slots, 10s heat) → Chamber 1 or 2 (20s process, 6s pickup window) → LoadPort 2.

A priority-based scheduler (P0–P3) drives a single robot and ensures no wafer misses its pickup window.

Final result: **25 wafers processed, 0 damaged, 507s total time.**

---

## Design Decisions and Simplifications

### 1. Simple Polling Loop (not event-driven)

The simulation advances one second at a time in a `while` loop. Every tick, all component timers advance and the scheduler checks the full state of the system.

```csharp
while (!IsComplete(totalWafers) && SimulatedTime < 2000)
{
    _buffer.Tick(1);
    foreach (var ch in _chambers) ch.Tick(1, SimulatedTime + 1);
    _robot.Tick(1, SimulatedTime + 1, ...);
    SimulatedTime++;
    // damage check, then Decide()
}
```

**Why:** Simple to implement, easy to debug, and sufficient for a small system with a known upper bound of ~500 ticks. Every component state is visible at every second.

**Industry alternative:** A priority event queue (Discrete Event Simulation). Time would jump directly from one event to the next — no wasted ticks between events. This scales far better for large fabs with hundreds of tools and thousands of wafers simulated over days of fab time. The scheduling logic (P0–P3, look-ahead) would be identical; only the time-advance engine would change.

---

### 2. Fixed 3-Second Move Time for All Robot Moves

Every robot dispatch — whether picking from LP1, placing in a PHB slot, or moving between chambers — is assumed to take exactly **3 seconds**.

```csharp
private const int MoveDuration = 3;
```

**Why:** The assignment does not specify per-move travel distances or acceleration curves. A fixed uniform move time keeps the scheduling logic clean and the look-ahead math simple and exact.

**Real world:** An actual EFEM robot has different travel distances between positions. LP1 to S1 is not the same distance as S3 to CH2. A real scheduler would use a travel-time matrix and the look-ahead guard would need to use the specific travel time for each planned move rather than a worst-case constant.

---

### 3. PreHeatBuffer Has a Soft Deadline (No Damage)

A wafer in the PreHeatBuffer is considered "ready" after **10 seconds** of heating. However, it can sit in the slot indefinitely after that — there is no penalty or damage if it waits longer.

```csharp
public bool IsReady => Wafer != null && HeatTimer >= HeatDuration;
// IsReady flips true at 10s and stays true — no expiry timer
```

**Why:** The pre-heat is modelled as a minimum warm-up time, not a tight process window. The robot picks the wafer up when the scheduler has capacity; the wafer just waits at temperature until then.

**Real world:** In practice, leaving a wafer on a heated stage too long can also cause problems (over-oxidation, contamination). A full model would add a maximum hold time. For this simulation the soft deadline keeps the scheduling logic focused on the chamber's hard pickup window.

---

### 4. Damaged Chamber Instantly Resets to Idle

When a wafer is not picked up within the 6-second window, the chamber transitions to `WaferDamaged`. The scheduler detects this on the same tick, increments `DamagedCount`, and immediately calls `ch.Reset()` — returning the chamber to `Idle` with zero recovery time.

```csharp
if (ch.State == ChamberState.WaferDamaged)
{
    DamagedCount++;
    ch.Reset();   // ← chamber back to Idle instantly, ready for next wafer
}
```

The damaged wafer is abandoned — the robot does not go to retrieve it. `IsComplete` accounts for it:

```csharp
(_lp2.WaferCount + DamagedCount) >= totalWafers
```

**Why:** Simplifies the simulation. The focus of the assignment is on the scheduling logic that *prevents* damage, not on recovery procedures.

**Real world:** After a damage event a real chamber would be taken offline for inspection and a cleaning/purge cycle before it can accept the next wafer. The damaged wafer would be physically removed by a handler or technician and logged against the lot record. Recovery time could be tens of minutes.

---

### 5. Tick-Order: Damage Check Before Robot Tick

The damage check runs after chambers tick but before the robot tick. This is important:

```
1. ch.Tick()     — chamber may transition to WaferDamaged
2. Damage check  — DamagedCount++, ch.Reset() → chamber now Idle
3. robot.Tick()  — robot sees correct chamber state (Idle, no wafer)
```

If the robot ticked before the damage check, it could arrive at a chamber in the same tick it damages, scoop the wafer before `Reset()` runs, and the damage would go uncounted. Ordering the damage check first ensures correctness.

---

## FIFO Fix in `GetReadySlot()` — Avoiding Starvation

### What Happened Before the Fix

The original `GetReadySlot()` returned the first ready slot it encountered by iterating the array in order (S1 → S2 → S3):

```csharp
// Before — picks lowest slot index, ignores wafer age
public BufferSlot? GetReadySlot()
{
    return Slots.FirstOrDefault(s => s.IsReady);
}
```

This meant **S1 was always preferred over S2, and S2 always over S3**. If new wafers kept arriving into S1 and S2 and the chambers stayed busy, S3's wafer could sit at temperature being skipped indefinitely. In testing, Slot S3 was bypassed for **456 seconds** — a classic starvation bug where the lowest-priority slot never gets served.

### The Fix — Order by Wafer ID (FIFO)

```csharp
// After — picks the slot whose wafer arrived earliest (lowest ID = oldest)
public BufferSlot? GetReadySlot()
{
    return Slots.Where(s => s.IsReady).OrderBy(s => s.Wafer!.Id).FirstOrDefault();
}
```

Wafer IDs are assigned in order (W1, W2, W3 …), so the lowest ID is always the oldest wafer. This guarantees **first-in, first-out** service across all slots regardless of their slot number — no slot can be skipped forever.

**Why this matters:** Without FIFO, a wafer with a smaller slot index will always win, causing wafers in higher slots to wait arbitrarily long. FIFO ties the scheduling decision to arrival time, which is fair and prevents starvation.

---

## What Worked Well

- The look-ahead guard (`IsSafeToStartTransfer`) successfully protected all 25 wafers — zero damage in the standard run.
- FIFO slot selection (`GetReadySlot` ordered by wafer ID) prevented the starvation bug where S3 was skipped for 456 seconds.
- Episode-based wait counting (`_wasWaiting` flag) correctly reported 12 wait episodes rather than counting every idle tick.

## What Could Be Improved

- Move times could be per-destination using a travel-time matrix.
- Chamber recovery time after damage could be modelled.
- The scheduler picks the first idle chamber (lowest ID) — an EDF improvement would pick the chamber whose deadline is most urgent when both are free simultaneously.
- The event queue approach would make the simulation scale to larger systems without the O(n×T) cost of polling.

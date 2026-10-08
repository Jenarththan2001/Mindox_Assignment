# Design Decisions and Assumptions

This document records every significant decision and assumption made during the design and implementation of the EFEM scheduler simulation. Each entry explains what was chosen, what the alternative was, and why.

---

## Assumptions

Assumptions are things the specification did not define. A value or behaviour was chosen to make the simulation concrete and runnable.

---

### A1 — Fixed 3-Second Move Time for All Robot Positions

**Assumed:** Every robot move (LP1→PHB, PHB→Chamber, Chamber→LP2, any direction) takes exactly **3 seconds**.

**Why:** The specification states the robot moves between stations but does not provide a travel-time matrix or distances. A uniform 3-second value keeps the scheduling logic simple and the look-ahead math exact.

**Real world impact:** An actual EFEM robot has different travel distances between positions. LP1 to S1 is not the same as S3 to CH2. A production scheduler would use a per-destination travel-time table, and the look-ahead guard would use the specific travel time for the planned move rather than a worst-case constant.

---

### A2 — Pre-Heat Buffer Has No Maximum Hold Time (Soft Deadline)

**Assumed:** A wafer that has finished its 10-second heat can remain in the slot indefinitely without being damaged.

**Why:** The specification defines the minimum heat time (10 seconds) but does not mention an upper bound. Treating the PHB as soft-deadline keeps scheduling simpler — the robot collects the wafer when capacity allows.

**Real world impact:** In practice, leaving a wafer on a heated stage too long can cause over-oxidation or contamination. A full model would add a maximum hold time and an expiry state.

---

### A3 — Damaged Chamber Resets to Idle Instantly (No Recovery Time)

**Assumed:** When a wafer is not collected within the 6-second window, the chamber transitions to `WaferDamaged`. The scheduler immediately calls `ch.Reset()` — the chamber returns to `Idle` with zero recovery time.

**Why:** The focus of the simulation is the scheduling logic that *prevents* damage. Modelling a chamber recovery cycle would add complexity without testing the scheduler.

**Real world impact:** After a damage event a real chamber goes offline for inspection and a purge/cleaning cycle before accepting the next wafer. Recovery could take tens of minutes. The damaged wafer is physically removed and logged against the lot.

---

### A4 — Pickup at Exactly t+6s Is Valid

**Assumed:** The damage condition is `postProcessTimer > 6`, not `>= 6`. A pickup that completes at exactly the 6-second boundary is treated as safe.

**Why:** The specification says "within 6 seconds". Inclusive boundary (`> 6`) is the natural reading of "within".

---

### A5 — First Scheduler Decision at t=1

**Assumed:** `SimulatedTime` increments after `Tick()` calls, so the first `Decide()` runs at `SimulatedTime = 1`, not `t = 0`.

**Why:** Ticking first and incrementing after means the log timestamps reflect the moment an action *completes* rather than when it was enqueued. This matches real event logs where a timestamp marks when something happened, not when it was requested.

---

## Design Decisions

Design decisions are choices between two or more valid approaches. Each entry explains what was picked and why the alternative was rejected.

---

### D1 — Polling Loop Over Event-Driven Simulation

**Decision:** The simulation advances one second at a time in a `while` loop. Every tick, all component timers advance and the scheduler checks the full system state.

**Alternative:** A priority event queue (Discrete Event Simulation). Time jumps directly from one event to the next — no ticks between events.

**Why polling was chosen:** Simple to implement, easy to debug, and sufficient for a small system with a known upper bound of ~500 ticks. Every component state is visible at every second with no event queue bookkeeping.

**Trade-off:** The polling loop has O(n × T) cost — it runs every second regardless of whether anything happens. For 25 wafers over ~500 ticks this is negligible. For a large fab simulating thousands of wafers over hours of fab time, the event queue approach is far more scalable.

---

### D2 — Fixed Priority Scheduling (P0–P3) Over EDF or FCFS

**Decision:** The scheduler evaluates four priorities in order every tick. The first one that can fire wins.

| Priority | Action | Reason |
|---|---|---|
| P0 | Place carried wafer | Robot is mid-transfer — always complete it |
| P1 | Pick from chamber | Hard 6s deadline — most urgent |
| P2 | Feed chamber from PHB | Keep bottleneck busy |
| P3 | Load PHB from LP1 | Pipeline next wafer |

**Alternative — FCFS:** Process tasks in arrival order. Rejected — FCFS ignores the chamber's hard deadline entirely.

**Alternative — Pure EDF:** Run the task with the nearest deadline first. Closer to optimal but the robot cannot drop a wafer mid-move (non-preemptive), so full EDF correctness does not apply. The P1 priority achieves the same result for this system.

**Why fixed priority:** The priority order is stable, the logic is transparent, and every decision can be traced in the event log. The hierarchy maps directly to the real-world rule: protect product quality first, maximise throughput second.

---

### D3 — FIFO Slot Selection to Prevent Starvation

**Decision:** `GetReadySlot()` returns the slot whose wafer arrived earliest (lowest wafer ID = oldest wafer):

```csharp
return Slots.Where(s => s.IsReady).OrderBy(s => s.Wafer!.Id).FirstOrDefault();
```

**Before this fix:** `FirstOrDefault()` without ordering always returned S1 over S2 over S3. In testing, Slot S3 was skipped for **456 seconds** — a classic starvation bug.

**Why FIFO:** Wafer IDs are assigned in arrival order, so ordering by ID is ordering by arrival time. FIFO guarantees every slot eventually gets served regardless of slot index. It is the simplest fair scheduling policy.

---

### D4 — Look-Ahead Guard Threshold: Block When `remaining < 4`

**Decision:** `IsSafeToStartTransfer()` blocks any new transfer when a processing chamber has fewer than 4 seconds of processing remaining.

**The math:**
```
Robot arrival at chamber = 9s (3s pick + 3s place + 3s travel)
Chamber deadline         = remaining + 6s window
Block if: 9 > (remaining + 6) - 1  →  remaining < 4
```

**Earlier (rejected) version:** Block if `remaining <= 9` — this ignored the 6-second pickup window. It was too conservative: the robot would idle unnecessarily for 6 extra seconds on every chamber cycle, inflating total simulation time.

**Why `remaining < 4`:** The robot does not need to arrive the instant the chamber finishes — it has 6 seconds. Accounting for the window correctly reduces unnecessary idle time while still guaranteeing the window is never missed. The `SafetyMargin = 1` absorbs tick-boundary edge cases.

---

### D5 — Tick Order: Damage Check Before Robot Tick

**Decision:** Every simulation tick runs in this order:
1. `ch.Tick()` — chamber may transition to `WaferDamaged`
2. Damage check — `DamagedCount++`, `ch.Reset()` → chamber back to `Idle`
3. `robot.Tick()` — robot sees the correct chamber state

**Why this order matters:** If the robot ticked before the damage check, it could arrive at a chamber in the same tick it damages. The robot would pick the wafer before `Reset()` ran and `DamagedCount` would never increment — damage silently missed.

**Discovery:** This bug was confirmed by running the simulation with the guard disabled (`--no-guard`). With the old tick order, the simulator reported 0 damaged wafers even when a wafer clearly missed its window. After reordering, the correct count (1 damaged) was reported.

---

### D6 — Episode-Based Wait Counting

**Decision:** `RobotWaitCount` counts the number of distinct *episodes* where the robot wanted to move but was blocked — not the number of ticks spent waiting.

```csharp
if (wantedToMove)
{
    if (!_wasWaiting) { RobotWaitCount++; _wasWaiting = true; }
}
else { _wasWaiting = false; }
```

**Alternative:** Increment every idle tick. Rejected — a single 30-second blockage would report as 30 waits, making the number meaningless and hard to compare across runs.

**Why episodes:** One episode = one distinct period where the robot had useful work it could not do. This is the meaningful metric: how many times did the pipeline stall, not how long each stall lasted (that is captured separately in `RobotIdleTime`).

---

### D7 — Two-Dispatch Robot Model

**Decision:** Every wafer transfer is two separate robot dispatches: one pick move (3s) followed by one place move (3s). Total transfer time = 6s.

The destination is reserved at pick time via `PlaceTask` and `DestIndex`. Priority 0 reads these to dispatch the place move immediately after the robot becomes idle.

**Alternative:** One dispatch per full transfer (robot handles pick + travel + place internally). Rejected — a single-dispatch model hides intermediate robot state, making it impossible to interrupt for a higher-priority pick mid-transfer. The two-dispatch model keeps the robot's state transparent to the scheduler every tick.

---

### D8 — Scheduler Lives Outside `Models/`

**Decision:** `Scheduler.cs` sits directly in `EFEMSimulator/`, not in `EFEMSimulator/Models/`.

**Why:** `Models/` contains classes that represent physical components (Wafer, LoadPort, Robot, Chamber, PreHeatBuffer). The Scheduler does not model a physical component — it orchestrates all of them. Placing it outside `Models/` makes the separation of concerns visible in the file structure.

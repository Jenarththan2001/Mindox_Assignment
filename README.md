# EFEM Scheduler Simulation — Mindox Techno Assessment

A C# discrete-time simulation of an Equipment Front End Module (EFEM) for semiconductor wafer processing. The system moves 25 wafers from an input load port through a pre-heat buffer and two process chambers, then into an output load port — without damaging a single wafer.

---

## System Flow

```
LP1 (25 wafers) ──► Robot ──► PreHeatBuffer ──► Robot ──► CH1 / CH2 ──► Robot ──► LP2
                              (3 slots, 10s)              (20s process,
                                                           6s pickup window)
```

| Station | Detail |
|---|---|
| LoadPort 1 | Input — 25 wafers queued at start |
| PreHeatBuffer | 3 independent slots, 10s minimum heat, soft deadline |
| Chamber CH1 / CH2 | 20s processing, **6-second hard pickup window** |
| LoadPort 2 | Output — collects processed wafers |
| Robot | Single arm, 3s per move, carries one wafer at a time |

---

## Sequence Diagram

```mermaid
sequenceDiagram
    participant LP1 as LoadPort1
    participant ROB as Robot
    participant PHB as PreHeatBuffer
    participant CH  as Chamber
    participant LP2 as LoadPort2
    participant SCH as Scheduler

    SCH->>ROB: P3 Dispatch: pick from LP1
    ROB->>LP1: Pick wafer (3s)
    SCH->>ROB: P0 Dispatch: place in PHB S1
    ROB->>PHB: Place wafer (3s)
    PHB->>PHB: Heat slot S1 — 10s timer starts

    Note over PHB: 10 seconds...

    SCH->>ROB: P2 Dispatch: pick from PHB S1
    ROB->>PHB: Pick heated wafer (3s)
    SCH->>ROB: P0 Dispatch: place in CH1
    ROB->>CH: Place wafer (3s)
    CH->>CH: Process wafer — 20s timer starts

    Note over CH: 20 seconds...

    CH-->>SCH: DoneAwaitingPickup — 6s window open
    SCH->>ROB: P1 URGENT: pick from CH1
    ROB->>CH: Pick wafer (3s)
    SCH->>ROB: P0 Dispatch: place in LP2
    ROB->>LP2: Place wafer (3s) — COMPLETE
```

---

## Class Diagram

```mermaid
classDiagram
    class Scheduler {
        +int SimulatedTime
        +int RobotIdleTime
        +int RobotWaitCount
        +int DamagedCount
        +void RunSimulation()
        -bool Decide()
        -bool TryPlaceCarriedWafer()
        -bool TryPickFromChamber()
        -bool TryFeedChamber()
        -bool TryLoadPreHeat()
        -bool IsSafeToStartTransfer()
        -void TrackWait()
    }

    class Robot {
        +RobotState State
        +bool IsCarrying
        +Wafer CarriedWafer
        +RobotTask PlaceTask
        +int DestIndex
        +bool IsIdle
        +void Dispatch(task, source, dest)
        +void Tick(seconds, time, ...)
    }

    class Chamber {
        +int Id
        +ChamberState State
        +Wafer Wafer
        +int ProcessingSecondsRemaining
        +bool IsIdle
        +bool NeedsPickup
        +void PlaceWafer(Wafer)
        +Wafer PickupWafer()
        +void Tick(seconds, time)
        +void Reset()
    }

    class PreHeatBuffer {
        +BufferSlot[] Slots
        +bool HasFreeSlot
        +bool HasReadySlot
        +BufferSlot GetFreeSlot()
        +BufferSlot GetReadySlot()
        +void Tick(seconds)
    }

    class BufferSlot {
        +int SlotId
        +Wafer Wafer
        +int HeatTimer
        +bool IsReady
        +void PlaceWafer(Wafer)
        +Wafer PickupWafer()
        +void Tick(seconds)
    }

    class LoadPort {
        +int Id
        +int WaferCount
        +bool HasWafers
        +Wafer TakeWafer()
        +void ReturnWafer(Wafer)
    }

    class Wafer {
        +int Id
        +WaferStatus Status
    }

    Scheduler --> LoadPort
    Scheduler --> Robot
    Scheduler --> PreHeatBuffer
    Scheduler --> Chamber
    PreHeatBuffer --> BufferSlot
    Robot --> Wafer
    BufferSlot --> Wafer
    Chamber --> Wafer
    LoadPort --> Wafer
```

---

## Wafer Pipeline — Gantt Chart

The scheduler keeps multiple wafers in flight simultaneously. The chart below shows the first 4 wafers to illustrate how the pipeline overlaps across the pre-heat buffer and both chambers.

![EFEM Wafer Pipeline — first 4 wafers](docs/assets/gantt_4w_6s.png)

> Each robot transfer = 2 moves × 3s = 6s total. Heating and processing overlap across wafers — this is the pipeline the scheduler maintains.

---

## Scheduling Algorithm

The scheduler runs a fixed-priority decision every tick (P0 → P3, first match wins):

| Priority | Action | Why |
|---|---|---|
| **P0** | Place carried wafer at reserved destination | Robot is mid-transfer — always complete it first |
| **P1** | Pick from chamber (`DoneAwaitingPickup`) | Hard 6s deadline — most urgent |
| **P2** | Pick from PHB → place in free chamber | Keep the bottleneck busy |
| **P3** | Pick from LP1 → place in free PHB slot | Pipeline the next wafer |

**Look-ahead guard (`IsSafeToStartTransfer`):** Before starting any P2 or P3 transfer, the scheduler checks whether a processing chamber will finish before the robot could reach it. A transfer is blocked when `remaining < 4` seconds — the robot needs 9 seconds (3+3+3) to complete a transfer and arrive, while the pickup window adds 6 seconds, giving a safe threshold of 4.

---

## Project Structure

```
Mindox_Assignment/
├── EFEMSimulator/
│   ├── EFEMSimulator.csproj
│   ├── Program.cs                  ← entry point, wires components together
│   ├── Scheduler.cs                ← scheduling engine (P0–P3, look-ahead)
│   └── Models/
│       ├── Wafer.cs                ← wafer identity and status
│       ├── LoadPort.cs             ← input / output wafer store
│       ├── BufferSlot.cs           ← single pre-heat slot with timer
│       ├── PreHeatBuffer.cs        ← manages 3 slots, FIFO slot selection
│       ├── Chamber.cs              ← 20s process, 6s hard pickup window
│       └── Robot.cs                ← 3s moves, two-dispatch transfer model
└── docs/
    ├── design/
    │   └── design-decisions.md     ← all assumptions and design choices
    ├── reflection/
    │   └── reflection.md           ← simulation simplifications and trade-offs
    └── Research/
        └── reading_materials/      ← background theory (wafer fab, EFEM, FSM, scheduling)
```

---

## How to Run

**1. Clone the repository**

```bash
git clone https://github.com/Jenarththan2001/Mindox_Assignment.git
cd Mindox_Assignment
```

**2. Run the simulation**

```bash
cd EFEMSimulator
dotnet run
```

Or open `EFEMSimulator/EFEMSimulator.csproj` in Visual Studio 2022 and press **F5**.

> Requires [.NET 8 SDK](https://dotnet.microsoft.com/download) or later.

---

## Results

```
==============================================
  SIMULATION COMPLETE
==============================================
  Total wafers processed : 25
  Damaged wafers         : 0
  Total simulated time   : 507s
  Robot wait episodes    : 12
  Robot idle time        : 57s
==============================================
```

25 wafers processed, 0 damaged. The look-ahead guard successfully prevented every potential pickup window miss across the full 507-second run.

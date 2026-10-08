# Research: EFEM (Equipment Front End Module)

## What is an EFEM?

An **EFEM** (Equipment Front End Module) is the entry and exit point of a semiconductor process tool. It sits between the cleanroom environment and the process modules (chambers) inside the tool — the logistics hub of a wafer processing machine.

---

## Physical Layout

```mermaid
graph LR
    subgraph EFEM
        LP1["Load Port 1\n25 wafers in"]
        LP2["Load Port 2\n25 wafers out"]
        ROBOT["Atmospheric Robot"]
        PHB["Pre-Heat Buffer\nS1 | S2 | S3"]
    end

    subgraph Process Modules
        CH1[Chamber 1]
        CH2[Chamber 2]
    end

    LP1 -->|pick wafer| ROBOT
    ROBOT -->|place| PHB
    PHB -->|after 10s heat| ROBOT
    ROBOT -->|place| CH1
    ROBOT -->|place| CH2
    CH1 -->|collect within 6s| ROBOT
    CH2 -->|collect within 6s| ROBOT
    ROBOT -->|return| LP2
```

---

## EFEM Components

### Load Ports
- Docking points where FOUPs (wafer cassettes) attach to the tool
- In this simulation: LP1 is the input (25 wafers), LP2 is the output
- Robot picks from LP1 and deposits processed wafers into LP2

### Atmospheric Robot (SCARA Robot)
- Operates at atmospheric pressure
- Can only carry **one wafer at a time**
- In this simulation: every move = 3 seconds

### Pre-Heat Buffer (PHB)
- Warms wafers to process temperature before they enter the chamber
- Prevents thermal shock when a wafer enters the hot chamber
- **3 independent slots** — each slot heats independently (10 seconds)
- **Soft deadline** — a wafer that has finished heating can sit in the slot indefinitely without damage; it simply waits until the robot collects it

### Process Chambers (CH1, CH2)
- Where the actual semiconductor process runs (etching, deposition, etc.)
- One wafer at a time, **20 seconds processing**
- **Hard deadline**: after processing completes the robot must collect within **6 seconds** or the wafer is permanently damaged

---

## EFEM System States

```mermaid
stateDiagram-v2
    [*] --> Idle: System powered on
    Idle --> Running: Start recipe / lot
    Running --> Paused: Hold command
    Paused --> Running: Resume command
    Running --> Faulted: Error / alarm
    Faulted --> Idle: Clear fault
    Running --> Idle: All wafers complete
```

---

## How the EFEM Fits Into a Bigger Tool

```mermaid
flowchart LR
    subgraph Full Tool
        direction LR
        subgraph EFEM
            LP[Load Ports]
            ROB[Atmospheric Robot]
            PHB[Pre-Heat Buffer]
        end
        subgraph VTM[Vacuum Transfer Module]
            VROBOT[Vacuum Robot]
        end
        subgraph PM[Process Modules]
            CH1[Chamber 1]
            CH2[Chamber 2]
        end
    end

    LP --> ROB --> PHB --> ROB --> CH1
    ROB --> CH2
```

> In a real tool the chambers operate under vacuum. The EFEM runs at atmospheric pressure. A Load Lock transitions wafers between the two environments. This simulation models only the EFEM — no vacuum handling.

---

## Scheduler Responsibilities

The EFEM scheduler must:

1. **Track all component states** every tick
2. **Prioritise correctly** — chamber pickup beats new wafer loading
3. **Prevent conflicts** — never move the robot to a busy slot
4. **Respect timing constraints** — the 6-second pickup window is a hard deadline
5. **Maximise throughput** — keep chambers busy, fill pre-heat slots proactively

### Priority Decision Tree (implemented)

```mermaid
flowchart TD
    A{Robot idle?} -- No --> Z[Wait / Retry next tick]
    A -- Yes --> A2{Robot carrying a wafer?}
    A2 -- Yes --> A3[P0: Place wafer at reserved destination\nAlways safe — look-ahead guaranteed it]
    A2 -- No --> B{Chamber in DoneAwaitingPickup?}
    B -- Yes --> C[P1: Pick from chamber\nHard 6s deadline — no guard needed]
    B -- No --> D{PHB slot ready AND free chamber\nAND IsSafeToStartTransfer?}
    D -- Yes --> E[P2: Pick from PHB, place in chamber\nKeep chambers busy]
    D -- No --> F{LP1 has wafers AND free PHB slot\nAND IsSafeToStartTransfer?}
    F -- Yes --> G[P3: Pick from LP1, place in PHB\nPipeline next wafer]
    F -- No --> H[Robot holds position]
```

> `IsSafeToStartTransfer` blocks any transfer when a processing chamber has fewer than 4 seconds remaining — the robot could not reach the chamber within the pickup window. See [04-realtime-scheduling.md](04-realtime-scheduling.md) and [05-system-design.md](05-system-design.md) for the full look-ahead logic.

---

## Key EFEM Metrics

| Metric | What It Measures |
|---|---|
| WPH (Wafers Per Hour) | Primary throughput measure |
| Robot Utilisation | % time robot is moving vs idle |
| Chamber Utilisation | % time chamber is processing |
| Mean Transfer Time | Average time to move one wafer |
| Damage Rate | % wafers damaged (target: 0) |

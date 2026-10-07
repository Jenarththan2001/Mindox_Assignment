# 02 — EFEM (Equipment Front End Module)

## What is an EFEM?

An **EFEM** (Equipment Front End Module) is the entry and exit point of a semiconductor process tool. It sits between the cleanroom fab environment and the process modules (chambers) inside the tool.

Think of it as the **lobby and logistics hub** of a wafer processing machine.

---

## Physical Layout

```mermaid
graph LR
    subgraph EFEM
        LP1["Load Port 1<br/>25 wafers"]
        LP2["Load Port 2<br/>25 wafers"]
        ROBOT["Atmospheric<br/>Robot"]
        PHB["Pre-Heat Buffer<br/>S1 | S2 | S3"]
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
- The **docking points** where FOUPs (wafer cassettes) attach to the tool
- In your simulation: LP1 is the input (25 wafers), LP2 is the output (25 processed wafers)
- Robot picks from LP1; robot drops to LP2

### Atmospheric Robot (SCARA Robot)
- A robotic arm that operates in **atmospheric pressure** (normal air)
- Moves wafers between load ports, pre-heat buffer, and chambers
- Can only carry **one wafer at a time**
- Has an end effector (the blade/paddle that holds the wafer)
- In your simulation: every move = 3 seconds

### Pre-Heat Buffer (PHB)
- Warms wafers to the required process temperature before they enter the chamber
- Prevents thermal shock to the wafer when it enters the hot chamber
- Has multiple independent slots (your simulation: 3 slots — S1, S2, S3)
- Each slot heats independently: 10 seconds per wafer

### Process Chambers (CH1, CH2)
- Where the actual semiconductor process happens (etching, deposition, etc.)
- In your simulation: one wafer at a time, 20 seconds processing
- **Critical**: After processing completes, the robot must collect within 6 seconds or the wafer is damaged

---

## EFEM States — System Level

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
            ROB[Robot]
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

> In a real tool the chambers might be under vacuum. The EFEM operates at atmospheric pressure. A **Load Lock** transitions wafers between atmospheric and vacuum. Your assessment simplifies this — no vacuum, just the EFEM logic.

---

## EFEM Scheduler Responsibilities

The EFEM scheduler must:

1. **Track all component states** at every moment
2. **Prioritise correctly** — chamber pickup > new wafer loading
3. **Prevent conflicts** — never move a robot to a busy slot
4. **Respect timing constraints** — 6-second pickup window is hard
5. **Maximise throughput** — keep the chambers busy, fill pre-heat slots proactively

### Priority Decision Tree

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
    F -- No --> H[IDLE — hold position near chamber]
```

> `IsSafeToStartTransfer` blocks any transfer when a processing chamber has fewer than 4 seconds remaining (i.e. the robot could not reach the chamber within the pickup window). See doc 04 and 05 for the full logic.

---


## Key EFEM Metrics

| Metric | What It Measures |
|---|---|
| WPH (Wafers Per Hour) | Primary throughput measure |
| Robot Utilisation | % time robot is moving vs idle |
| Chamber Utilisation | % time chamber is processing |
| Mean Transfer Time | Average time to move one wafer |
| Damage Rate | % wafers damaged (should be 0) |

---

## What You Need to Remember for the Assessment

1. EFEM = front-end logistics module of a semiconductor tool
2. Your simulation IS an EFEM: LoadPort → Robot → Pre-Heat Buffer → Robot → Chamber → Robot → LoadPort
3. Two chambers (CH1, CH2) means more parallelism = higher throughput
4. The scheduler is the brain that orchestrates all moves
5. Chamber pickup priority is always highest — missing the 6-second window = damaged wafer

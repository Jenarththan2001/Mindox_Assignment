# EFEM Simulator — Mindox Techno Assessment

A C# simulation of an Equipment Front End Module (EFEM) for semiconductor wafer processing.

## System Overview

```
LoadPort1 (25 wafers) → Robot → Pre-Heat Buffer (3 slots, 10s) → Robot → Chamber CH1/CH2 (20s) → Robot → LoadPort2
```

**Rules:**
- Robot carries one wafer at a time, every move = 3 seconds
- Pre-Heat Buffer: 3 independent slots, 10 seconds to heat
- Chamber: 20 seconds processing, **6-second pickup window** after completion

## Project Structure

```
src/EFEMSimulator/     C# console application (.NET 9)
docs/                  Study notes and design documents
```

## How to Run

```bash
cd src/EFEMSimulator
dotnet run
```

Or open `src/EFEMSimulator/EFEMSimulator.csproj` in Visual Studio 2022 and press **F5**.

## Classes

| Class | Responsibility |
|---|---|
| `Wafer` | Tracks wafer ID and status |
| `LoadPort` | Holds and releases wafers |
| `BufferSlot` | Single pre-heat slot with independent timer |
| `PreHeatBuffer` | Manages 3 independent buffer slots |
| `Chamber` | Processes one wafer, enforces 6s pickup window |
| `Robot` | Moves wafers between stations (3s per move) |
| `Scheduler` | Decides what the robot does next every tick |

## Simulation Output

- Full event log of every robot move and state change
- Total simulated time
- Robot wait count (destination occupied)
- Robot idle time
- Damaged wafer count

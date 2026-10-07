# References & Resources

> Track everything read or referred to during this assessment. Add entries as you go.

---

## Background Reading (Assessment Required)

### 1. Semiconductor Wafer Fabrication

| Resource | Type | Status | Notes |
|---|---|---|---|
| [How Semiconductor Manufacturing Works — HowStuffWorks](https://electronics.howstuffworks.com/semiconductor.htm) | Article | | General overview, good starting point |
| [Semiconductor device fabrication — Wikipedia](https://en.wikipedia.org/wiki/Semiconductor_device_fabrication) | Article | | Covers the full process flow |
| [How a CPU is made — YouTube (Asianometry)](https://www.youtube.com/watch?v=d9SWNLZvA8g) | Video | | Visual walkthrough of fab process |

---

### 2. EFEM (Equipment Front End Module)

| Resource | Type | Status | Notes |
|---|---|---|---|
| [EFEM — Wikipedia](https://en.wikipedia.org/wiki/Equipment_front_end_module) | Article | | Definition and component overview |
| [SEMI Standards Overview](https://www.semi.org/en/products-services/standards) | Standards | | Industry interface standards (E84, E87) |
| [Brooks Automation EFEM Overview](https://www.brooks.com) | Product page | | Real EFEM hardware reference |

---

### 3. State Machines in Software

| Resource | Type | Status | Notes |
|---|---|---|---|
| [Finite-state machine — Wikipedia](https://en.wikipedia.org/wiki/Finite-state_machine) | Article | | Core theory |
| [State pattern — Refactoring Guru](https://refactoring.guru/design-patterns/state) | Article | | OOP state pattern with C# example |
| [State machines in C# — blog.devgenius.io](https://blog.devgenius.io/state-machine-in-c-b765ac3c7d5) | Article | | Practical C# implementation |

---

### 4. Real-Time Scheduling

| Resource | Type | Status | Notes |
|---|---|---|---|
| [Real-time computing — Wikipedia](https://en.wikipedia.org/wiki/Real-time_computing) | Article | | Hard vs soft deadlines explained |
| [Earliest deadline first — Wikipedia](https://en.wikipedia.org/wiki/Earliest_deadline_first_scheduling) | Article | | The algorithm behind your scheduler |
| [Discrete-event simulation — Wikipedia](https://en.wikipedia.org/wiki/Discrete-event_simulation) | Article | | Theory behind the 1-second tick loop |

---

## Internal Study Notes (Created for This Assignment)

| File | Topic |
|---|---|
| [01-semiconductor-wafer-fabrication.md](01-semiconductor-wafer-fabrication.md) | What wafer fab is, why timing matters |
| [02-efem-overview.md](02-efem-overview.md) | EFEM components, layout, scheduler responsibilities |
| [03-state-machines.md](03-state-machines.md) | State diagrams for every component, C# examples |
| [04-realtime-scheduling.md](04-realtime-scheduling.md) | Priority logic, idle/wait tracking, simulation loop |
| [05-system-design.md](05-system-design.md) | Class diagram, enums, design decisions, VS setup |

---

## C# / .NET References

| Resource | Type | Notes |
|---|---|---|
| [C# documentation — Microsoft](https://learn.microsoft.com/en-us/dotnet/csharp/) | Official docs | Language reference |
| [Console.WriteLine — Microsoft](https://learn.microsoft.com/en-us/dotnet/api/system.console.writeline) | API docs | For event logging |
| [Enum in C# — Microsoft](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum) | API docs | Used for all state types |
| [switch statement — Microsoft](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/selection-statements) | API docs | Used in Tick() methods |

---

## Tools Used

| Tool | Version | Purpose |
|---|---|---|
| Visual Studio 2022 Community | 17.x | Main IDE for C# development |
| .NET 8 SDK | 8.x | C# runtime and compiler |
| Claude Code (AI assistant) | — | Learning support and code guidance |

---

## Add New Entries Here

> When you read something new, paste the link and a one-line note below.

- 

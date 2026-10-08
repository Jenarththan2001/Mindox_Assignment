namespace EFEMSimulator.Models;

public enum RobotState { Idle, Moving }

public enum RobotTask
{
    None,
    PickFromLP1,
    PlaceInPHB,
    PickFromPHB,
    PlaceInChamber,
    PickFromChamber,
    PlaceInLP2
}

public class Robot
{
    public RobotState State { get; private set; } = RobotState.Idle;
    public bool IsCarrying { get; private set; }
    public Wafer? CarriedWafer { get; private set; }

    private RobotTask _pendingTask = RobotTask.None;
    private int _sourceIndex;
    private int _destIndex;
    private int _moveTicks;

    private const int MoveDuration = 3;

    public bool IsIdle => State == RobotState.Idle;

    // Set after each pick — tells P0 which place task comes next
    public RobotTask PlaceTask { get; private set; } = RobotTask.None;
    public int DestIndex => _destIndex;

    public void Dispatch(RobotTask task, int sourceIndex, int destIndex)
    {
        _pendingTask = task;
        _sourceIndex = sourceIndex;
        _destIndex = destIndex;
        _moveTicks = MoveDuration;
        State = RobotState.Moving;
    }

    public void Tick(int seconds, int simulatedTime,
                     LoadPort lp1, LoadPort lp2,
                     PreHeatBuffer buffer, Chamber[] chambers)
    {
        if (State != RobotState.Moving) return;

        _moveTicks -= seconds;
        if (_moveTicks > 0) return;

        // Move complete — execute the action
        State = RobotState.Idle;
        ExecuteAction(simulatedTime, lp1, lp2, buffer, chambers);
    }

    private void ExecuteAction(int simulatedTime,
                                LoadPort lp1, LoadPort lp2,
                                PreHeatBuffer buffer, Chamber[] chambers)
    {
        switch (_pendingTask)
        {
            case RobotTask.PickFromLP1:
                CarriedWafer = lp1.TakeWafer();
                IsCarrying = true;
                PlaceTask = RobotTask.PlaceInPHB;
                Console.WriteLine($"[t={simulatedTime:D4}s] ROBOT  Picked {CarriedWafer} from LoadPort1");
                break;

            case RobotTask.PlaceInPHB:
                var slot = buffer.Slots[_destIndex - 1];
                slot.PlaceWafer(CarriedWafer!);
                Console.WriteLine($"[t={simulatedTime:D4}s] ROBOT  Placed {CarriedWafer} in PHB Slot S{_destIndex} — heating started");
                CarriedWafer = null;
                IsCarrying = false;
                break;

            case RobotTask.PickFromPHB:
                var pickSlot = buffer.Slots[_sourceIndex - 1];
                CarriedWafer = pickSlot.PickupWafer();
                IsCarrying = true;
                PlaceTask = RobotTask.PlaceInChamber;
                Console.WriteLine($"[t={simulatedTime:D4}s] ROBOT  Picked {CarriedWafer} from PHB Slot S{_sourceIndex}");
                break;

            case RobotTask.PlaceInChamber:
                var chamber = chambers[_destIndex - 1];
                chamber.PlaceWafer(CarriedWafer!);
                Console.WriteLine($"[t={simulatedTime:D4}s] ROBOT  Placed {CarriedWafer} in CH{_destIndex} — processing started");
                CarriedWafer = null;
                IsCarrying = false;
                break;

            case RobotTask.PickFromChamber:
                var pickChamber = chambers[_sourceIndex - 1];
                if (pickChamber.Wafer == null)
                {
                    // Chamber was cleared by damage handler this tick — nothing to pick
                    Console.WriteLine($"[t={simulatedTime:D4}s] ROBOT  CH{_sourceIndex} already cleared (wafer damaged) — pick skipped");
                }
                else
                {
                    CarriedWafer = pickChamber.PickupWafer();
                    IsCarrying = true;
                    PlaceTask = RobotTask.PlaceInLP2;
                    Console.WriteLine($"[t={simulatedTime:D4}s] ROBOT  Picked {CarriedWafer} from CH{_sourceIndex}");
                }
                break;

            case RobotTask.PlaceInLP2:
                lp2.ReturnWafer(CarriedWafer!);
                Console.WriteLine($"[t={simulatedTime:D4}s] ROBOT  Placed {CarriedWafer} in LoadPort2 — COMPLETE");
                CarriedWafer = null;
                IsCarrying = false;
                break;
        }

        _pendingTask = RobotTask.None;
    }

    public override string ToString() =>
        $"Robot [{State}] {(IsCarrying ? $"carrying {CarriedWafer}" : "empty")}";
}

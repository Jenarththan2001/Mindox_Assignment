using EFEMSimulator.Models;

namespace EFEMSimulator;

public class Scheduler
{
    private readonly LoadPort _lp1;
    private readonly LoadPort _lp2;
    private readonly Robot _robot;
    private readonly PreHeatBuffer _buffer;
    private readonly Chamber[] _chambers;

    public int SimulatedTime { get; private set; }
    public int RobotIdleTime { get; private set; }
    public int RobotWaitCount { get; private set; }
    public int DamagedCount { get; private set; }

    private const int SafetyMargin = 1;
    private bool _wasWaiting = false;

    public Scheduler(LoadPort lp1, LoadPort lp2, Robot robot, PreHeatBuffer buffer, Chamber[] chambers)
    {
        _lp1 = lp1;
        _lp2 = lp2;
        _robot = robot;
        _buffer = buffer;
        _chambers = chambers;
    }

    public void RunSimulation()
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("  EFEM SIMULATION START - 25 wafers");
        Console.WriteLine("==============================================\n");

        int totalWafers = _lp1.WaferCount;

        while (!IsComplete(totalWafers) && SimulatedTime < 2000)
        {
            // 1. Advance timers
            _buffer.Tick(1);
            foreach (var ch in _chambers)
                ch.Tick(1, SimulatedTime + 1);

            // 2. Handle damaged chambers BEFORE robot acts
            //    (fixes tick-order bug: chamber damages and robot arrives same tick)
            foreach (var ch in _chambers)
            {
                if (ch.State == ChamberState.WaferDamaged)
                {
                    DamagedCount++;
                    ch.Reset();
                }
            }

            _robot.Tick(1, SimulatedTime + 1, _lp1, _lp2, _buffer, _chambers);

            SimulatedTime++;

            // 3. Scheduler decides next move
            bool dispatched = Decide();

            // 4. Track idle time
            if (_robot.IsIdle && !dispatched)
                RobotIdleTime++;
        }

        if (SimulatedTime >= 2000)
            Console.WriteLine("\nWARNING: Simulation hit time cap - possible deadlock");

        PrintStats(totalWafers);
    }

    private bool IsComplete(int totalWafers)
    {
        return (_lp2.WaferCount + DamagedCount) >= totalWafers;
    }

    private bool Decide()
    {
        if (!_robot.IsIdle) return false;

        // P0 - robot is carrying: place at reserved destination
        if (_robot.IsCarrying)
        {
            _wasWaiting = false;
            return TryPlaceCarriedWafer();
        }

        // P1 - chamber done: pick immediately (hard deadline)
        if (TryPickFromChamber()) { _wasWaiting = false; return true; }

        // P2 - heated wafer ready + free chamber: feed chamber
        if (TryFeedChamber()) { _wasWaiting = false; return true; }

        // P3 - load port has wafers + free PHB slot: pipeline next wafer
        if (TryLoadPreHeat()) { _wasWaiting = false; return true; }

        // Nothing to do - track wait episode
        TrackWait();
        return false;
    }

    // P0
    private bool TryPlaceCarriedWafer()
    {
        Console.WriteLine($"[t={SimulatedTime:D4}s] SCHED  Dispatch: place {_robot.CarriedWafer}");
        _robot.Dispatch(_robot.PlaceTask, sourceIndex: -1, destIndex: _robot.DestIndex);
        return true;
    }

    // P1
    private bool TryPickFromChamber()
    {
        foreach (var ch in _chambers)
        {
            if (ch.NeedsPickup)
            {
                Console.WriteLine($"[t={SimulatedTime:D4}s] SCHED  Dispatch: pick from CH{ch.Id}");
                _robot.Dispatch(RobotTask.PickFromChamber, sourceIndex: ch.Id, destIndex: 0);
                return true;
            }
        }
        return false;
    }

    // P2
    private bool TryFeedChamber()
    {
        if (!IsSafeToStartTransfer()) return false;

        var readySlot = _buffer.GetReadySlot();
        var freeChamber = GetFreeChamber();
        if (readySlot == null || freeChamber == null) return false;

        Console.WriteLine($"[t={SimulatedTime:D4}s] SCHED  Dispatch: pick from PHB S{readySlot.SlotId} -> CH{freeChamber.Id}");
        _robot.Dispatch(RobotTask.PickFromPHB, sourceIndex: readySlot.SlotId, destIndex: freeChamber.Id);
        return true;
    }

    // P3
    private bool TryLoadPreHeat()
    {
        if (!IsSafeToStartTransfer()) return false;

        var freeSlot = _buffer.GetFreeSlot();
        if (freeSlot == null || !_lp1.HasWafers) return false;

        Console.WriteLine($"[t={SimulatedTime:D4}s] SCHED  Dispatch: pick from LP1 -> PHB S{freeSlot.SlotId}");
        _robot.Dispatch(RobotTask.PickFromLP1, sourceIndex: -1, destIndex: freeSlot.SlotId);
        return true;
    }

    private bool IsSafeToStartTransfer()
    {
        int robotArrival = 9; // 3s pick + 3s place + 3s travel to chamber

        foreach (var ch in _chambers)
        {
            if (ch.State != ChamberState.Processing) continue;
            int deadline = ch.ProcessingSecondsRemaining + 6;
            if (robotArrival > deadline - SafetyMargin)
                return false;
        }
        return true;
    }

    private Chamber? GetFreeChamber()
    {
        foreach (var ch in _chambers)
            if (ch.IsIdle) return ch;
        return null;
    }

    private void TrackWait()
    {
        bool wantedToMove = (_lp1.HasWafers && !_buffer.HasFreeSlot) ||
                            (_buffer.HasReadySlot && GetFreeChamber() == null);

        if (wantedToMove)
        {
            if (!_wasWaiting)
            {
                RobotWaitCount++;
                _wasWaiting = true;
            }
        }
        else
        {
            _wasWaiting = false;
        }
    }

    private void PrintStats(int totalWafers)
    {
        Console.WriteLine("\n==============================================");
        Console.WriteLine("  SIMULATION COMPLETE");
        Console.WriteLine("==============================================");
        Console.WriteLine($"  Total wafers processed : {_lp2.WaferCount}");
        Console.WriteLine($"  Damaged wafers         : {DamagedCount}");
        Console.WriteLine($"  Total simulated time   : {SimulatedTime}s");
        Console.WriteLine($"  Robot wait episodes    : {RobotWaitCount}");
        Console.WriteLine($"  Robot idle time        : {RobotIdleTime}s");
        Console.WriteLine("==============================================");
    }
}

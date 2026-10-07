namespace EFEMSimulator.Models;

public enum ChamberState
{
    Idle,
    Processing,
    DoneAwaitingPickup,
    WaferDamaged
}

public class Chamber
{
    public int Id { get; }
    public ChamberState State { get; private set; } = ChamberState.Idle;
    public Wafer? Wafer { get; private set; }
    public int ProcessTimer { get; private set; }
    public int PostProcessTimer { get; private set; }

    private const int ProcessDuration = 20;
    private const int PickupWindow = 6;

    public bool IsIdle => State == ChamberState.Idle;
    public bool NeedsPickup => State == ChamberState.DoneAwaitingPickup;

    public Chamber(int id)
    {
        Id = id;
    }

    public void PlaceWafer(Wafer wafer)
    {
        Wafer = wafer;
        Wafer.Status = WaferStatus.Processing;
        ProcessTimer = 0;
        PostProcessTimer = 0;
        State = ChamberState.Processing;
    }

    public Wafer PickupWafer()
    {
        var wafer = Wafer!;
        wafer.Status = WaferStatus.InTransit;
        Wafer = null;
        ProcessTimer = 0;
        PostProcessTimer = 0;
        State = ChamberState.Idle;
        return wafer;
    }

    public void Reset()
    {
        Wafer = null;
        ProcessTimer = 0;
        PostProcessTimer = 0;
        State = ChamberState.Idle;
    }

    public void Tick(int seconds, int simulatedTime)
    {
        switch (State)
        {
            case ChamberState.Processing:
                ProcessTimer += seconds;
                if (ProcessTimer >= ProcessDuration)
                {
                    State = ChamberState.DoneAwaitingPickup;
                    PostProcessTimer = 0;
                    Console.WriteLine($"[t={simulatedTime:D4}s] CH{Id,-4} Processing complete ({Wafer}) — pickup window: {PickupWindow}s");
                }
                break;

            case ChamberState.DoneAwaitingPickup:
                PostProcessTimer += seconds;
                if (PostProcessTimer > PickupWindow)
                {
                    State = ChamberState.WaferDamaged;
                    Wafer!.Status = WaferStatus.Damaged;
                    Console.WriteLine($"[t={simulatedTime:D4}s] CH{Id,-4} !!! WAFER DAMAGED ({Wafer}) — pickup window missed !!!");
                }
                break;
        }
    }

    public int ProcessingSecondsRemaining =>
        State == ChamberState.Processing ? ProcessDuration - ProcessTimer : int.MaxValue;

    public override string ToString() => $"CH{Id} [{State}]";
}

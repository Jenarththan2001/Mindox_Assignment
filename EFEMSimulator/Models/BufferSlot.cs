namespace EFEMSimulator.Models;

public enum BufferSlotState
{
    Empty,
    Heating,
    ReadyForPickup
}

public class BufferSlot
{
    public int SlotId { get; }
    public BufferSlotState State { get; private set; } = BufferSlotState.Empty;
    public Wafer? Wafer { get; private set; }
    public int HeatTimer { get; private set; }

    private const int HeatDuration = 10;

    public bool IsEmpty => State == BufferSlotState.Empty;
    public bool IsReady => State == BufferSlotState.ReadyForPickup;

    public BufferSlot(int slotId)
    {
        SlotId = slotId;
    }

    public void PlaceWafer(Wafer wafer)
    {
        Wafer = wafer;
        Wafer.Status = WaferStatus.Heating;
        HeatTimer = 0;
        State = BufferSlotState.Heating;
    }

    public Wafer PickupWafer()
    {
        var wafer = Wafer!;
        wafer.Status = WaferStatus.InTransit;
        Wafer = null;
        HeatTimer = 0;
        State = BufferSlotState.Empty;
        return wafer;
    }

    public void Tick(int seconds)
    {
        if (State != BufferSlotState.Heating) return;

        HeatTimer += seconds;
        if (HeatTimer >= HeatDuration)
            State = BufferSlotState.ReadyForPickup;
    }

    public override string ToString() => $"PHB-S{SlotId} [{State}]";
}

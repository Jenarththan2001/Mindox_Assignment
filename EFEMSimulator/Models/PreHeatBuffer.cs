namespace EFEMSimulator.Models;

public class PreHeatBuffer
{
    public BufferSlot[] Slots { get; }

    public PreHeatBuffer()
    {
        Slots = new BufferSlot[]
        {
            new BufferSlot(1),
            new BufferSlot(2),
            new BufferSlot(3)
        };
    }

    public BufferSlot? GetFreeSlot()
    {
        foreach (var slot in Slots)
            if (slot.IsEmpty) return slot;
        return null;
    }

    public BufferSlot? GetReadySlot()
    {
        return Slots
            .Where(s => s.IsReady)
            .OrderBy(s => s.Wafer!.Id)
            .FirstOrDefault();
    }

    public bool HasFreeSlot => GetFreeSlot() != null;
    public bool HasReadySlot => GetReadySlot() != null;

    public void Tick(int seconds)
    {
        foreach (var slot in Slots)
            slot.Tick(seconds);
    }

    public override string ToString()
    {
        return $"PHB [S1:{Slots[0].State} S2:{Slots[1].State} S3:{Slots[2].State}]";
    }
}

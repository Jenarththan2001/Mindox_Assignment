namespace EFEMSimulator.Models;

public class LoadPort
{
    public int Id { get; }
    private readonly Queue<Wafer> _wafers = new();

    public int WaferCount => _wafers.Count;
    public bool HasWafers => _wafers.Count > 0;

    public LoadPort(int id, int waferCount = 0)
    {
        Id = id;
        for (int i = 1; i <= waferCount; i++)
            _wafers.Enqueue(new Wafer(i));
    }

    public Wafer? TakeWafer()
    {
        if (!HasWafers) return null;
        var wafer = _wafers.Dequeue();
        wafer.Status = WaferStatus.InTransit;
        return wafer;
    }

    public void ReturnWafer(Wafer wafer)
    {
        wafer.Status = WaferStatus.Complete;
        _wafers.Enqueue(wafer);
    }

    public override string ToString() => $"LoadPort{Id} ({WaferCount} wafers)";
}

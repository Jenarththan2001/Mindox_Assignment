namespace EFEMSimulator.Models;

public enum WaferStatus
{
    InLoadPort1,
    InTransit,
    Heating,
    InChamber,
    Processing,
    Complete,
    Damaged
}

public class Wafer
{
    public int Id { get; }
    public WaferStatus Status { get; set; }

    public Wafer(int id)
    {
        Id = id;
        Status = WaferStatus.InLoadPort1;
    }

    public override string ToString() => $"W{Id:D2}";
}

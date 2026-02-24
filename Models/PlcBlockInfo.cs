namespace OpennessCopy.Models;

public enum PlcBlockKind
{
    Unknown,
    OB,
    FB,
    FC,
    GlobalDb,
    InstanceDb,
    ArrayDb
}

public class PlcBlockInfo
{
    public string Name { get; set; } = string.Empty;
    public int BlockNumber { get; set; }
    public PlcBlockKind Kind { get; set; }
    public bool IsInstanceDb { get; set; }
    public string InstanceOfName { get; set; } = string.Empty;
    public string GroupPath { get; set; } = string.Empty;
    public string BlockId { get; set; } = string.Empty;
}

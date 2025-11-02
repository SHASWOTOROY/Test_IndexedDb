namespace Testing_Indexed_db.Shared.Models;

public enum SyncActionType
{
    Create,
    Update,
    Delete
}

public class SyncAction
{
    public int Id { get; set; }
    public SyncActionType ActionType { get; set; }
    public Employee Employee { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsSynced { get; set; } = false;
}



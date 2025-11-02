namespace Testing_Indexed_db.Shared.Services;

public interface ISyncService
{
    Task<bool> SyncAsync();
    Task<List<string>> GetPendingSyncActionsAsync();
    bool IsSyncing { get; }
    event EventHandler<string>? SyncStatusChanged;
}



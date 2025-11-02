namespace Testing_Indexed_db.Shared.Services;

public interface IOfflineService
{
    Task<bool> IsOnlineAsync();
    Task InitializeOfflineDetectionAsync();
    event EventHandler<bool>? OnlineStatusChanged;
}



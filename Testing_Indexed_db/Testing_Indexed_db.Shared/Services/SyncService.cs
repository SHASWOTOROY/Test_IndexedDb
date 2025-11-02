using Testing_Indexed_db.Shared.Models;
using Microsoft.JSInterop;
using System.Text.Json;

namespace Testing_Indexed_db.Shared.Services;

public class SyncService : ISyncService
{
    private readonly IEmployeeService _localService;
    private readonly IEmployeeApiService _apiService;
    private readonly IOfflineService _offlineService;
    private readonly IJSRuntime _jsRuntime;
    private bool _isSyncing = false;

    public bool IsSyncing => _isSyncing;
    public event EventHandler<string>? SyncStatusChanged;

    public SyncService(
        IEmployeeService localService,
        IEmployeeApiService apiService,
        IOfflineService offlineService,
        IJSRuntime jsRuntime)
    {
        _localService = localService;
        _apiService = apiService;
        _offlineService = offlineService;
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> SyncAsync()
    {
        if (_isSyncing)
        {
            OnSyncStatusChanged("Sync already in progress...");
            return false;
        }

        var isOnline = await _offlineService.IsOnlineAsync();
        if (!isOnline)
        {
            OnSyncStatusChanged("Cannot sync: Device is offline");
            return false;
        }

        _isSyncing = true;
        OnSyncStatusChanged("Starting sync...");

        try
        {
            // 1. Get pending sync actions from IndexedDB
            var pendingActions = await GetPendingSyncActionsFromIndexedDBAsync();
            OnSyncStatusChanged($"Found {pendingActions.Count} pending changes to sync...");

            // 2. Upload pending changes to server
            var uploadedCount = 0;
            foreach (var action in pendingActions)
            {
                try
                {
                    switch (action.ActionType)
                    {
                        case SyncActionType.Create:
                            var created = await _apiService.CreateEmployeeAsync(action.Employee);
                            await MarkSyncActionAsSyncedAsync(action.Id);
                            uploadedCount++;
                            break;

                        case SyncActionType.Update:
                            await _apiService.UpdateEmployeeAsync(action.Employee);
                            await MarkSyncActionAsSyncedAsync(action.Id);
                            uploadedCount++;
                            break;

                        case SyncActionType.Delete:
                            await _apiService.DeleteEmployeeAsync(action.Employee.Id);
                            await MarkSyncActionAsSyncedAsync(action.Id);
                            uploadedCount++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error syncing action {action.Id}: {ex.Message}");
                }
            }

            OnSyncStatusChanged($"Uploaded {uploadedCount} changes...");

            // 3. Download latest data from server
            OnSyncStatusChanged("Downloading latest data from server...");
            var serverEmployees = await _apiService.GetAllEmployeesAsync();
            var localEmployees = await _localService.GetAllEmployeesAsync();

            // 4. Merge server data with local IndexedDB
            foreach (var serverEmp in serverEmployees)
            {
                try
                {
                    var localEmp = localEmployees.FirstOrDefault(e => e.Id == serverEmp.Id);
                    
                    if (localEmp == null)
                    {
                        // New employee from server - use updateEmployee which will add if doesn't exist
                        // This preserves the server's ID
                        await _localService.UpdateEmployeeAsync(serverEmp);
                    }
                    else
                    {
                        // Update local if server version is newer
                        var serverDate = serverEmp.HireDate;
                        var localDate = localEmp.HireDate;
                        
                        // Simple conflict resolution: use server version
                        if (serverDate >= localDate || 
                            (serverEmp.Name != localEmp.Name || serverEmp.Email != localEmp.Email))
                        {
                            await _localService.UpdateEmployeeAsync(serverEmp);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error syncing employee {serverEmp.Id} ({serverEmp.Name}): {ex.Message}");
                    // Continue with next employee even if one fails
                }
            }

            // 5. Remove sync actions that are synced
            await RemoveSyncedActionsAsync();

            OnSyncStatusChanged("Sync completed successfully!");
            return true;
        }
        catch (Exception ex)
        {
            OnSyncStatusChanged($"Sync failed: {ex.Message}");
            Console.WriteLine($"Sync error: {ex.Message}");
            return false;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    public async Task<List<string>> GetPendingSyncActionsAsync()
    {
        try
        {
            var actions = await GetAllPendingSyncActionsAsync();
            return actions.Select(a => $"{a.ActionType} - {a.Employee.Name} ({a.Timestamp:yyyy-MM-dd HH:mm})").ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private void OnSyncStatusChanged(string message)
    {
        SyncStatusChanged?.Invoke(this, message);
    }

    private async Task<List<SyncAction>> GetPendingSyncActionsFromIndexedDBAsync()
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<string>("indexedDBService.getAllPendingSyncActions");
            if (string.IsNullOrEmpty(result) || result == "[]")
                return new List<SyncAction>();

            var actions = JsonSerializer.Deserialize<List<SyncAction>>(result, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return actions?.Where(a => !a.IsSynced).ToList() ?? new List<SyncAction>();
        }
        catch
        {
            return new List<SyncAction>();
        }
    }

    private async Task<List<SyncAction>> GetAllPendingSyncActionsAsync()
    {
        return await GetPendingSyncActionsFromIndexedDBAsync();
    }

    private async Task MarkSyncActionAsSyncedAsync(int actionId)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("indexedDBService.markSyncActionAsSynced", actionId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error marking action as synced: {ex.Message}");
        }
    }

    private async Task RemoveSyncedActionsAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("indexedDBService.removeSyncedActions");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing synced actions: {ex.Message}");
        }
    }
}



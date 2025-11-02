using Microsoft.JSInterop;

namespace Testing_Indexed_db.Shared.Services;

public class OfflineService : IOfflineService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<OfflineService>? _dotNetRef;
    public event EventHandler<bool>? OnlineStatusChanged;

    public OfflineService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> IsOnlineAsync()
    {
        try
        {
            var result = await _jsRuntime.InvokeAsync<bool>("navigator.onLine");
            return result;
        }
        catch
        {
            // If JS interop fails, assume online
            return true;
        }
    }

    public async Task InitializeOfflineDetectionAsync()
    {
        try
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            
            // Initialize offline detection using the JavaScript service
            await _jsRuntime.InvokeVoidAsync("offlineDetection.init", _dotNetRef);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing offline detection: {ex.Message}");
            // Fallback: just check status periodically if events don't work
        }
    }
    
    public async Task<bool> CheckOnlineStatusAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("offlineDetection.isOnline");
        }
        catch
        {
            return true; // Assume online if check fails
        }
    }

    [JSInvokable]
    public void OnOnlineStatusChanged(bool isOnline)
    {
        OnlineStatusChanged?.Invoke(this, isOnline);
    }

    public async ValueTask DisposeAsync()
    {
        _dotNetRef?.Dispose();
        await Task.CompletedTask;
    }
}


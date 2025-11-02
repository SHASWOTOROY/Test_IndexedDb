using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Testing_Indexed_db.Services;
using Testing_Indexed_db.Shared.Services;

namespace Testing_Indexed_db
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            // Add device-specific services used by the Testing_Indexed_db.Shared project
            builder.Services.AddSingleton<IFormFactor, FormFactor>();

            // Add HttpClient for API calls (configure base URL for mobile)
            builder.Services.AddHttpClient<IEmployeeApiService, EmployeeApiService>(client =>
            {
                // Configure API base URL based on platform
                // IMPORTANT: Update the port (5134) if your web app runs on a different port
                // Check launchSettings.json in Testing_Indexed_db.Web project for the correct port
                
#if ANDROID
                // Android Emulator: Use 10.0.2.2 to access host machine's localhost
                // For Physical Android Device: Use your computer's IP address (e.g., "http://192.168.31.69:5134")
                // Find your IP: Run 'ipconfig' (Windows) or 'ifconfig' (Mac/Linux) and look for IPv4 Address
                client.BaseAddress = new Uri("http://10.0.2.2:5134");
#elif IOS
                // iOS Simulator: Use localhost (same as host machine)
                // For Physical iOS Device: Use your computer's IP address (e.g., "http://192.168.31.69:5134")
                // Find your IP: Run 'ipconfig' (Windows) or 'ifconfig' (Mac/Linux) and look for IPv4 Address
                client.BaseAddress = new Uri("http://localhost:5134");
#elif WINDOWS
                // Windows: Use localhost
                client.BaseAddress = new Uri("http://localhost:5134");
#else
                // Default fallback
                client.BaseAddress = new Uri("http://localhost:5134");
#endif
            });

            // Add Employee service for IndexedDB operations (WORKS OFFLINE)
            builder.Services.AddScoped<IEmployeeService>(sp =>
            {
                var jsRuntime = sp.GetRequiredService<IJSRuntime>();
                var offlineService = sp.GetRequiredService<IOfflineService>();
                var apiService = sp.GetService<IEmployeeApiService>();
                return new EmployeeService(jsRuntime, offlineService, apiService);
            });
            
            // Add Offline service for online/offline detection
            builder.Services.AddScoped<IOfflineService, OfflineService>();

            // Add Sync service
            builder.Services.AddScoped<ISyncService, SyncService>();

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

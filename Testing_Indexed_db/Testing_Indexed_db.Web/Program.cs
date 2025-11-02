using Microsoft.JSInterop;
using Testing_Indexed_db.Shared.Services;
using Testing_Indexed_db.Web.Components;
using Testing_Indexed_db.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add controllers for Web API
builder.Services.AddControllers();

// Add device-specific services used by the Testing_Indexed_db.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

// Enable CORS for mobile app (if needed)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add HttpContextAccessor for getting current request base URL
builder.Services.AddHttpContextAccessor();

// Add HttpClient for API calls (for Blazor Server, BaseAddress will be set dynamically)
builder.Services.AddHttpClient<IEmployeeApiService, EmployeeApiService>();

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseCors();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Testing_Indexed_db.Shared._Imports).Assembly);

// Map API controllers
app.MapControllers();

app.Run();

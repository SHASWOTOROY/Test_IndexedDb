using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Testing_Indexed_db.Shared.Models;

namespace Testing_Indexed_db.Shared.Services;

public class EmployeeApiService : IEmployeeApiService
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public EmployeeApiService(HttpClient httpClient, IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        // Set BaseAddress if not already set (for mobile apps)
        if (_httpClient.BaseAddress == null)
        {
            // For web apps, we'll construct absolute URIs using HttpContext
            // For mobile apps, BaseAddress should be set in DI configuration
        }
    }
    
    private string GetBaseUrl()
    {
        // For web app (Blazor Server), get base URL from HttpContext
        if (_httpContextAccessor?.HttpContext != null)
        {
            try
            {
                var request = _httpContextAccessor.HttpContext.Request;
                var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
                // Ensure it doesn't end with a slash (we'll add it in GetApiUri)
                return baseUrl.TrimEnd('/');
            }
            catch
            {
                // If HttpContext access fails, try BaseAddress
            }
        }
        
        // For mobile app, use BaseAddress
        var baseAddress = _httpClient.BaseAddress?.ToString();
        if (!string.IsNullOrEmpty(baseAddress))
        {
            return baseAddress.TrimEnd('/');
        }
        
        return "";
    }

    public async Task<bool> IsApiAvailableAsync()
    {
        try
        {
            var uri = GetApiUri("api/employees");
            var response = await _httpClient.GetAsync(uri);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<Employee>> GetAllEmployeesAsync()
    {
        try
        {
            var uri = GetApiUri("api/employees");
            var response = await _httpClient.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            var employees = await response.Content.ReadFromJsonAsync<List<Employee>>();
            return employees ?? new List<Employee>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"API Error getting employees: {ex.Message}");
            throw;
        }
    }

    public async Task<Employee?> GetEmployeeByIdAsync(int id)
    {
        try
        {
            var uri = GetApiUri($"api/employees/{id}");
            var response = await _httpClient.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Employee>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"API Error getting employee: {ex.Message}");
            return null;
        }
    }

    public async Task<Employee> CreateEmployeeAsync(Employee employee)
    {
        try
        {
            var uri = GetApiUri("api/employees");
            var response = await _httpClient.PostAsJsonAsync(uri, employee);
            response.EnsureSuccessStatusCode();
            var createdEmployee = await response.Content.ReadFromJsonAsync<Employee>();
            return createdEmployee ?? employee;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"API Error creating employee: {ex.Message}");
            throw;
        }
    }

    public async Task<Employee> UpdateEmployeeAsync(Employee employee)
    {
        try
        {
            var uri = GetApiUri($"api/employees/{employee.Id}");
            var response = await _httpClient.PutAsJsonAsync(uri, employee);
            response.EnsureSuccessStatusCode();
            var updatedEmployee = await response.Content.ReadFromJsonAsync<Employee>();
            return updatedEmployee ?? employee;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"API Error updating employee: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DeleteEmployeeAsync(int id)
    {
        try
        {
            var uri = GetApiUri($"api/employees/{id}");
            var response = await _httpClient.DeleteAsync(uri);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"API Error deleting employee: {ex.Message}");
            return false;
        }
    }
    
    private Uri GetApiUri(string relativePath)
    {
        var baseUrl = GetBaseUrl();
        if (string.IsNullOrEmpty(baseUrl))
        {
            // For Blazor Server, if we can't get base URL, throw a clear error
            throw new InvalidOperationException(
                "Cannot determine base URL for API calls. " +
                "Make sure HttpContextAccessor is properly registered and available.");
        }
        
        // Ensure relativePath starts with /
        if (!relativePath.StartsWith("/"))
        {
            relativePath = "/" + relativePath;
        }
        
        // Construct absolute URI
        var baseUri = new Uri(baseUrl);
        return new Uri(baseUri, relativePath);
    }
}


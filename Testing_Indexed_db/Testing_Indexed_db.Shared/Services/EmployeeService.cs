using Microsoft.JSInterop;
using Testing_Indexed_db.Shared.Models;
using System.Text.Json;

namespace Testing_Indexed_db.Shared.Services;

public class EmployeeService : IEmployeeService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IOfflineService? _offlineService;
    private readonly IEmployeeApiService? _apiService;
    private bool _initialized = false;

    public EmployeeService(
        IJSRuntime jsRuntime,
        IOfflineService? offlineService = null,
        IEmployeeApiService? apiService = null)
    {
        _jsRuntime = jsRuntime;
        _offlineService = offlineService;
        _apiService = apiService;
    }

    public async Task InitializeAsync()
    {
        if (!_initialized)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("indexedDBService.init");
                _initialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing IndexedDB: {ex.Message}");
                throw;
            }
        }
    }

    public async Task<List<Employee>> GetAllEmployeesAsync()
    {
        await EnsureInitialized();
        
        try
        {
            var result = await _jsRuntime.InvokeAsync<string>("indexedDBService.getAllEmployees");
            var employees = JsonSerializer.Deserialize<List<Employee>>(result, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }) ?? new List<Employee>();
            
            // Ensure dates are parsed correctly
            foreach (var emp in employees)
            {
                if (emp.HireDate == default || emp.HireDate.Year == 1)
                {
                    emp.HireDate = DateTime.Now;
                }
            }
            
            return employees;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting employees: {ex.Message}");
            return new List<Employee>();
        }
    }

    public async Task<Employee?> GetEmployeeByIdAsync(int id)
    {
        await EnsureInitialized();
        
        try
        {
            var result = await _jsRuntime.InvokeAsync<string>("indexedDBService.getEmployeeById", id);
            if (string.IsNullOrEmpty(result) || result == "null")
            {
                return null;
            }
            
            var employee = JsonSerializer.Deserialize<Employee>(result, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            if (employee != null && (employee.HireDate == default || employee.HireDate.Year == 1))
            {
                employee.HireDate = DateTime.Now;
            }
            
            return employee;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting employee: {ex.Message}");
            return null;
        }
    }

    public async Task<Employee> AddEmployeeAsync(Employee employee)
    {
        await EnsureInitialized();
        
        try
        {
            // Create a clean employee object without id for new employees
            var employeeToAdd = new
            {
                name = employee.Name,
                email = employee.Email,
                department = employee.Department,
                position = employee.Position,
                hireDate = employee.HireDate
            };
            
            // Always save to local IndexedDB first (don't send id for new employees)
            var employeeJson = JsonSerializer.Serialize(employeeToAdd, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            var result = await _jsRuntime.InvokeAsync<string>("indexedDBService.addEmployee", employeeJson);
            var addedEmployee = JsonSerializer.Deserialize<Employee>(result, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }) ?? employee;

            if (addedEmployee.HireDate == default || addedEmployee.HireDate.Year == 1)
            {
                addedEmployee.HireDate = DateTime.Now;
            }

            // Try to sync with server if online (non-blocking - don't fail if sync fails)
            if (_offlineService != null && _apiService != null)
            {
                try
                {
                    var isOnline = await _offlineService.IsOnlineAsync();
                    if (isOnline)
                    {
                        try
                        {
                            await _apiService.CreateEmployeeAsync(addedEmployee);
                        }
                        catch (Exception syncEx)
                        {
                            // If sync fails, queue for later - don't fail the whole operation
                            Console.WriteLine($"API sync failed, queuing for later: {syncEx.Message}");
                            await QueueSyncActionAsync(SyncActionType.Create, addedEmployee);
                        }
                    }
                    else
                    {
                        // Queue for sync when online
                        await QueueSyncActionAsync(SyncActionType.Create, addedEmployee);
                    }
                }
                catch (Exception ex)
                {
                    // Even if offline detection or queueing fails, still return the employee
                    Console.WriteLine($"Warning: Could not queue sync action: {ex.Message}");
                }
            }

            return addedEmployee;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding employee to IndexedDB: {ex.Message}");
            throw;
        }
    }

    public async Task<Employee> UpdateEmployeeAsync(Employee employee)
    {
        await EnsureInitialized();
        
        try
        {
            // Always update local IndexedDB first
            var employeeJson = JsonSerializer.Serialize(employee, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            var result = await _jsRuntime.InvokeAsync<string>("indexedDBService.updateEmployee", employeeJson);
            var updatedEmployee = JsonSerializer.Deserialize<Employee>(result, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }) ?? employee;

            if (updatedEmployee.HireDate == default || updatedEmployee.HireDate.Year == 1)
            {
                updatedEmployee.HireDate = DateTime.Now;
            }

            // Try to sync with server if online
            if (_offlineService != null && _apiService != null)
            {
                var isOnline = await _offlineService.IsOnlineAsync();
                if (isOnline)
                {
                    try
                    {
                        await _apiService.UpdateEmployeeAsync(updatedEmployee);
                    }
                    catch
                    {
                        // If sync fails, queue for later
                        await QueueSyncActionAsync(SyncActionType.Update, updatedEmployee);
                    }
                }
                else
                {
                    // Queue for sync when online
                    await QueueSyncActionAsync(SyncActionType.Update, updatedEmployee);
                }
            }

            return updatedEmployee;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating employee: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DeleteEmployeeAsync(int id)
    {
        await EnsureInitialized();
        
        try
        {
            // Get employee before deleting
            var employee = await GetEmployeeByIdAsync(id);
            
            // Delete from local IndexedDB
            await _jsRuntime.InvokeVoidAsync("indexedDBService.deleteEmployee", id);

            // Try to sync with server if online
            if (_offlineService != null && _apiService != null && employee != null)
            {
                var isOnline = await _offlineService.IsOnlineAsync();
                if (isOnline)
                {
                    try
                    {
                        await _apiService.DeleteEmployeeAsync(id);
                    }
                    catch
                    {
                        // If sync fails, queue for later
                        await QueueSyncActionAsync(SyncActionType.Delete, employee);
                    }
                }
                else
                {
                    // Queue for sync when online
                    await QueueSyncActionAsync(SyncActionType.Delete, employee);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting employee: {ex.Message}");
            return false;
        }
    }

    private async Task QueueSyncActionAsync(SyncActionType actionType, Employee employee)
    {
        try
        {
            var syncAction = new SyncAction
            {
                ActionType = actionType,
                Employee = employee,
                Timestamp = DateTime.Now,
                IsSynced = false
            };

            var actionJson = JsonSerializer.Serialize(syncAction, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await _jsRuntime.InvokeVoidAsync("indexedDBService.addSyncAction", actionJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error queuing sync action: {ex.Message}");
        }
    }

    private async Task EnsureInitialized()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
    }
}


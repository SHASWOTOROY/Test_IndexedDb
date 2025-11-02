using Testing_Indexed_db.Shared.Models;

namespace Testing_Indexed_db.Shared.Services;

public interface IEmployeeApiService
{
    Task<List<Employee>> GetAllEmployeesAsync();
    Task<Employee?> GetEmployeeByIdAsync(int id);
    Task<Employee> CreateEmployeeAsync(Employee employee);
    Task<Employee> UpdateEmployeeAsync(Employee employee);
    Task<bool> DeleteEmployeeAsync(int id);
    Task<bool> IsApiAvailableAsync();
}



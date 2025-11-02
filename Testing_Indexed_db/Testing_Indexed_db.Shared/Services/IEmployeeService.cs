using Testing_Indexed_db.Shared.Models;

namespace Testing_Indexed_db.Shared.Services;

public interface IEmployeeService
{
    Task InitializeAsync();
    Task<List<Employee>> GetAllEmployeesAsync();
    Task<Employee?> GetEmployeeByIdAsync(int id);
    Task<Employee> AddEmployeeAsync(Employee employee);
    Task<Employee> UpdateEmployeeAsync(Employee employee);
    Task<bool> DeleteEmployeeAsync(int id);
}



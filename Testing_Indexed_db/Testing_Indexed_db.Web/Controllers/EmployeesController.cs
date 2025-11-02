using Microsoft.AspNetCore.Mvc;
using Testing_Indexed_db.Shared.Models;

namespace Testing_Indexed_db.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeesController : ControllerBase
{
    private static List<Employee> _employees = new();
    private static int _nextId = 1;

    static EmployeesController()
    {
        // Initialize with some sample data
        _employees.Add(new Employee
        {
            Id = _nextId++,
            Name = "John Doe",
            Email = "john.doe@example.com",
            Department = "Engineering",
            Position = "Software Engineer",
            HireDate = DateTime.Now.AddYears(-2)
        });
    }

    [HttpGet]
    public ActionResult<List<Employee>> GetAll()
    {
        return Ok(_employees);
    }

    [HttpGet("{id}")]
    public ActionResult<Employee> GetById(int id)
    {
        var employee = _employees.FirstOrDefault(e => e.Id == id);
        if (employee == null)
            return NotFound();

        return Ok(employee);
    }

    [HttpPost]
    public ActionResult<Employee> Create([FromBody] Employee employee)
    {
        employee.Id = _nextId++;
        employee.HireDate = employee.HireDate == default ? DateTime.Now : employee.HireDate;
        _employees.Add(employee);
        return CreatedAtAction(nameof(GetById), new { id = employee.Id }, employee);
    }

    [HttpPut("{id}")]
    public ActionResult<Employee> Update(int id, [FromBody] Employee employee)
    {
        var existingEmployee = _employees.FirstOrDefault(e => e.Id == id);
        if (existingEmployee == null)
            return NotFound();

        existingEmployee.Name = employee.Name;
        existingEmployee.Email = employee.Email;
        existingEmployee.Department = employee.Department;
        existingEmployee.Position = employee.Position;
        existingEmployee.HireDate = employee.HireDate;

        return Ok(existingEmployee);
    }

    [HttpDelete("{id}")]
    public ActionResult Delete(int id)
    {
        var employee = _employees.FirstOrDefault(e => e.Id == id);
        if (employee == null)
            return NotFound();

        _employees.Remove(employee);
        return NoContent();
    }
}



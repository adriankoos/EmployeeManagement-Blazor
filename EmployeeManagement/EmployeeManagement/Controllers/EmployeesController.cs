using EmployeeManagement.Client.Models;
using EmployeeManagement.Data;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace EmployeeManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeesController : ControllerBase
    {
        private readonly AppDbContext _db;

        private readonly IpCountryService _ipService;

        public EmployeesController(AppDbContext db, IpCountryService ipService)
        {
            _db = db;
            _ipService = ipService;
        }

        // Zobrazenie všetkých zamestnancov
        [HttpGet]
        public async Task<ActionResult<List<Employee>>> GetAll()
        {
            return await _db.Employees
                .Include(e => e.Position)
                .OrderBy(e => e.Surname).ThenBy(e => e.Name)
                .ToListAsync();
        }

        // Zobrazenie jedného zamestnanca
        [HttpGet("{id}")]
        public async Task<ActionResult<Employee>> GetById(int id)
        {
            var employee = await _db.Employees
                .Include(e => e.Position)
                .FirstOrDefaultAsync(e => e.EmployeeID == id);

            if (employee == null) return NotFound();
            return employee;
        }

        // Pridanie
        [HttpPost]
        public async Task<ActionResult<Employee>> Create(Employee employee)
        {
            bool exists = await _db.Employees.AnyAsync(e =>
                e.Name == employee.Name &&
                e.Surname == employee.Surname &&
                e.BirthDate == employee.BirthDate);

            if (exists)
                return Conflict(new { message = "Zamestnanec s týmto menom, priezviskom a dátumom narodenia už existuje." });
            // Krajinu zistíme z IP adresy cez externé API
            employee.IPCountryCode = await _ipService.GetCountryCodeAsync(employee.IPaddress);

            employee.Position = null; // pozícia sa viaže cez PositionID
            _db.Employees.Add(employee);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = employee.EmployeeID }, employee);
        }

        // Úprava
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, Employee employee)
        {
            if (id != employee.EmployeeID) return BadRequest();

            var existing = await _db.Employees.FindAsync(id);
            if (existing == null) return NotFound();

            bool duplicate = await _db.Employees.AnyAsync(e =>
                e.EmployeeID != id &&
                e.Name == employee.Name &&
                e.Surname == employee.Surname &&
                e.BirthDate == employee.BirthDate);

            if (duplicate)
                return Conflict(new { message = "Zamestnanec s týmto menom, priezviskom a dátumom narodenia už existuje." });

            existing.Name = employee.Name;
            existing.Surname = employee.Surname;
            existing.BirthDate = employee.BirthDate;
            existing.PositionID = employee.PositionID;
            existing.IPaddress = employee.IPaddress;
            existing.IPCountryCode = employee.IPCountryCode;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Zmazanie
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var employee = await _db.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            _db.Employees.Remove(employee);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
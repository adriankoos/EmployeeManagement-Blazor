using EmployeeManagement.Client.Models;
using EmployeeManagement.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PositionsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public PositionsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<List<Position>>> GetAll()
        {
            return await _db.Positions.OrderBy(p => p.PositionName).ToListAsync();
        }
    }
}
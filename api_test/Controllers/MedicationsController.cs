using api_test.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api_test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MedicationsController : ControllerBase
    {
       private readonly AppDbContext _context;
        public MedicationsController(AppDbContext context)
        {
            _context = context;
        }
        
       
        [Authorize(Roles = "Admin,Patient")]
        [HttpGet("All Meds")]
        public async Task<ActionResult> GetMedications()
        {
            var medications = await _context.Medications.ToListAsync();
            return Ok(medications);
        }
    }
}

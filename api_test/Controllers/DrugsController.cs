using api_test.Data;
using api_test.Entities;
using api_test.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api_test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DrugsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DrugsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("mydrugs")]
        public async Task<ActionResult<IEnumerable<DrugDto>>> GetMyDrugs()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Unauthorized("Please log in first.");

            var drugs = await _context.Drugs
                .Where(d => d.UserId == userId)
                .Select(d => new DrugDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description,
                    Type = d.Type,
                    ExpirationDate = d.ExpirationDate,
                    ProductDate = d.ProductDate
                })
                .ToListAsync();

            return Ok(drugs);
        }

        [HttpPost]
        public async Task<ActionResult> AddDrug(CreateDrugDto dto)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Unauthorized("Please log in first.");

            var drug = new Drug
            {
                Name = dto.Name,
                Description = dto.Description,
                Type = dto.Type,
                ExpirationDate = dto.ExpirationDate,
                ProductDate = dto.ProductDate,
                UserId = userId.Value
            };

            _context.Drugs.Add(drug);
            await _context.SaveChangesAsync();

            return Ok("Drug added successfully");
        }
    }
}

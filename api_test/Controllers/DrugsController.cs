using api_test.Data;
using api_test.Entities;
using api_test.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace api_test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DrugsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DrugsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult> AddDrug(CreateDrugDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var drug = new Drug
            {
                Name = dto.Name,
                Description = dto.Description,
                Type = dto.Type,
                ExpirationDate = dto.ExpirationDate,
                ProductDate = dto.ProductDate,
                UserId = userId
            };

            _context.Drugs.Add(drug);
            await _context.SaveChangesAsync();

            return Ok("Drug added successfully");
        }

        [HttpGet("mydrugs")]
        public async Task<ActionResult> GetMyDrugs()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

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
    }
}

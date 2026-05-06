using api_test.Data;
using api_test.Entities;
using api_test.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api_test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MedIngredientsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MedIngredientsController(AppDbContext context)
        {
            _context = context;
        }


        [AllowAnonymous]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllIngredients()
        {
            var ingredients = await _context.Ingredients
                .Select(i => new IngredientDto
                {
                    Id = i.Id,
                    IngredientName = i.IngredientName ?? "Unknown" 
                })
                .ToListAsync();

            if (!ingredients.Any())
                return NotFound(new { Message = "No ingredients found." });

            return Ok(ingredients);
        }

        [Authorize(Roles = "Admin,Patient")]
        [HttpGet("by-med/{medName}")]
        public async Task<IActionResult> GetIngredientsByMedication(string medName)
        {
            var medication = await _context.Medications
                .FirstOrDefaultAsync(m => m.Trade_name == medName);

            if (medication == null)
                return NotFound(new { Message = "Medication not found." });

            var ingredients = await _context.Med_Ingredients_Link
                .Where(m => m.Med_id == medication.ID)
                .Select(m => new
                {
                    m.Ingredient_id,
                    m.Ingredient.IngredientName,
                    m.Strength_value,
                    m.Strength_unit
                })
                .ToListAsync();

            return Ok(ingredients);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("add")]
        public async Task<IActionResult> AddIngredientToMedication(AddMedIngredientDto dto)
        {
            var medication = await _context.Medications
                .FirstOrDefaultAsync(m => m.Trade_name == dto.MedicationName);
            if (medication == null)
                return NotFound(new { Message = $"Medication '{dto.MedicationName}' not found." });

            var addedIngredients = new List<object>();

            foreach (var item in dto.Ingredients)
            {
                var ingredient = await _context.Ingredients
                    .FirstOrDefaultAsync(i => i.IngredientName == item.IngredientName);
                if (ingredient == null)
                    return NotFound(new { Message = $"Ingredient '{item.IngredientName}' not found." });

                var alreadyExists = await _context.Med_Ingredients_Link
                    .AnyAsync(m => m.Med_id == medication.ID && m.Ingredient_id == ingredient.Id);
                if (alreadyExists)
                    return BadRequest(new { Message = $"'{item.IngredientName}' already linked to this medication." });

                _context.Med_Ingredients_Link.Add(new MedIngredientLink
                {
                    Med_id = medication.ID,
                    Ingredient_id = ingredient.Id,
                    Strength_value = item.StrengthValue,
                    Strength_unit = item.StrengthUnit
                });

                addedIngredients.Add(new
                {
                    Ingredient = ingredient.IngredientName,
                    item.StrengthValue,
                    item.StrengthUnit
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "Ingredients added to medication successfully.",
                Medication = medication.Trade_name,
                Added = addedIngredients
            });
        }
    }
}

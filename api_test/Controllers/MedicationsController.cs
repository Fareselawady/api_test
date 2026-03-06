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
            var medications = await _context.Medications
     .Select(m => new
     {
         m.ID,
         m.Trade_name,
         m.Description,
         m.Dosage_Form,
         m.image_url,

         Ingredients = m.Ingredients!
             .Select(i => new {
                 i.Ingredient.IngredientName,
                 i.Strength_value,
                 i.Strength_unit
             })
     })
     .ToListAsync();

            return Ok(medications);
        }
        [Authorize]
        [HttpGet("check-interaction")]
        public async Task<IActionResult> CheckDrugInteraction(string med1Name, string med2Name)
        {
            // جيب الدوائين بالاسم
            var med1 = await _context.Medications
                .FirstOrDefaultAsync(m => m.Trade_name == med1Name);

            var med2 = await _context.Medications
                .FirstOrDefaultAsync(m => m.Trade_name == med2Name);

            if (med1 == null)
                return NotFound(new { Message = $"Medication '{med1Name}' not found." });

            if (med2 == null)
                return NotFound(new { Message = $"Medication '{med2Name}' not found." });

            // جيب المواد الفعالة بالـ ID اللي جبناه
            var med1Ingredients = await _context.Med_Ingredients_Link
                .Where(m => m.Med_id == med1.ID)
                .Select(m => m.Ingredient_id)
                .ToListAsync();

            var med2Ingredients = await _context.Med_Ingredients_Link
                .Where(m => m.Med_id == med2.ID)
                .Select(m => m.Ingredient_id)
                .ToListAsync();

            var interactions = await _context.Drug_Interactions
                .Where(di =>
                    (med1Ingredients.Contains(di.Ingredient_1_id!.Value) && med2Ingredients.Contains(di.Ingredient_2_id!.Value)) ||
                    (med1Ingredients.Contains(di.Ingredient_2_id!.Value) && med2Ingredients.Contains(di.Ingredient_1_id!.Value))
                )
                .ToListAsync();

            if (!interactions.Any())
                return Ok(new { Message = $"No interaction between '{med1Name}' and '{med2Name}'." });

            var interactionDetails = interactions.Select(i => new
            {
                i.Interaction_type,
                Med1 = med1.Trade_name,
                Med2 = med2.Trade_name
            }).ToList();

            return Ok(new
            {
                Message = "Interaction exists between these medications.",
                Interactions = interactionDetails
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("add")]
        public async Task<IActionResult> AddMedication(CreateMedicationDto dto)
        {
            var exists = await _context.Medications
                .AnyAsync(m => m.Trade_name == dto.TradeName);

            if (exists)
                return BadRequest(new { Message = "Medication with this name already exists." });

            var medication = new Medication
            {
                Trade_name = dto.TradeName,
                Description = dto.Description,
                Dosage_Form = dto.DosageForm,
                image_url = dto.ImageUrl
            };

            _context.Medications.Add(medication);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "Medication added successfully.",
                MedicationId = medication.ID,
                MedicationName = medication.Trade_name
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteMedication(int id)
        {
            var medication = await _context.Medications
                .FirstOrDefaultAsync(m => m.ID == id);

            if (medication == null)
                return NotFound(new { Message = "Medication not found." });

            _context.Medications.Remove(medication);
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"Medication '{medication.Trade_name}' deleted successfully." });
        }
    }
}

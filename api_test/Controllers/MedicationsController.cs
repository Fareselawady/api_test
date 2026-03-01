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
        public async Task<IActionResult> CheckDrugInteraction(int med1Id, int med2Id)
        {
            var med1Ingredients = await _context.Med_Ingredients_Link
                .Where(m => m.Med_id == med1Id)
                .Select(m => m.Ingredient_id)
                .ToListAsync();

            var med2Ingredients = await _context.Med_Ingredients_Link
                .Where(m => m.Med_id == med2Id)
                .Select(m => m.Ingredient_id)
                .ToListAsync();

            var interactions = await _context.Drug_Interactions
                .Where(di =>
                    (med1Ingredients.Contains(di.Ingredient_1_id!.Value) && med2Ingredients.Contains(di.Ingredient_2_id!.Value)) ||
                    (med1Ingredients.Contains(di.Ingredient_2_id!.Value) && med2Ingredients.Contains(di.Ingredient_1_id!.Value))
                )
                .ToListAsync();

            if (!interactions.Any())
                return Ok(new { Message = "No interaction between these two medications." });

            var med1 = await _context.Medications.FindAsync(med1Id);
            var med2 = await _context.Medications.FindAsync(med2Id);

            var interactionDetails = interactions.Select(i => new
            {
                i.Interaction_type,
                Med1 = med1?.Trade_name,
                Med2 = med2?.Trade_name
            }).ToList();

            return Ok(new
            {
                Message = "Interaction exists between these medications.",
                Interactions = interactionDetails
            });
        }
    }
}

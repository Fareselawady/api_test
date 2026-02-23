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
    }
}

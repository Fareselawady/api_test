using api_test.Data;
using api_test.Entities;
using api_test.Models;
using api_test.Services;
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
        private readonly ITranslationService _translationService;

        public MedicationsController(AppDbContext context, ITranslationService translationService)
        {
            _context = context;
            _translationService = translationService;
        }


        [AllowAnonymous]
        [HttpGet("all")]
        public async Task<ActionResult> GetMedications([FromQuery] string lang = "en")
        {
            var medications = await _context.Medications
                .AsNoTracking()
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

            var result = medications.Select(m =>
            {
                var translatedName = _translationService.GetMedName(m.ID, lang);
                var translatedDescription = _translationService.GetMedDescription(m.ID, lang);
                var translatedDosageForm = _translationService.GetDosageForm(m.Dosage_Form ?? string.Empty, lang);

                return new
                {
                    m.ID,
                    Trade_name = string.IsNullOrWhiteSpace(translatedName)
                        ? m.Trade_name
                        : translatedName,
                    Description = string.IsNullOrWhiteSpace(translatedDescription)
                        ? m.Description
                        : translatedDescription,
                    Dosage_Form = string.IsNullOrWhiteSpace(translatedDosageForm)
                        ? m.Dosage_Form
                        : translatedDosageForm,
                    m.image_url,
                    m.Ingredients
                };
            });

            return Ok(result);
        }

        [Authorize]
        [HttpGet("check-interaction")]
        public async Task<IActionResult> CheckDrugInteraction([FromQuery] List<string> medNames)
        {
            if (medNames == null || medNames.Count < 2 || medNames.Count > 10)
                return BadRequest(new { Message = "Please provide between 2 and 10 medications." });

            // جيب كل الأدوية
            var medications = await _context.Medications
    .Where(m => m.Trade_name != null && medNames.Contains(m.Trade_name))
    .ToListAsync();
            // تحقق إن كل الأدوية موجودة
            var notFound = medNames.Except(medications.Select(m => m.Trade_name)).ToList();
            if (notFound.Any())
                return NotFound(new { Message = $"Not found: {string.Join(", ", notFound)}" });

            // جيب المواد الفعالة لكل دواء
            var medIds = medications.Select(m => m.ID).ToList();
            var allIngredients = await _context.Med_Ingredients_Link
                .Where(m => medIds.Contains(m.Med_id))
                .ToListAsync();

            // اعمل dictionary: MedId → List of IngredientIds
            var medIngredientMap = allIngredients
                .GroupBy(m => m.Med_id)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Ingredient_id).ToList());

            // قارن كل دواء مع الباقيين
            var results = new List<object>();

            for (int i = 0; i < medications.Count; i++)
            {
                for (int j = i + 1; j < medications.Count; j++)
                {
                    var med1 = medications[i];
                    var med2 = medications[j];

                    var ing1 = medIngredientMap.GetValueOrDefault(med1.ID, new List<int>());
                    var ing2 = medIngredientMap.GetValueOrDefault(med2.ID, new List<int>());

                    var interactions = await _context.Drug_Interactions
                        .Where(di =>
                            (ing1.Contains(di.Ingredient_1_id!.Value) && ing2.Contains(di.Ingredient_2_id!.Value)) ||
                            (ing1.Contains(di.Ingredient_2_id!.Value) && ing2.Contains(di.Ingredient_1_id!.Value))
                        )
                        .ToListAsync();

                    if (interactions.Any())
                    {
                        results.Add(new
                        {
                            Med1 = med1.Trade_name,
                            Med2 = med2.Trade_name,
                            Interactions = interactions.Select(i => new
                            {
                                i.Interaction_type
                            })
                        });
                    }
                }
            }

            if (!results.Any())
                return Ok(new { Message = "No interactions found between the provided medications." });

            return Ok(new
            {
                Message = "Interactions found!",
                Results = results
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

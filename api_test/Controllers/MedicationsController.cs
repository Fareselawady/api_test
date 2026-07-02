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
                    QuantityUnit = MedicationQuantityHelper.GetSuggestedUnit(m.Dosage_Form),
                    m.DefaultAfterOpeningValue,
                    m.DefaultAfterOpeningUnit,
                    m.RequiresOpeningTracking,
                    m.AfterOpeningNote,
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
                    CanonicalName = m.Trade_name,
                    DisplayName = string.IsNullOrWhiteSpace(translatedName)
                        ? m.Trade_name
                        : translatedName,
                    Trade_name = string.IsNullOrWhiteSpace(translatedName)
                        ? m.Trade_name
                        : translatedName,
                    Description = string.IsNullOrWhiteSpace(translatedDescription)
                        ? m.Description
                        : translatedDescription,
                    Dosage_Form = string.IsNullOrWhiteSpace(translatedDosageForm)
                        ? m.Dosage_Form
                        : translatedDosageForm,
                    m.QuantityUnit,
                    m.DefaultAfterOpeningValue,
                    DefaultAfterOpeningUnit = MedicationExpiryHelper.NormalizeDefaultUnit(m.DefaultAfterOpeningUnit),
                    RequiresOpeningTracking = m.RequiresOpeningTracking
                        || m.DefaultAfterOpeningValue.HasValue
                        || MedicationExpiryHelper.HasDosageFormDefault(m.Dosage_Form),
                    m.AfterOpeningNote,
                    m.image_url,
                    m.Ingredients
                };
            });

            return Ok(result);
        }

        [Authorize]
        [HttpGet("check-interaction")]
        public async Task<IActionResult> CheckDrugInteraction(
            [FromQuery(Name = "medIds")] List<int>? medicationIds,
            [FromQuery] List<string>? medNames,
            [FromQuery] string lang = "en")
        {
            medicationIds ??= new List<int>();
            medNames ??= new List<string>();

            var requestedCount = medicationIds.Count > 0
                ? medicationIds.Count
                : medNames.Count;
            if (requestedCount < 2 || requestedCount > 10)
                return BadRequest(new { Message = "Please provide between 2 and 10 medications." });

            List<Medication> medications;
            var unresolved = new List<string>();

            if (medicationIds.Count > 0)
            {
                var distinctIds = medicationIds.Distinct().ToList();
                medications = await _context.Medications
                    .Where(m => distinctIds.Contains(m.ID))
                    .ToListAsync();

                unresolved.AddRange(distinctIds
                    .Except(medications.Select(m => m.ID))
                    .Select(id => id.ToString()));
            }
            else
            {
                medications = new List<Medication>();
                foreach (var requestedName in medNames)
                {
                    var normalizedName = requestedName?.Trim();
                    if (string.IsNullOrWhiteSpace(normalizedName))
                    {
                        unresolved.Add(requestedName ?? string.Empty);
                        continue;
                    }

                    var translatedId = _translationService.FindMedIdByName(normalizedName);
                    var medication = translatedId.HasValue
                        ? await _context.Medications.FirstOrDefaultAsync(m => m.ID == translatedId.Value)
                        : await _context.Medications.FirstOrDefaultAsync(m =>
                            m.Trade_name != null &&
                            m.Trade_name.Trim().ToLower() == normalizedName.ToLower());

                    if (medication == null)
                        unresolved.Add(normalizedName);
                    else if (medications.All(m => m.ID != medication.ID))
                        medications.Add(medication);
                }
            }

            if (unresolved.Count > 0)
            {
                return NotFound(new
                {
                    Message = $"Could not resolve: {string.Join(", ", unresolved)}",
                    UnresolvedMedications = unresolved
                });
            }

            if (medications.Count < 2)
                return BadRequest(new { Message = "Please provide at least two different medications." });

            // جيب المواد الفعالة لكل دواء
            var medIds = medications.Select(m => m.ID).ToList();
            var allIngredients = await _context.Med_Ingredients_Link
                .Where(m => medIds.Contains(m.Med_id))
                .ToListAsync();

            // اعمل dictionary: medicationId -> List of IngredientIds
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
                            di.Ingredient_1_id.HasValue &&
                            di.Ingredient_2_id.HasValue &&
                            ((ing1.Contains(di.Ingredient_1_id.Value) && ing2.Contains(di.Ingredient_2_id.Value)) ||
                            (ing1.Contains(di.Ingredient_2_id.Value) && ing2.Contains(di.Ingredient_1_id.Value))
                            )
                        )
                        .ToListAsync();

                    if (interactions.Any())
                    {
                        results.Add(new
                        {
                            Med1Id = med1.ID,
                            Med2Id = med2.ID,
                            Med1 = LocalizedMedicationName(med1, lang),
                            Med2 = LocalizedMedicationName(med2, lang),
                            Interactions = interactions.Select(i => new
                            {
                                Interaction_type = _translationService.GetInteractionReason(
                                    i.Interaction_type ?? string.Empty,
                                    lang)
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

        private string LocalizedMedicationName(Medication medication, string lang)
        {
            var translated = _translationService.GetMedName(medication.ID, lang);
            return string.IsNullOrWhiteSpace(translated)
                ? medication.Trade_name ?? string.Empty
                : translated;
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("add")]
        public async Task<IActionResult> AddMedication(CreateMedicationDto dto)
        {
            var defaultError = MedicationExpiryHelper.ValidateMedicationDefault(
                dto.DefaultAfterOpeningValue,
                dto.DefaultAfterOpeningUnit);
            if (defaultError != null)
                return BadRequest(new { Message = defaultError });

            var exists = await _context.Medications
                .AnyAsync(m => m.Trade_name == dto.TradeName);

            if (exists)
                return BadRequest(new { Message = "Medication with this name already exists." });

            var medication = new Medication
            {
                Trade_name = dto.TradeName,
                Description = dto.Description,
                Dosage_Form = dto.DosageForm,
                image_url = dto.ImageUrl,
                DefaultAfterOpeningValue = dto.DefaultAfterOpeningValue,
                DefaultAfterOpeningUnit = MedicationExpiryHelper.NormalizeDefaultUnit(dto.DefaultAfterOpeningUnit),
                RequiresOpeningTracking = dto.RequiresOpeningTracking,
                AfterOpeningNote = dto.AfterOpeningNote
            };

            _context.Medications.Add(medication);
            await _context.SaveChangesAsync();
            var notifiedUsers = await NotifyMissingMedicationRequestUsersAsync(medication);

            return Ok(new
            {
                Message = "Medication added successfully.",
                MedicationId = medication.ID,
                MedicationName = medication.Trade_name,
                medication.DefaultAfterOpeningValue,
                medication.DefaultAfterOpeningUnit,
                medication.RequiresOpeningTracking,
                medication.AfterOpeningNote,
                NotifiedMissingMedicationRequestUsers = notifiedUsers
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateMedication(int id, CreateMedicationDto dto)
        {
            var medication = await _context.Medications
                .FirstOrDefaultAsync(m => m.ID == id);

            if (medication == null)
                return NotFound(new { Message = "Medication not found." });

            var defaultError = MedicationExpiryHelper.ValidateMedicationDefault(
                dto.DefaultAfterOpeningValue,
                dto.DefaultAfterOpeningUnit);
            if (defaultError != null)
                return BadRequest(new { Message = defaultError });

            if (string.IsNullOrWhiteSpace(dto.TradeName))
                return BadRequest(new { Message = "Trade name is required." });

            var normalizedName = dto.TradeName.Trim();
            var exists = await _context.Medications
                .AnyAsync(m => m.ID != id && m.Trade_name == normalizedName);

            if (exists)
                return BadRequest(new { Message = "Medication with this name already exists." });

            medication.Trade_name = normalizedName;
            medication.Description = dto.Description;
            medication.Dosage_Form = dto.DosageForm;
            medication.image_url = dto.ImageUrl;
            medication.DefaultAfterOpeningValue = dto.DefaultAfterOpeningValue;
            medication.DefaultAfterOpeningUnit = MedicationExpiryHelper.NormalizeDefaultUnit(dto.DefaultAfterOpeningUnit);
            medication.RequiresOpeningTracking = dto.RequiresOpeningTracking;
            medication.AfterOpeningNote = dto.AfterOpeningNote;

            await _context.SaveChangesAsync();
            var notifiedUsers = await NotifyMissingMedicationRequestUsersAsync(medication);

            return Ok(new
            {
                Message = "Medication updated successfully.",
                NotifiedMissingMedicationRequestUsers = notifiedUsers,
                Medication = new
                {
                    medication.ID,
                    medication.Trade_name,
                    medication.Description,
                    medication.Dosage_Form,
                    medication.image_url,
                    medication.DefaultAfterOpeningValue,
                    medication.DefaultAfterOpeningUnit,
                    medication.RequiresOpeningTracking,
                    medication.AfterOpeningNote
                }
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

        private async Task<int> NotifyMissingMedicationRequestUsersAsync(Medication medication)
        {
            if (string.IsNullOrWhiteSpace(medication.Trade_name))
                return 0;

            var medicationName = medication.Trade_name.Trim();
            var tickets = await _context.SupportTickets
                .Where(t => t.Category == SupportCategory.MissingMedicationRequest
                    && t.Status != SupportStatus.Closed
                    && t.Message.Contains("Medication name:"))
                .ToListAsync();

            var matchingTickets = tickets
                .Where(t => MissingMedicationNameMatches(t.Message, medicationName))
                .ToList();

            if (matchingTickets.Count == 0)
                return 0;

            var now = DateTime.UtcNow;
            foreach (var ticket in matchingTickets)
            {
                ticket.Status = SupportStatus.Closed;
                ticket.AdminReply ??= $"'{medicationName}' has been added to the medication database.";
                ticket.RepliedAt ??= now;

                _context.Alerts.Add(new Alert
                {
                    UserId = ticket.UserId,
                    Type = "AdminMessage",
                    Title = "Medication Added to Database",
                    Message = $"'{medicationName}' is now available in the medication database.",
                    IsRead = false,
                    ScheduledAt = now,
                    CreatedAt = now
                });
            }

            await _context.SaveChangesAsync();
            return matchingTickets.Select(t => t.UserId).Distinct().Count();
        }

        private static bool MissingMedicationNameMatches(string message, string medicationName)
        {
            var lines = message.Split('\n', StringSplitOptions.TrimEntries);
            var nameLine = lines.FirstOrDefault(line =>
                line.StartsWith("Medication name:", StringComparison.OrdinalIgnoreCase));

            if (nameLine == null)
                return false;

            var requestedName = nameLine["Medication name:".Length..].Trim();
            return requestedName.Equals(medicationName, StringComparison.OrdinalIgnoreCase);
        }
    }
}

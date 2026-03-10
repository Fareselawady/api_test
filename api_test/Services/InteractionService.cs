using api_test.Data;
using Microsoft.EntityFrameworkCore;

namespace api_test.Services
{
    public class InteractionService : IInteractionService
    {
        private readonly AppDbContext _context;

        public InteractionService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// For each medication the user already has, queries the Drug_Interactions
        /// table for interactions with the new medication — exactly the same logic
        /// used by GET /api/medications/check-interaction, kept internal so we
        /// never make an HTTP call from the server to itself.
        /// </summary>
        public async Task<List<string>> CheckInteractionsForNewMedAsync(
            int userId, string newMedName)
        {
            var warnings = new List<string>();

            // ── 1. Resolve the new medication ─────────────────────────────────
            var newMed = await _context.Medications
                .FirstOrDefaultAsync(m => m.Trade_name == newMedName);

            if (newMed == null)
                return warnings; // Unknown med → nothing to check; controller already validated it

            // ── 2. Get the ingredient IDs of the new medication ───────────────
            var newMedIngredients = await _context.Med_Ingredients_Link
                .Where(m => m.Med_id == newMed.ID)
                .Select(m => m.Ingredient_id)
                .ToListAsync();

            if (newMedIngredients.Count == 0)
                return warnings; // No ingredients on record → no interactions possible

            // ── 3. Get trade names of every medication the user already has ───
            //    We skip the new medication itself (already saved, same MedId)
            //    so we query by MedId != newMed.ID to avoid a self-check.
            var existingMedNames = await _context.UserMedications
                .Where(um => um.UserId == userId && um.MedId != newMed.ID)
                .Include(um => um.Medication)
                .Select(um => um.Medication.Trade_name)
                .Distinct()
                .ToListAsync();

            if (existingMedNames.Count == 0)
                return warnings; // First medication for this user — nothing to cross-check

            // ── 4. For each existing medication, run the same interaction query
            //       that CheckDrugInteraction uses in MedicationsController ────
            foreach (var existingName in existingMedNames)
            {
                if (string.IsNullOrWhiteSpace(existingName)) continue;

                var existingMed = await _context.Medications
                    .FirstOrDefaultAsync(m => m.Trade_name == existingName);

                if (existingMed == null) continue;

                var existingIngredients = await _context.Med_Ingredients_Link
                    .Where(m => m.Med_id == existingMed.ID)
                    .Select(m => m.Ingredient_id)
                    .ToListAsync();

                // Mirror the bidirectional check from MedicationsController exactly:
                //   (newMed has Ingredient_1 AND existing has Ingredient_2)
                //   OR
                //   (newMed has Ingredient_2 AND existing has Ingredient_1)
                var interactions = await _context.Drug_Interactions
                    .Where(di =>
                        (newMedIngredients.Contains(di.Ingredient_1_id!.Value)
                            && existingIngredients.Contains(di.Ingredient_2_id!.Value)) ||
                        (newMedIngredients.Contains(di.Ingredient_2_id!.Value)
                            && existingIngredients.Contains(di.Ingredient_1_id!.Value))
                    )
                    .ToListAsync();

                foreach (var interaction in interactions)
                {
                    // Build a readable warning; include interaction type when available
                    var type = string.IsNullOrWhiteSpace(interaction.Interaction_type)
                        ? string.Empty
                        : $" ({interaction.Interaction_type})";

                    warnings.Add(
                        $"Warning: '{newMedName}' may interact with '{existingName}'{type}.");
                }
            }

            return warnings;
        }
    }
}
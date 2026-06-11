namespace api_test.Services
{
    public static class MedicationQuantityHelper
    {
        private const string DefaultUnit = "unit";

        public static string GetSuggestedUnit(string? dosageForm)
        {
            var normalized = Normalize(dosageForm);

            return normalized switch
            {
                "TABLET" or "TAB" or "TAB_EC" or "TAB_SR" or "TAB_EFF" or "TAB_CHEW"
                    or "TAB_MR" or "TAB_PR" or "TAB_ER" or "TAB_SC" or "TAB_DISPERSIBLE" => "tablet",

                "CAP" or "CAPSULE" or "CAPSULE_SG" or "CAPSULE_SR" or "CAPSULE_EC" or "CAPSULE_SG_EC" => "capsule",

                "SYRUP" or "SUSPENSION" or "ORAL_SOLUTION" => "ml",

                "ORAL_DROP" or "ORAL_DROPS" or "EYE_DROP" or "EYE_DROPS" or "OPHTHALMIC_SOLUTION" => "drops",

                "INJECTION" or "AMPOULE" => "ampoule",

                "CREAM" or "GEL" or "EMULGEL" or "OINTMENT" => "g",

                "INHALER" => "puffs",

                "OTHER" => DefaultUnit,

                "TOPICAL_PATCH" => "patch",

                "VIAL_POWDER" or "VIAL" => "vial",

                "SUPPOSITORY" or "SUPPOSITORIES" => "suppository",

                "SACHET" or "SACHETS" or "EC_GRANULES_SACHETS" => "sachet",

                _ => DefaultUnit
            };
        }

        public static string ResolveUnit(string? dosageForm, string? requestedUnit)
        {
            var suggested = GetSuggestedUnit(dosageForm);
            return string.IsNullOrWhiteSpace(requestedUnit)
                ? suggested
                : requestedUnit.Trim();
        }

        public static string? ValidateUnit(string? dosageForm, string? requestedUnit)
        {
            if (string.IsNullOrWhiteSpace(requestedUnit))
                return null;

            var suggested = GetSuggestedUnit(dosageForm);
            if (suggested == DefaultUnit)
                return null;

            if (requestedUnit.Trim().Equals(suggested, StringComparison.OrdinalIgnoreCase))
                return null;

            return $"quantityUnit '{requestedUnit}' does not match dosageForm '{dosageForm}'. Expected '{suggested}'.";
        }

        public static decimal? ResolveQuantity(decimal? quantity, int? legacyValue)
        {
            if (quantity.HasValue)
                return quantity.Value;

            return legacyValue.HasValue ? legacyValue.Value : null;
        }

        public static int? ResolveLegacyCount(int? legacyValue, decimal? quantity)
        {
            if (legacyValue.HasValue)
                return legacyValue.Value;

            if (!quantity.HasValue)
                return null;

            return decimal.Truncate(quantity.Value) == quantity.Value
                && quantity.Value <= int.MaxValue
                && quantity.Value >= int.MinValue
                    ? (int)quantity.Value
                    : null;
        }

        public static bool HasInvalidQuantity(decimal? value)
            => value.HasValue && value.Value < 0;

        public static bool HasInvalidDoseQuantity(decimal? value)
            => value.HasValue && value.Value <= 0;

        private static string Normalize(string? dosageForm)
        {
            if (string.IsNullOrWhiteSpace(dosageForm))
                return string.Empty;

            return dosageForm.Trim()
                .Replace("-", "_")
                .Replace(" ", "_")
                .ToUpperInvariant();
        }
    }
}

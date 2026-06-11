using api_test.Entities;

namespace api_test.Services
{
    public static class UserMedicationFeatureHelper
    {
        public const string CustomMedicationWarning =
            "This medication was added manually. Some features like drug interactions and active ingredient warnings may not be available.";

        public static bool SupportsDatabaseFeatures(UserMedication? userMedication)
            => userMedication?.IsCustomMedication == false
               && userMedication.MedicationId.HasValue;

        public static bool SupportsInteractions(UserMedication? userMedication)
            => SupportsDatabaseFeatures(userMedication);

        public static bool SupportsIngredientWarnings(UserMedication? userMedication)
            => SupportsDatabaseFeatures(userMedication);

        public static string? GetCustomMedicationWarning(UserMedication? userMedication)
            => userMedication is null || SupportsDatabaseFeatures(userMedication)
                ? null
                : CustomMedicationWarning;

        public static string GetDisplayName(
            UserMedication? userMedication,
            ITranslationService? translation = null,
            string lang = "en")
        {
            if (userMedication is null)
                return string.Empty;

            if (SupportsDatabaseFeatures(userMedication)
                && userMedication.MedicationId.HasValue
                && translation is not null)
            {
                var translated = translation.GetMedName(userMedication.MedicationId.Value, lang);
                if (!string.IsNullOrWhiteSpace(translated))
                    return translated;
            }

            if (!string.IsNullOrWhiteSpace(userMedication.MedicationName))
                return userMedication.MedicationName;

            return userMedication.Medication?.Trade_name ?? string.Empty;
        }

        public static string? GetDosageForm(UserMedication? userMedication)
            => string.IsNullOrWhiteSpace(userMedication?.DosageForm)
                ? userMedication?.Medication?.Dosage_Form
                : userMedication.DosageForm;

        public static string? GetQuantityUnit(UserMedication? userMedication)
            => string.IsNullOrWhiteSpace(userMedication?.QuantityUnit)
                ? MedicationQuantityHelper.GetSuggestedUnit(GetDosageForm(userMedication))
                : userMedication.QuantityUnit;
    }
}

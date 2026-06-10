using api_test.Entities;

namespace api_test.Services
{
    public static class MedicationExpiryHelper
    {
        public const string PackageExpiryReason = "PACKAGE_EXPIRY";
        public const string AfterOpeningExpiryReason = "AFTER_OPENING_EXPIRY";
        public const string OpeningDefaultWarning =
            "This is a default duration based on the medication type. Please check the package leaflet or ask a pharmacist.";

        private const string UserSource = "USER";
        private const string MedicationDefaultSource = "MEDICATION_DEFAULT";
        private const string DosageFormDefaultSource = "DOSAGE_FORM_DEFAULT";

        private static readonly Dictionary<string, (int Value, string Unit)> DosageFormDefaults =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Ophthalmic Solution"] = (28, "days"),
                ["Oral_Drop"] = (30, "days"),
                ["Oral Drops"] = (30, "days"),
                ["Syrup"] = (90, "days"),
                ["Suspension"] = (14, "days"),
                ["Oral_Solution"] = (90, "days"),
                ["Gel"] = (90, "days"),
                ["Emulgel"] = (90, "days")
            };

        public static string? ValidateAfterOpeningInput(
            bool isOpened,
            DateTime? openedDate,
            int? durationValue,
            string? durationUnit,
            DateTime now)
        {
            if (openedDate.HasValue && openedDate.Value.Date > now.Date)
                return "openedDate must not be in the future.";

            if (durationValue.HasValue && durationValue <= 0)
                return "afterOpeningDurationValue must be greater than 0.";

            if (!string.IsNullOrWhiteSpace(durationUnit) && NormalizeUnit(durationUnit) == null)
                return "afterOpeningDurationUnit must be one of: days, weeks, months.";

            if (!isOpened && openedDate.HasValue)
                return "openedDate can only be set when isOpened is true.";

            return null;
        }

        public static string? ValidateMedicationDefault(int? durationValue, string? durationUnit)
        {
            if (durationValue.HasValue && durationValue <= 0)
                return "defaultAfterOpeningValue must be greater than 0.";

            if (!string.IsNullOrWhiteSpace(durationUnit) && NormalizeUnit(durationUnit) == null)
                return "defaultAfterOpeningUnit must be one of: days, weeks, months.";

            return null;
        }

        public static bool HasDosageFormDefault(string? dosageForm)
            => TryGetDosageFormDefault(dosageForm, out _);

        public static void Apply(UserMedication userMedication, Medication? medication, DateTime now)
        {
            if (!userMedication.IsOpened)
            {
                userMedication.AfterOpeningExpiryDate = null;
                userMedication.EffectiveExpiryDate = ToDateTime(userMedication.ExpiryDate);
                userMedication.ExpiryReason = PackageExpiryReason;
                userMedication.AfterOpeningSource = null;
                userMedication.AfterOpeningDurationUnit = NormalizeUnit(userMedication.AfterOpeningDurationUnit) ?? "days";
                return;
            }

            userMedication.OpenedDate ??= now.Date;

            var durationValue = userMedication.AfterOpeningDurationValue;
            var durationUnit = NormalizeUnit(userMedication.AfterOpeningDurationUnit) ?? "days";
            var source = string.IsNullOrWhiteSpace(userMedication.AfterOpeningSource)
                ? UserSource
                : userMedication.AfterOpeningSource;

            if (!durationValue.HasValue)
            {
                if (medication?.DefaultAfterOpeningValue is > 0)
                {
                    durationValue = medication.DefaultAfterOpeningValue;
                    durationUnit = NormalizeUnit(medication.DefaultAfterOpeningUnit) ?? "days";
                    source = MedicationDefaultSource;
                }
                else if (TryGetDosageFormDefault(userMedication.DosageForm ?? medication?.Dosage_Form, out var dosageDefault))
                {
                    durationValue = dosageDefault.Value;
                    durationUnit = dosageDefault.Unit;
                    source = DosageFormDefaultSource;
                }
                else
                {
                    userMedication.AfterOpeningExpiryDate = null;
                    userMedication.EffectiveExpiryDate = ToDateTime(userMedication.ExpiryDate);
                    userMedication.ExpiryReason = PackageExpiryReason;
                    userMedication.AfterOpeningSource = null;
                    userMedication.AfterOpeningDurationUnit = durationUnit;
                    return;
                }
            }

            userMedication.AfterOpeningDurationValue = durationValue;
            userMedication.AfterOpeningDurationUnit = durationUnit;
            userMedication.AfterOpeningSource = source;

            var afterOpeningExpiry = AddDuration(userMedication.OpenedDate.Value.Date, durationValue.Value, durationUnit);
            userMedication.AfterOpeningExpiryDate = afterOpeningExpiry;

            var packageExpiry = ToDateTime(userMedication.ExpiryDate);
            if (packageExpiry.HasValue && packageExpiry.Value.Date <= afterOpeningExpiry.Date)
            {
                userMedication.EffectiveExpiryDate = packageExpiry.Value.Date;
                userMedication.ExpiryReason = PackageExpiryReason;
            }
            else
            {
                userMedication.EffectiveExpiryDate = afterOpeningExpiry.Date;
                userMedication.ExpiryReason = AfterOpeningExpiryReason;
            }
        }

        public static DateTime? GetEffectiveExpiryDate(UserMedication userMedication)
            => userMedication.EffectiveExpiryDate ?? ToDateTime(userMedication.ExpiryDate);

        public static string? GetWarning(UserMedication userMedication)
            => userMedication.IsOpened ? OpeningDefaultWarning : null;

        public static string NormalizeDefaultUnit(string? unit)
            => NormalizeUnit(unit) ?? "days";

        private static DateTime? ToDateTime(DateOnly? date)
            => date?.ToDateTime(TimeOnly.MinValue);

        private static DateTime AddDuration(DateTime openedDate, int value, string unit)
        {
            return unit switch
            {
                "weeks" => openedDate.AddDays(value * 7),
                "months" => openedDate.AddMonths(value),
                _ => openedDate.AddDays(value)
            };
        }

        private static string? NormalizeUnit(string? unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
                return null;

            return unit.Trim().ToLowerInvariant() switch
            {
                "day" or "days" => "days",
                "week" or "weeks" => "weeks",
                "month" or "months" => "months",
                _ => null
            };
        }

        private static bool TryGetDosageFormDefault(string? dosageForm, out (int Value, string Unit) value)
        {
            value = default;

            if (string.IsNullOrWhiteSpace(dosageForm))
                return false;

            return DosageFormDefaults.TryGetValue(dosageForm.Trim(), out value);
        }
    }
}

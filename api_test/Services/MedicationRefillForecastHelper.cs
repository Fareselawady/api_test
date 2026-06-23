using api_test.Entities;
using api_test.Models;

namespace api_test.Services
{
    public static class MedicationRefillForecastHelper
    {
        public static RefillForecastDto BuildForecast(UserMedication userMedication, DateTime now)
        {
            var currentQuantity = MedicationQuantityHelper.ResolveQuantity(
                userMedication.CurrentQuantity,
                userMedication.CurrentPillCount);
            var doseQuantity = ResolveDoseQuantity(userMedication);

            if (!currentQuantity.HasValue || currentQuantity.Value <= 0 || doseQuantity <= 0)
            {
                return new RefillForecastDto
                {
                    DosesRemaining = currentQuantity.HasValue && doseQuantity > 0
                        ? 0
                        : null,
                    DaysUntilEmpty = currentQuantity.HasValue && currentQuantity.Value <= 0 ? 0 : null,
                    EstimatedRunOutDate = currentQuantity.HasValue && currentQuantity.Value <= 0 ? now.Date : null,
                    RefillReminderDaysBefore = userMedication.RefillReminderDaysBefore,
                    RefillWarning = currentQuantity.HasValue && currentQuantity.Value <= 0
                };
            }

            var dosesRemaining = Math.Floor(currentQuantity.Value / doseQuantity);
            var dailyQuantity = ResolveDailyQuantity(userMedication, doseQuantity);

            int? daysUntilEmpty = null;
            DateTime? runOutDate = null;
            if (dailyQuantity > 0)
            {
                var daysDecimal = currentQuantity.Value / dailyQuantity;
                daysUntilEmpty = (int)Math.Floor(daysDecimal);
                runOutDate = now.Date.AddDays((double)Math.Ceiling(daysDecimal));
            }

            var refillWarning = false;
            if (daysUntilEmpty.HasValue && userMedication.RefillReminderDaysBefore.HasValue)
            {
                refillWarning = daysUntilEmpty.Value <= userMedication.RefillReminderDaysBefore.Value;
            }

            return new RefillForecastDto
            {
                DosesRemaining = dosesRemaining,
                DaysUntilEmpty = daysUntilEmpty,
                EstimatedRunOutDate = runOutDate,
                RefillReminderDaysBefore = userMedication.RefillReminderDaysBefore,
                RefillWarning = refillWarning
            };
        }

        public static decimal ResolveDoseQuantity(UserMedication userMedication)
        {
            var doseQuantity = MedicationQuantityHelper.ResolveQuantity(
                userMedication.DoseQuantity,
                userMedication.PillsPerDose);

            return doseQuantity.HasValue && doseQuantity.Value > 0
                ? doseQuantity.Value
                : 1;
        }

        private static decimal ResolveDailyQuantity(UserMedication userMedication, decimal doseQuantity)
        {
            var useType = userMedication.MedicationUseType?.Trim();
            if (string.Equals(useType, "AsNeeded", StringComparison.OrdinalIgnoreCase))
            {
                return userMedication.MaxDosesPerDay.HasValue && userMedication.MaxDosesPerDay.Value > 0
                    ? doseQuantity * userMedication.MaxDosesPerDay.Value
                    : 0;
            }

            if (userMedication.IntervalHours.HasValue && userMedication.IntervalHours.Value > 0)
            {
                return doseQuantity * (decimal)(24.0 / userMedication.IntervalHours.Value);
            }

            if (userMedication.DosesPerPeriod.HasValue && userMedication.DosesPerPeriod.Value > 0)
            {
                var periodDays = ResolvePeriodDays(userMedication.PeriodUnit, userMedication.PeriodValue ?? 1);
                if (periodDays > 0)
                    return doseQuantity * userMedication.DosesPerPeriod.Value / periodDays;
            }

            return userMedication.FirstDoseTime.HasValue ? doseQuantity : 0;
        }

        private static decimal ResolvePeriodDays(string? periodUnit, int periodValue)
        {
            if (periodValue <= 0) return 0;

            return (periodUnit ?? "day").Trim().ToLowerInvariant() switch
            {
                "hour" or "hours" => periodValue / 24m,
                "day" or "days" => periodValue,
                "week" or "weeks" => periodValue * 7m,
                "month" or "months" => periodValue * 30m,
                _ => periodValue
            };
        }
    }
}

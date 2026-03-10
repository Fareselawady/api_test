using api_test.Entities;
using api_test.Models;

namespace api_test.Services
{
    public interface IScheduleService
    {
        // ── Generation ────────────────────────────────────────────────────────
        Task GenerateScheduleAsync(UserMedication userMed);

        // ── Queries ───────────────────────────────────────────────────────────

        /// <summary>All schedules for one UserMedication (across all dates).</summary>
        Task<List<MedicationScheduleDto>> GetSchedulesForMedicationAsync(int userMedId, int requestingUserId);

        /// <summary>All pending alerts for a user.</summary>
        Task<List<AlertDto>> GetPendingAlertsAsync(int userId);

        /// <summary>Schedules whose ScheduledAt falls on today (UTC) for a user.</summary>
        Task<List<MedicationScheduleDto>> GetTodaySchedulesAsync(int userId);

        /// <summary>Update status of a single schedule (Taken / Missed / Pending).</summary>
        Task<bool> UpdateScheduleStatusAsync(int scheduleId, string newStatus, int requestingUserId);

        /// <summary>
        /// Schedules whose ScheduledAt falls on a specific date (UTC) for a user.
        /// Used by the mobile home screen week view when the user taps a day.
        /// </summary>
        Task<List<MedicationScheduleDto>> GetSchedulesByDateAsync(int userId, DateOnly date);
    }
}
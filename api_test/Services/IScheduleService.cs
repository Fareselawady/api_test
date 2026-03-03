using api_test.Entities;
using api_test.Models;

namespace api_test.Services
{
    public interface IScheduleService
    {
        // ── Generation (existing) ─────────────────────────────────────────────
        Task GenerateScheduleAsync(UserMedication userMed);

        // ── Queries (new) ─────────────────────────────────────────────────────

        /// <summary>All schedules for one UserMedication (across all dates).</summary>
        Task<List<MedicationScheduleDto>> GetSchedulesForMedicationAsync(int userMedId, int requestingUserId);

        /// <summary>All pending alerts for a user.</summary>
        Task<List<AlertDto>> GetPendingAlertsAsync(int userId);

        /// <summary>Schedules whose ScheduledAt falls on today (UTC) for a user.</summary>
        Task<List<MedicationScheduleDto>> GetTodaySchedulesAsync(int userId);

        /// <summary>Update status of a single schedule (Taken / Missed / Pending).</summary>
        Task<bool> UpdateScheduleStatusAsync(int scheduleId, string newStatus, int requestingUserId);
    }
}
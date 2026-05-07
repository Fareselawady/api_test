using api_test.Entities;
using api_test.Models;

namespace api_test.Services
{
    public interface IScheduleService
    {
        // ── Generation ────────────────────────────────────────────────────────
        Task GenerateScheduleAsync(UserMedication userMed);

        /// <summary>
        /// Generates schedules using exact custom dose times (e.g. 08:00, 14:00, 22:00).
        /// Called instead of GenerateScheduleAsync when the request contains doseTimes.
        /// </summary>
        Task GenerateScheduleWithDoseTimesAsync(UserMedication userMed, List<TimeOnly> doseTimes);

        // ── Queries ───────────────────────────────────────────────────────────
        Task<List<MedicationScheduleDto>> GetSchedulesForMedicationAsync(int userMedId, int requestingUserId);
        Task<List<AlertDto>> GetPendingAlertsAsync(int userId);
        Task<List<MedicationScheduleDto>> GetTodaySchedulesAsync(int userId);
        Task<List<MedicationScheduleDto>> GetSchedulesByDateAsync(int userId, DateOnly date);

        // ── Status Updates ────────────────────────────────────────────────────
        Task<bool> UpdateScheduleStatusAsync(int scheduleId, string newStatus, int requestingUserId);
        Task<TakeDoseResult> TakeDoseAsync(int scheduleId, int requestingUserId);

        // ── Snooze ────────────────────────────────────────────────────────────
        Task<SnoozeResult> SnoozeAsync(int scheduleId, int requestingUserId);

        Task RegenerateScheduleAsync(UserMedication userMed);
    }
}
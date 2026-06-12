using api_test.Entities;
using api_test.Models;
namespace api_test.Services
{
    public interface IScheduleService
    {
        // ── Generation ────────────────────────────────────────────────────────
        Task GenerateScheduleAsync(UserMedication userMed);
        Task GenerateScheduleWithDoseTimesAsync(UserMedication userMed, List<TimeOnly> doseTimes);

        // ── Queries ───────────────────────────────────────────────────────────
        Task<List<MedicationScheduleDto>> GetSchedulesForMedicationAsync(int userMedId, int requestingUserId, string lang = "en");
        Task<List<AlertDto>> GetPendingAlertsAsync(int userId);
        Task<List<MedicationScheduleDto>> GetTodaySchedulesAsync(int userId, string lang = "en");
        Task<List<MedicationScheduleDto>> GetSchedulesByDateAsync(int userId, DateOnly date, string lang = "en");

        // ── Status Updates ────────────────────────────────────────────────────
        Task<bool> UpdateScheduleStatusAsync(int scheduleId, string newStatus, int requestingUserId);
        Task<TakeDoseResult> TakeDoseAsync(int scheduleId, int requestingUserId);

        // ── Snooze ────────────────────────────────────────────────────────────
        Task<SnoozeResult> SnoozeAsync(int scheduleId, int requestingUserId, int minutes = 15);

        // ── Skip ──────────────────────────────────────────────────────────────
        Task<SkipDoseResult> SkipDoseAsync(int scheduleId, int requestingUserId);

        Task RegenerateScheduleAsync(UserMedication userMed);
        Task RegenerateScheduleWithDoseTimesAsync(UserMedication userMed, List<TimeOnly> doseTimes);
    }
}
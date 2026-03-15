using api_test.Entities;
using api_test.Models;

namespace api_test.Services
{
    public interface IScheduleService
    {
        // ── Generation ────────────────────────────────────────────────────────
        Task GenerateScheduleAsync(UserMedication userMed);

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
    }
}
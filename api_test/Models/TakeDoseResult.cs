namespace api_test.Models
{
    public class TakeDoseResult
    {
        public bool Succeeded { get; private set; }
        public string? Error { get; private set; }
        public int ScheduleId { get; private set; }
        public int PillsDeducted { get; private set; }
        public int? RemainingPills { get; private set; }
        public bool LowStockAlertCreated { get; private set; }

        public static TakeDoseResult Success(
            int scheduleId, int pillsDeducted, int? remainingPills, bool lowStockAlert) =>
            new TakeDoseResult
            {
                Succeeded = true,
                ScheduleId = scheduleId,
                PillsDeducted = pillsDeducted,
                RemainingPills = remainingPills,
                LowStockAlertCreated = lowStockAlert
            };

        public static TakeDoseResult NotFound() =>
            new TakeDoseResult { Succeeded = false, Error = "Schedule not found or access denied." };

        public static TakeDoseResult AlreadyTaken() =>
            new TakeDoseResult { Succeeded = false, Error = "This dose has already been marked as taken." };
    }
}
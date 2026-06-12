namespace api_test.Models
{
    public class SkipDoseResult
    {
        public bool Succeeded { get; private set; }
        public string? Error { get; private set; }
        public int ScheduleId { get; private set; }
        public string? Status { get; private set; }
        public int PillsDeducted { get; private set; } = 0;
        public string? Message { get; private set; }

        public static SkipDoseResult Success(int scheduleId) =>
            new SkipDoseResult
            {
                Succeeded = true,
                ScheduleId = scheduleId,
                Status = "Skipped",
                PillsDeducted = 0,
                Message = "Dose marked as skipped."
            };

        public static SkipDoseResult NotFound() =>
            new SkipDoseResult { Succeeded = false, Error = "Schedule not found or access denied." };

        public static SkipDoseResult AlreadyTaken() =>
            new SkipDoseResult { Succeeded = false, Error = "Cannot skip a dose that is already taken." };

        public static SkipDoseResult AlreadySkipped() =>
            new SkipDoseResult { Succeeded = false, Error = "Dose is already skipped." };

        public static SkipDoseResult AlreadyMissed() =>
            new SkipDoseResult { Succeeded = false, Error = "Dose is already missed." };

        public static SkipDoseResult InvalidStatus(string status) =>
            new SkipDoseResult { Succeeded = false, Error = $"Cannot skip a dose with status '{status}'." };
    }
}
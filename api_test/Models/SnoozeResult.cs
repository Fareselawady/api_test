namespace api_test.Models
{
    public class SnoozeResult
    {
        public bool Succeeded { get; private set; }
        public string? Error { get; private set; }
        public int ScheduleId { get; private set; }
        public int SnoozeCount { get; private set; }
        public DateTime? NextReminderAt { get; private set; }

        public static SnoozeResult Success(int scheduleId, int snoozeCount, DateTime nextReminderAt) =>
            new SnoozeResult
            {
                Succeeded = true,
                ScheduleId = scheduleId,
                SnoozeCount = snoozeCount,
                NextReminderAt = nextReminderAt
            };

        public static SnoozeResult NotFound() =>
            new SnoozeResult { Succeeded = false, Error = "Schedule not found or access denied." };

        public static SnoozeResult AlreadyTaken() =>
            new SnoozeResult { Succeeded = false, Error = "Cannot snooze a dose that has already been taken." };

        public static SnoozeResult AlreadyMissed() =>
            new SnoozeResult { Succeeded = false, Error = "Cannot snooze a dose that has already been marked as missed." };
    }
}
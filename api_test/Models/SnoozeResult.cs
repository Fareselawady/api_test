namespace api_test.Models
{
    public class SnoozeResult
    {
        public bool Succeeded { get; private set; }
        public string? Error { get; private set; }
        public int ScheduleId { get; private set; }
        public string? Status { get; private set; }
        public int SnoozeCount { get; private set; }
        public bool SnoozeLimitReached { get; private set; }
        public DateTime? OldScheduledAt { get; private set; }
        public DateTime? NewScheduledAt { get; private set; }
        public DateTime? OldNotificationTime { get; private set; }
        public DateTime? NewNotificationTime { get; private set; }
        public string? Message { get; private set; }

        private const int MaxSnoozeCount = 2;

        public static SnoozeResult Success(
            int scheduleId,
            int snoozeCount,
            DateTime oldScheduledAt,
            DateTime newScheduledAt,
            DateTime oldNotificationTime,
            DateTime newNotificationTime,
            int minutes) =>
            new SnoozeResult
            {
                Succeeded = true,
                ScheduleId = scheduleId,
                Status = "Snoozed",
                SnoozeCount = snoozeCount,
                SnoozeLimitReached = snoozeCount >= MaxSnoozeCount,
                OldScheduledAt = oldScheduledAt,
                NewScheduledAt = newScheduledAt,
                OldNotificationTime = oldNotificationTime,
                NewNotificationTime = newNotificationTime,
                Message = snoozeCount >= MaxSnoozeCount
                    ? $"Dose snoozed for {minutes} minutes. Snooze limit reached."
                    : $"Dose snoozed for {minutes} minutes."
            };

        public static SnoozeResult LimitReached(int scheduleId, int snoozeCount) =>
            new SnoozeResult
            {
                Succeeded = false,
                Error = "Maximum snooze count reached.",
                ScheduleId = scheduleId,
                Status = "Snoozed",
                SnoozeCount = snoozeCount,
                SnoozeLimitReached = true
            };

        public static SnoozeResult NotFound() =>
            new SnoozeResult { Succeeded = false, Error = "Schedule not found or access denied." };

        public static SnoozeResult AlreadyTaken() =>
            new SnoozeResult { Succeeded = false, Error = "Cannot snooze a dose that is already taken." };

        public static SnoozeResult AlreadyMissed() =>
            new SnoozeResult { Succeeded = false, Error = "Cannot snooze a missed dose." };

        public static SnoozeResult InvalidStatus(string status) =>
            new SnoozeResult { Succeeded = false, Error = $"Cannot snooze a dose with status '{status}'." };
    }
}
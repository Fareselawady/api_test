// Models/ScheduleEntryDto.cs
namespace api_test.Models
{
    public class ScheduleEntryDto
    {
        public int UserMedId { get; set; }
        public DateTime ScheduledAt { get; set; }
        public DateTime NotificationTime { get; set; }
    }
}

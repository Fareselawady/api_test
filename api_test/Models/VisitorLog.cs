namespace api_test.Models
{
    public class VisitorLog
    {
        public int Id { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public DateTime VisitedAt { get; set; }
    }
}

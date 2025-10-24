namespace api_test.Models
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public List<DrugDto> Drugs { get; set; } = new List<DrugDto>();
    }
}

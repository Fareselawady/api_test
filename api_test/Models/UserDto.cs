using System.Text.Json.Serialization;

namespace api_test.Models
{
   
        public class UserDto
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;

              [JsonIgnore]
        public int RoleId { get; set; } 
         }
    
}

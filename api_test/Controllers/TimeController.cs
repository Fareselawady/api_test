using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace api_test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TimeController : ControllerBase
    {
        [HttpGet("api/debug/time")]
        [AllowAnonymous]
        public ActionResult GetTime()
        {
            return Ok(new
            {
                UtcNow = DateTime.UtcNow,
                LocalNow = DateTime.Now
            });
        }
    }
}

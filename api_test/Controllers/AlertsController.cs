using api_test.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace api_test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AlertsController : ControllerBase
    {

        private readonly AlertService _alertService;

        public AlertsController(AlertService alertService)
        {
            _alertService = alertService;
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPending()
        {
            int userId = GetCurrentUserId();
            var alerts = await _alertService.GetPendingAlertsAsync(userId);
            return Ok(alerts);
        }

        [HttpPost("{alertId}/read")]
        public async Task<IActionResult> MarkAsRead(int alertId)
        {
            await _alertService.MarkAlertAsReadAsync(alertId);
            return Ok();
        }

        private int GetCurrentUserId()
        {
            // TODO: replace with your auth logic
            return 1;
        }
    }
}

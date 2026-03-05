using Microsoft.AspNetCore.Mvc;

namespace PlaySpace.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public ActionResult<object> GetHealth()
        {
            return Ok(new
            {
                Status = "Healthy",
                Message = "Spotto API is running successfully!",
                Timestamp = DateTime.UtcNow,
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                Version = "1.0.0"
            });
        }

        [HttpGet("ping")]
        public ActionResult<string> Ping()
        {
            return Ok("Pong! API is alive.");
        }
    }
}
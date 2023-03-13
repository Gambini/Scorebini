using Microsoft.AspNetCore.Mvc;
using ScorebiniTwitchApi.Shared.Responses;

namespace ScorebiniTwitchApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Produces("application/json")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BasicResponse))]
        public IActionResult Get()
        {
            return Ok(new BasicResponse(new(200, "Healthy")));
        }

    }
}

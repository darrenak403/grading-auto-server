using Microsoft.AspNetCore.Mvc;
using givenAPI.Models;
using System.Linq;

namespace givenAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExpertisesController : ControllerBase
    {
        // GET /api/expertises
        [HttpGet]
        public IActionResult GetExpertises()
        {
            var expertises = DataInitializer.Instructors
                .Select(i => i.Expertise)
                .Distinct()
                .ToList();
            return Ok(expertises);
        }
    }
}
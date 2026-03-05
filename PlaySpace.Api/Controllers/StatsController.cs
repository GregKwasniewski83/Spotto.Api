using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StatsController : ControllerBase
{
    private readonly ITrainingService _trainingService;
    private readonly IReservationService _reservationService;
    private readonly ISocialWallPostService _socialWallPostService;

    public StatsController(
        ITrainingService trainingService,
        IReservationService reservationService,
        ISocialWallPostService socialWallPostService)
    {
        _trainingService = trainingService;
        _reservationService = reservationService;
        _socialWallPostService = socialWallPostService;
    }

    [HttpGet("trainings")]
    public ActionResult<StatsDto> GetTotalTrainings()
    {
        try
        {
            var count = _trainingService.GetTotalTrainingsCount();
            return Ok(new StatsDto
            {
                Count = count,
                Description = "Total trainings created by users"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving training statistics", error = ex.Message });
        }
    }

    [HttpGet("reservations")]
    public ActionResult<StatsDto> GetTotalReservations()
    {
        try
        {
            var count = _reservationService.GetTotalReservationsCount();
            return Ok(new StatsDto
            {
                Count = count,
                Description = "Total reservations made by users"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving reservation statistics", error = ex.Message });
        }
    }

    [HttpGet("social-posts")]
    public ActionResult<StatsDto> GetTotalSocialPosts()
    {
        try
        {
            var count = _socialWallPostService.GetTotalPostsCount();
            return Ok(new StatsDto
            {
                Count = count,
                Description = "Total social posts made by users"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving social post statistics", error = ex.Message });
        }
    }
}
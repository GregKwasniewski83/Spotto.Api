using Microsoft.AspNetCore.Http;

namespace PlaySpace.Domain.DTOs;

public class AvatarUploadDto
{
    public IFormFile Avatar { get; set; } = null!;
}

public class FacilityPlanUploadDto
{
    public IFormFile FacilityPlan { get; set; } = null!;
}
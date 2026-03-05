using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Domain.Attributes;
using PlaySpace.Services.Interfaces;
using System.Security.Claims;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/business-profile")]
[Authorize]
public class BusinessProfileController : ControllerBase
{
    private readonly IBusinessProfileService _businessProfileService;
    private readonly IFacilityService _facilityService;
    private readonly IAgentManagementService _agentManagementService;
    private readonly IProductService _productService;
    private readonly IUserFavouriteService _userFavouriteService;
    private readonly ITrainerBusinessAssociationService _associationService;
    private readonly ISalesReportService _salesReportService;

    public BusinessProfileController(
        IBusinessProfileService businessProfileService,
        IFacilityService facilityService,
        IAgentManagementService agentManagementService,
        IProductService productService,
        IUserFavouriteService userFavouriteService,
        ITrainerBusinessAssociationService associationService,
        ISalesReportService salesReportService)
    {
        _businessProfileService = businessProfileService;
        _facilityService = facilityService;
        _agentManagementService = agentManagementService;
        _productService = productService;
        _userFavouriteService = userFavouriteService;
        _associationService = associationService;
        _salesReportService = salesReportService;
    }

    [HttpGet]
    public ActionResult<BusinessProfileDto> GetBusinessProfile()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var profile = _businessProfileService.GetBusinessProfileByUserId(userId);
            if (profile == null)
            {
                return NotFound("Business profile not found");
            }

            return Ok(profile);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving business profile", error = ex.Message });
        }
    }

    [HttpGet("{businessProfileId}")]
    [AllowAnonymous]
    public ActionResult<BusinessProfileDetailDto> GetBusinessProfileDetail(Guid businessProfileId)
    {
        try
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<BusinessProfileController>>();
            logger.LogInformation("Retrieving business profile detail for {BusinessProfileId}", businessProfileId);

            var profile = _businessProfileService.GetBusinessProfileById(businessProfileId);
            if (profile == null)
            {
                return NotFound("Business profile not found");
            }

            var facilities = _facilityService.GetUserFacilities(profile.UserId);
            var products = _productService.GetProductsByBusinessProfile(businessProfileId);

            var detailDto = new BusinessProfileDetailDto
            {
                Id = profile.Id.ToString(),
                BusinessName = profile.DisplayName ?? profile.CompanyName,
                Nip = profile.Nip,
                Email = profile.Email,
                Phone = profile.PhoneNumber,
                Address = profile.Address,
                City = profile.City,
                PostalCode = profile.PostalCode,
                Description = profile.WebsiteDescription,
                Facilities = facilities.ToArray(),
                Products = products.ToArray()
            };

            return Ok(detailDto);
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<BusinessProfileController>>();
            logger.LogError(ex, "Failed to retrieve business profile detail: {Message}", ex.Message);
            return StatusCode(500, new { message = "An error occurred while retrieving business profile detail", error = ex.Message });
        }
    }

    /// <summary>
    /// Get public business profile information (no authentication required)
    /// </summary>
    [HttpGet("{businessProfileId}/public")]
    [AllowAnonymous]
    public ActionResult<BusinessProfilePublicDto> GetBusinessProfilePublic(Guid businessProfileId)
    {
        try
        {
            var profile = _businessProfileService.GetBusinessProfileById(businessProfileId);
            if (profile == null)
            {
                return NotFound("Business profile not found");
            }

            var facilities = _facilityService.GetUserFacilities(profile.UserId);
            var facilityTypes = facilities
                .Select(f => f.Type)
                .Distinct()
                .ToList();

            var publicDto = new BusinessProfilePublicDto
            {
                Id = profile.Id.ToString(),
                DisplayName = profile.DisplayName,
                CompanyName = profile.CompanyName,
                Description = profile.WebsiteDescription,
                Address = profile.Address,
                City = profile.City,
                PostalCode = profile.PostalCode,
                Latitude = profile.Latitude,
                Longitude = profile.Longitude,
                AvatarUrl = profile.AvatarUrl,
                Email = profile.Email,
                Phone = profile.PhoneNumber,
                FacilityTypes = facilityTypes
            };

            return Ok(publicDto);
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<BusinessProfileController>>();
            logger.LogError(ex, "Failed to retrieve public business profile: {Message}", ex.Message);
            return StatusCode(500, new { message = "An error occurred while retrieving public business profile", error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<BusinessProfileDto>> CreateBusinessProfile([FromBody] CreateBusinessProfileDto profileDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var profile = await _businessProfileService.CreateBusinessProfile(profileDto, userId);
            
            // Return additional information about TPay registration
            var response = new
            {
                businessProfile = profile,
                tpayRegistration = new
                {
                    isRegistered = !string.IsNullOrEmpty(profile.TPayMerchantId),
                    merchantId = profile.TPayMerchantId,
                    activationLink = profile.TPayActivationLink,
                    verificationStatus = profile.TPayVerificationStatus
                }
            };
            
            return CreatedAtAction(nameof(GetBusinessProfile), new { }, response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating business profile", error = ex.Message });
        }
    }

    [HttpPost("multipart")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<BusinessProfileDto>> CreateBusinessProfileMultipart([FromForm] CreateBusinessProfileMultipartDto profileDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            // Log request details (without file data)
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<BusinessProfileController>>();
            logger.LogInformation("Creating business profile multipart for user {UserId}", userId);
            logger.LogInformation("Request data - CompanyName: {CompanyName}, DisplayName: {DisplayName}, Nip: {Nip}, Email: {Email}, HasFacilityPlan: {HasFacilityPlan}", 
                profileDto.CompanyName, profileDto.DisplayName, profileDto.Nip, profileDto.Email, profileDto.FacilityPlan != null);
            logger.LogInformation("Schedule data present - WeekdaysSchedule: {HasWeekdays}, SaturdaySchedule: {HasSaturday}, SundaySchedule: {HasSunday}, DateSpecificAvailability: {HasDateSpecific}",
                !string.IsNullOrEmpty(profileDto.WeekdaysSchedule), !string.IsNullOrEmpty(profileDto.SaturdaySchedule), 
                !string.IsNullOrEmpty(profileDto.SundaySchedule), !string.IsNullOrEmpty(profileDto.DateSpecificAvailability));

            // Convert multipart DTO to regular DTO
            logger.LogInformation("Starting multipart DTO conversion...");
            var convertedDto = await ConvertMultipartToCreateDto(profileDto);
            logger.LogInformation("Multipart DTO conversion completed successfully");
            logger.LogInformation("Converted schedule data - WeekdaysSchedule count: {WeekdaysCount}, SaturdaySchedule count: {SaturdayCount}, SundaySchedule count: {SundayCount}, DateSpecificAvailability count: {DateSpecificCount}",
                convertedDto.WeekdaysSchedule.Count, convertedDto.SaturdaySchedule.Count, convertedDto.SundaySchedule.Count, convertedDto.DateSpecificAvailability.Count);
            
            logger.LogInformation("Calling business profile service CreateBusinessProfile...");
            var profile = await _businessProfileService.CreateBusinessProfile(convertedDto, userId);
            logger.LogInformation("Business profile created successfully with ID: {ProfileId}", profile.Id);
            
            // Return additional information about TPay registration
            var response = new
            {
                businessProfile = profile,
                tpayRegistration = new
                {
                    isRegistered = !string.IsNullOrEmpty(profile.TPayMerchantId),
                    merchantId = profile.TPayMerchantId,
                    activationLink = profile.TPayActivationLink,
                    verificationStatus = profile.TPayVerificationStatus
                }
            };
            
            return CreatedAtAction(nameof(GetBusinessProfile), new { }, response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating business profile", error = ex.Message });
        }
    }

    [HttpPut("{businessProfileId}")]
    public async Task<ActionResult<BusinessProfileDto>> UpdateBusinessProfile(Guid businessProfileId, [FromBody] UpdateBusinessProfileDto profileDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            var profile = await _businessProfileService.UpdateBusinessProfile(businessProfileId, profileDto);
            if (profile == null)
            {
                return NotFound("Business profile not found");
            }

            return Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating business profile", error = ex.Message });
        }
    }

    [HttpPut("{businessProfileId}/multipart")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<BusinessProfileDto>> UpdateBusinessProfileMultipart(Guid businessProfileId, [FromForm] UpdateBusinessProfileMultipartDto profileDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            // Convert multipart DTO to regular DTO
            var convertedDto = await ConvertMultipartToUpdateDto(profileDto);

            var profile = await _businessProfileService.UpdateBusinessProfile(businessProfileId, convertedDto);
            if (profile == null)
            {
                return NotFound("Business profile not found");
            }

            return Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating business profile", error = ex.Message });
        }
    }

    [HttpDelete("{businessProfileId}")]
    public async Task<ActionResult> DeleteBusinessProfile(Guid businessProfileId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            var deleted = _businessProfileService.DeleteBusinessProfile(businessProfileId);
            if (!deleted)
            {
                return NotFound("Business profile not found");
            }

            return Ok(new { message = "Business profile deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while deleting business profile", error = ex.Message });
        }
    }

    [HttpGet("{businessProfileId}/schedule")]
    public async Task<ActionResult<List<ScheduleSlotDto>>> GetScheduleForDate(Guid businessProfileId, [FromQuery] string date)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            if (!DateTime.TryParse(date, out var parsedDate))
            {
                return BadRequest("Invalid date format");
            }

            var schedule = _businessProfileService.GetScheduleForDate(businessProfileId, parsedDate);
            return Ok(schedule);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving schedule", error = ex.Message });
        }
    }

    [HttpPost("{businessProfileId}/avatar")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult> UploadAvatar(Guid businessProfileId, [FromForm] AvatarUploadDto uploadDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            if (uploadDto.Avatar == null || uploadDto.Avatar.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
            if (!allowedTypes.Contains(uploadDto.Avatar.ContentType.ToLower()))
            {
                return BadRequest("Invalid file type. Only JPEG, PNG and GIF images are allowed.");
            }

            // Validate file size (max 5MB)
            if (uploadDto.Avatar.Length > 5 * 1024 * 1024)
            {
                return BadRequest("File size too large. Maximum size is 5MB.");
            }

            var avatarUrl = await _businessProfileService.UploadAvatarAsync(businessProfileId, uploadDto.Avatar);
            
            return Ok(new { avatarUrl = avatarUrl });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while uploading avatar", error = ex.Message });
        }
    }

    [HttpGet("{businessProfileId}/facilities")]
    public async Task<ActionResult<List<FacilityDto>>> GetBusinessProfileFacilities(Guid businessProfileId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            // Check user roles
            var isBusinessOwner = User.IsInRole("Business");
            var isAgent = User.IsInRole("Agent");

            //if (!isBusinessOwner && !isAgent)
            //{
            //    return Forbid("Access denied. Business or Agent role required.");
            //}

            //// Verify access to the specified business profile
            //if (!await HasBusinessProfileAccess(businessProfileId, userId))
            //{
            //    return StatusCode(403, new { message = "You don't have access to this business profile" });
            //}

            // Get the business profile to find its owner (userId)
            var businessProfile = _businessProfileService.GetBusinessProfileById(businessProfileId);
            if (businessProfile == null)
            {
                return NotFound("Business profile not found");
            }

            // Get facilities for the business profile owner
            var facilities = _facilityService.GetUserFacilities(businessProfile.UserId);
            return Ok(facilities);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving business profile facilities", error = ex.Message });
        }
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<BusinessProfileWithFacilitiesDto>> GetBusinessProfileDashboard([FromQuery] Guid? businessProfileId = null)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            // Check user roles
            var isBusinessOwner = User.IsInRole("Business");
            var isAgent = User.IsInRole("Agent");

            if (!isBusinessOwner && !isAgent)
            {
                return StatusCode(403, new { message = "Access denied. Business or Agent role required." });
            }

            Guid targetBusinessProfileId;

            if (businessProfileId.HasValue)
            {
                // Verify access to the specified business profile
                if (!await HasBusinessProfileAccess(businessProfileId.Value, userId))
                {
                    return StatusCode(403, new { message = "You don't have access to this business profile" });
                }
                targetBusinessProfileId = businessProfileId.Value;
            }
            else
            {
                // For business owners, try to get their own business profile
                if (isBusinessOwner)
                {
                    var userBusinessProfile = _businessProfileService.GetBusinessProfileByUserId(userId);
                    if (userBusinessProfile == null)
                    {
                        return BadRequest("Business profile not found. Business owners must have a business profile.");
                    }
                    targetBusinessProfileId = userBusinessProfile.Id;
                }
                else if (isAgent)
                {
                    // For agents, find the business profile they're assigned to
                    var agentBusinessProfiles = await _agentManagementService.GetAgentBusinessProfilesAsync(userId);
                    if (agentBusinessProfiles.Count == 0)
                    {
                        return BadRequest("No business profiles assigned to this agent");
                    }
                    if (agentBusinessProfiles.Count > 1)
                    {
                        return BadRequest("Agent is assigned to multiple business profiles. Please specify businessProfileId parameter");
                    }
                    targetBusinessProfileId = agentBusinessProfiles.First();
                }
                else
                {
                    return BadRequest("Invalid user role");
                }
            }

            // Get the business profile by ID to find the owner's userId
            var businessProfile = _businessProfileService.GetBusinessProfileById(targetBusinessProfileId);
            if (businessProfile == null)
            {
                return NotFound("Business profile not found");
            }

            var dashboard = _businessProfileService.GetBusinessProfileWithFacilities(targetBusinessProfileId);
            if (dashboard == null)
            {
                return NotFound("Dashboard data not found");
            }

            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving dashboard data", error = ex.Message });
        }
    }

    [HttpGet("{businessProfileId}/availability/{date}")]
    public async Task<ActionResult<BusinessDateAvailabilityDto>> GetDateAvailability(Guid businessProfileId, DateTime date)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            var availability = _businessProfileService.GetDateAvailability(businessProfileId, date);
            if (availability == null)
            {
                return NotFound("Business profile not found");
            }

            return Ok(availability);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving date availability", error = ex.Message });
        }
    }

    [HttpPost("{businessProfileId}/tpay/register")]
    public async Task<ActionResult<BusinessProfileDto>> RegisterWithTPay(Guid businessProfileId, [FromBody] TPayBusinessRegistrationRequest request)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            var result = await _businessProfileService.RegisterWithTPayAsync(businessProfileId, request);
            
            return Ok(new {
                success = true,
                message = "Business successfully registered with TPay",
                businessProfile = result,
                activationLink = result.TPayActivationLink,
                verificationStatus = result.TPayVerificationStatus
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = "ALREADY_REGISTERED", message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { 
                error = "REGISTRATION_FAILED", 
                message = "Failed to register business with TPay", 
                details = ex.Message 
            });
        }
    }

    [HttpPost("{businessProfileId}/tpay/update-merchant")]
    public async Task<ActionResult<BusinessProfileDto>> UpdateTPayMerchantData(Guid businessProfileId, [FromBody] TPayBusinessRegistrationResponse response)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            var result = await _businessProfileService.UpdateTPayMerchantDataAsync(businessProfileId, response);
            
            return Ok(new {
                success = true,
                message = "TPay merchant data updated successfully",
                businessProfile = result
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { 
                error = "UPDATE_FAILED", 
                message = "Failed to update TPay merchant data", 
                details = ex.Message 
            });
        }
    }

    private async Task<bool> HasBusinessProfileAccess(Guid businessProfileId, Guid userId)
    {
        // Check if user is the business owner
        var isOwner = await _agentManagementService.IsUserBusinessOwnerAsync(userId, businessProfileId);
        if (isOwner) return true;

        // Check if user is an agent for this business profile
        var isAgent = await _agentManagementService.IsUserAgentForBusinessAsync(userId, businessProfileId);
        return isAgent;
    }

    private async Task<Guid?> GetUserBusinessProfileId(Guid userId)
    {
        // First try to get user's own business profile
        var userBusinessProfile = _businessProfileService.GetBusinessProfileByUserId(userId);
        if (userBusinessProfile != null)
        {
            return userBusinessProfile.Id;
        }

        // If user doesn't own a business profile, they might be an agent
        // We need to find which business profile they're an agent for
        // For now, we'll return null and handle this in the specific endpoints
        return null;
    }

    private async Task<CreateBusinessProfileDto> ConvertMultipartToCreateDto(CreateBusinessProfileMultipartDto multipartDto)
    {
        var dto = new CreateBusinessProfileDto
        {
            Nip = multipartDto.Nip,
            CompanyName = multipartDto.CompanyName,
            DisplayName = multipartDto.DisplayName,
            Address = multipartDto.Address,
            City = multipartDto.City,
            PostalCode = multipartDto.PostalCode,
            Latitude = multipartDto.Latitude,
            Longitude = multipartDto.Longitude,
            AvatarUrl = multipartDto.AvatarUrl,
            Email = multipartDto.Email,
            PhoneNumber = multipartDto.PhoneNumber,
            PhoneCountry = multipartDto.PhoneCountry,
            Regon = multipartDto.Regon,
            Krs = multipartDto.Krs,
            LegalForm = multipartDto.LegalForm,
            CategoryId = multipartDto.CategoryId,
            Mcc = multipartDto.Mcc,
            Website = multipartDto.Website,
            WebsiteDescription = multipartDto.WebsiteDescription,
            ContactPersonName = multipartDto.ContactPersonName,
            ContactPersonSurname = multipartDto.ContactPersonSurname,
            AutoRegisterWithTPay = multipartDto.AutoRegisterWithTPay,
            ParentBusinessProfileId = multipartDto.ParentBusinessProfileId
        };

        // Handle facility plan file upload
        if (multipartDto.FacilityPlan != null)
        {
            var loggerT = HttpContext.RequestServices.GetRequiredService<ILogger<BusinessProfileController>>();
            loggerT.LogInformation("Processing facility plan upload in ConvertMultipartToCreateDto...");
            var facilityPlanResult = await _businessProfileService.ProcessFacilityPlanUploadAsync(multipartDto.FacilityPlan);
            loggerT.LogInformation("Facility plan processing completed. URL: {Url}, FileName: {FileName}", facilityPlanResult.Url, facilityPlanResult.FileName);
            
            dto.FacilityPlanUrl = facilityPlanResult.Url;
            dto.FacilityPlanFileName = facilityPlanResult.FileName;
            dto.FacilityPlanFileType = facilityPlanResult.FileType;
        }

        // Parse JSON schedule strings
        var logger = HttpContext.RequestServices.GetRequiredService<ILogger<BusinessProfileController>>();
        logger.LogInformation("Parsing JSON schedule strings...");
        
        try
        {
            if (!string.IsNullOrEmpty(multipartDto.WeekdaysSchedule))
            {
                logger.LogInformation("Parsing WeekdaysSchedule: {WeekdaysSchedule}", multipartDto.WeekdaysSchedule);
                dto.WeekdaysSchedule = System.Text.Json.JsonSerializer.Deserialize<List<ScheduleSlotDto>>(multipartDto.WeekdaysSchedule) ?? new();
            }
            
            if (!string.IsNullOrEmpty(multipartDto.SaturdaySchedule))
            {
                logger.LogInformation("Parsing SaturdaySchedule: {SaturdaySchedule}", multipartDto.SaturdaySchedule);
                dto.SaturdaySchedule = System.Text.Json.JsonSerializer.Deserialize<List<ScheduleSlotDto>>(multipartDto.SaturdaySchedule) ?? new();
            }
                
            if (!string.IsNullOrEmpty(multipartDto.SundaySchedule))
            {
                logger.LogInformation("Parsing SundaySchedule: {SundaySchedule}", multipartDto.SundaySchedule);
                dto.SundaySchedule = System.Text.Json.JsonSerializer.Deserialize<List<ScheduleSlotDto>>(multipartDto.SundaySchedule) ?? new();
            }
                
            if (!string.IsNullOrEmpty(multipartDto.DateSpecificAvailability))
            {
                logger.LogInformation("Parsing DateSpecificAvailability: {DateSpecificAvailability}", multipartDto.DateSpecificAvailability);
                dto.DateSpecificAvailability = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<BusinessDateAvailabilitySlotDto>>>(multipartDto.DateSpecificAvailability) ?? new();
            }
            
            logger.LogInformation("JSON schedule parsing completed successfully");
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogError(ex, "JSON parsing error in ConvertMultipartToCreateDto: {ErrorMessage}", ex.Message);
            throw new InvalidOperationException($"Invalid JSON in schedule data: {ex.Message}", ex);
        }

        return dto;
    }

    private async Task<UpdateBusinessProfileDto> ConvertMultipartToUpdateDto(UpdateBusinessProfileMultipartDto multipartDto)
    {
        var dto = new UpdateBusinessProfileDto
        {
            Nip = multipartDto.Nip,
            CompanyName = multipartDto.CompanyName,
            DisplayName = multipartDto.DisplayName,
            Address = multipartDto.Address,
            City = multipartDto.City,
            PostalCode = multipartDto.PostalCode,
            Latitude = multipartDto.Latitude,
            Longitude = multipartDto.Longitude,
            AvatarUrl = multipartDto.AvatarUrl,
            Email = multipartDto.Email,
            PhoneNumber = multipartDto.PhoneNumber,
            PhoneCountry = multipartDto.PhoneCountry,
            Regon = multipartDto.Regon,
            Krs = multipartDto.Krs,
            LegalForm = multipartDto.LegalForm,
            CategoryId = multipartDto.CategoryId,
            Mcc = multipartDto.Mcc,
            Website = multipartDto.Website,
            WebsiteDescription = multipartDto.WebsiteDescription,
            ContactPersonName = multipartDto.ContactPersonName,
            ContactPersonSurname = multipartDto.ContactPersonSurname
        };

        // Handle facility plan file upload
        if (multipartDto.FacilityPlan != null)
        {
            var facilityPlanResult = await _businessProfileService.ProcessFacilityPlanUploadAsync(multipartDto.FacilityPlan);
            dto.FacilityPlanUrl = facilityPlanResult.Url;
            dto.FacilityPlanFileName = facilityPlanResult.FileName;
            dto.FacilityPlanFileType = facilityPlanResult.FileType;
        }

        // Parse JSON schedule strings
        var logger = HttpContext.RequestServices.GetRequiredService<ILogger<BusinessProfileController>>();
        logger.LogInformation("Parsing JSON schedule strings...");
        
        try
        {
            if (!string.IsNullOrEmpty(multipartDto.WeekdaysSchedule))
            {
                logger.LogInformation("Parsing WeekdaysSchedule: {WeekdaysSchedule}", multipartDto.WeekdaysSchedule);
                dto.WeekdaysSchedule = System.Text.Json.JsonSerializer.Deserialize<List<ScheduleSlotDto>>(multipartDto.WeekdaysSchedule) ?? new();
            }
            
            if (!string.IsNullOrEmpty(multipartDto.SaturdaySchedule))
            {
                logger.LogInformation("Parsing SaturdaySchedule: {SaturdaySchedule}", multipartDto.SaturdaySchedule);
                dto.SaturdaySchedule = System.Text.Json.JsonSerializer.Deserialize<List<ScheduleSlotDto>>(multipartDto.SaturdaySchedule) ?? new();
            }
                
            if (!string.IsNullOrEmpty(multipartDto.SundaySchedule))
            {
                logger.LogInformation("Parsing SundaySchedule: {SundaySchedule}", multipartDto.SundaySchedule);
                dto.SundaySchedule = System.Text.Json.JsonSerializer.Deserialize<List<ScheduleSlotDto>>(multipartDto.SundaySchedule) ?? new();
            }
                
            if (!string.IsNullOrEmpty(multipartDto.DateSpecificAvailability))
            {
                logger.LogInformation("Parsing DateSpecificAvailability: {DateSpecificAvailability}", multipartDto.DateSpecificAvailability);
                dto.DateSpecificAvailability = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<BusinessDateAvailabilitySlotDto>>>(multipartDto.DateSpecificAvailability) ?? new();
            }
            
            logger.LogInformation("JSON schedule parsing completed successfully");
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogError(ex, "JSON parsing error in ConvertMultipartToCreateDto: {ErrorMessage}", ex.Message);
            throw new InvalidOperationException($"Invalid JSON in schedule data: {ex.Message}", ex);
        }

        return dto;
    }

    // ==================== KSeF Integration Endpoints ====================

    /// <summary>
    /// Get KSeF configuration status for a business profile
    /// </summary>
    [HttpGet("{businessProfileId}/ksef/configuration")]
    [Authorize]
    public async Task<IActionResult> GetKSeFConfiguration(Guid businessProfileId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            var configuration = await _businessProfileService.GetKSeFConfigurationAsync(businessProfileId);
            return Ok(configuration);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Configure KSeF credentials for a business profile
    /// </summary>
    [HttpPost("{businessProfileId}/ksef/configure")]
    [Authorize]
    public async Task<IActionResult> ConfigureKSeF(Guid businessProfileId, [FromBody] ConfigureKSeFDto configDto)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            await _businessProfileService.ConfigureKSeFAsync(businessProfileId, configDto);
            return Ok(new { message = "KSeF credentials configured successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Enable or disable KSeF integration for a business profile
    /// </summary>
    [HttpPut("{businessProfileId}/ksef/status")]
    [Authorize]
    public async Task<IActionResult> UpdateKSeFStatus(Guid businessProfileId, [FromBody] UpdateKSeFStatusDto statusDto)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            await _businessProfileService.UpdateKSeFStatusAsync(businessProfileId, statusDto.Enabled);
            return Ok(new {
                message = statusDto.Enabled ? "KSeF integration enabled" : "KSeF integration disabled",
                enabled = statusDto.Enabled
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Test KSeF connection for a business profile
    /// </summary>
    [HttpPost("{businessProfileId}/ksef/test-connection")]
    [Authorize]
    public async Task<IActionResult> TestKSeFConnection(Guid businessProfileId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            var testResult = await _businessProfileService.TestKSeFConnectionAsync(businessProfileId);
            return Ok(testResult);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ============ FAVOURITE ENDPOINTS ============

    /// <summary>
    /// Toggle favourite status for a business profile.
    /// If already favourited, removes it. If not, adds it.
    /// </summary>
    [HttpPost("{businessProfileId}/favourite")]
    public async Task<ActionResult> ToggleFavourite(Guid businessProfileId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var isFavourited = await _userFavouriteService.ToggleFavouriteAsync(userId, businessProfileId);

            return Ok(new
            {
                isFavourited,
                message = isFavourited ? "Added to favourites" : "Removed from favourites"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while toggling favourite", error = ex.Message });
        }
    }

    /// <summary>
    /// Get all business profiles favourited by the current user.
    /// </summary>
    [HttpGet("favourites")]
    public async Task<ActionResult<List<BusinessProfileDto>>> GetFavourites()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var favourites = await _userFavouriteService.GetUserFavouritesAsync(userId);
            return Ok(favourites);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving favourites", error = ex.Message });
        }
    }

    /// <summary>
    /// Check if a business profile is favourited by the current user.
    /// </summary>
    [HttpGet("{businessProfileId}/is-favourite")]
    public async Task<ActionResult> IsFavourite(Guid businessProfileId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var isFavourited = await _userFavouriteService.IsFavouriteAsync(userId, businessProfileId);
            return Ok(new { isFavourited });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while checking favourite status", error = ex.Message });
        }
    }

    // ============ TRAINER ASSOCIATION ENDPOINTS (BUSINESS-SIDE) ============

    /// <summary>
    /// Get all trainer associations for a business profile.
    /// </summary>
    [HttpGet("{businessProfileId}/trainer-associations")]
    public async Task<ActionResult<List<TrainerBusinessAssociationResponseDto>>> GetTrainerAssociations(
        Guid businessProfileId,
        [FromQuery] string? status = null)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            var associations = await _associationService.GetAssociationsForBusinessAsync(businessProfileId, status);
            return Ok(associations);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving trainer associations", error = ex.Message });
        }
    }

    /// <summary>
    /// Get pending trainer association requests for a business profile.
    /// </summary>
    [HttpGet("{businessProfileId}/trainer-associations/pending")]
    public async Task<ActionResult<List<PendingAssociationRequestDto>>> GetPendingTrainerRequests(Guid businessProfileId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            var pendingRequests = await _associationService.GetPendingRequestsForBusinessAsync(businessProfileId);
            return Ok(pendingRequests);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving pending requests", error = ex.Message });
        }
    }

    /// <summary>
    /// Get available trainers for a business profile at a specific date and timeslots.
    /// Only returns trainers with slots assigned to this specific business.
    /// </summary>
    [HttpPost("{businessProfileId}/trainer-associations/available")]
    [AllowAnonymous]
    public async Task<ActionResult<List<BusinessAvailableTrainerDto>>> GetAvailableTrainers(
        Guid businessProfileId,
        [FromBody] GetAvailableTrainersRequestDto request)
    {
        try
        {
            if (request.TimeSlots == null || !request.TimeSlots.Any())
            {
                return BadRequest(new { message = "At least one timeslot is required" });
            }

            var result = await _associationService.GetAvailableTrainersForBusinessAsync(
                businessProfileId, request.Date, request.TimeSlots);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving available trainers", error = ex.Message });
        }
    }

    /// <summary>
    /// Update trainer pricing for an association.
    /// </summary>
    [HttpPut("{businessProfileId}/trainer-associations/{associationId}/pricing")]
    public async Task<ActionResult<TrainerBusinessAssociationResponseDto>> UpdateTrainerPricing(
        Guid businessProfileId,
        Guid associationId,
        [FromBody] UpdateTrainerPricingDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            var result = await _associationService.UpdateTrainerPricingAsync(associationId, businessProfileId, dto);
            return Ok(result);
        }
        catch (Domain.Exceptions.NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Domain.Exceptions.ForbiddenException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Domain.Exceptions.BusinessRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating trainer pricing", error = ex.Message });
        }
    }

    /// <summary>
    /// Update association permissions.
    /// </summary>
    [HttpPut("{businessProfileId}/trainer-associations/{associationId}/permissions")]
    public async Task<ActionResult<TrainerBusinessAssociationResponseDto>> UpdateAssociationPermissions(
        Guid businessProfileId,
        Guid associationId,
        [FromBody] UpdateAssociationPermissionsDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            var result = await _associationService.UpdateAssociationPermissionsAsync(associationId, businessProfileId, dto);
            return Ok(result);
        }
        catch (Domain.Exceptions.NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Domain.Exceptions.ForbiddenException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Domain.Exceptions.BusinessRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating association permissions", error = ex.Message });
        }
    }

    /// <summary>
    /// Remove a trainer association.
    /// </summary>
    [HttpDelete("{businessProfileId}/trainer-associations/{associationId}")]
    public async Task<ActionResult> RemoveTrainerAssociation(Guid businessProfileId, Guid associationId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            if (!await HasBusinessProfileAccess(businessProfileId, userId))
            {
                return StatusCode(403, new { message = "You don't have access to this business profile" });
            }

            var result = await _associationService.BusinessRemoveAssociationAsync(associationId, businessProfileId);
            if (!result)
            {
                return NotFound(new { message = "Association not found" });
            }

            return Ok(new { message = "Association removed successfully" });
        }
        catch (Domain.Exceptions.ForbiddenException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while removing association", error = ex.Message });
        }
    }

    [HttpGet("{businessProfileId}/reports/monthly")]
    [RequireRole("Business", "Agent")]
    public async Task<ActionResult<MonthlySalesReportDto>> GetMonthlyReport(
        Guid businessProfileId,
        [FromQuery] int year,
        [FromQuery] int month)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized("User not authenticated");

            if (year < 2000 || year > DateTime.UtcNow.Year + 1)
                return BadRequest(new { error = "INVALID_YEAR", message = "Invalid year" });

            if (month < 1 || month > 12)
                return BadRequest(new { error = "INVALID_MONTH", message = "Month must be between 1 and 12" });

            var report = await _salesReportService.GetMonthlySalesReportAsync(businessProfileId, userId, year, month);
            return Ok(report);
        }
        catch (Domain.Exceptions.NotFoundException ex)
        {
            return NotFound(new { error = "NOT_FOUND", message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while generating the report", error = ex.Message });
        }
    }

    [HttpGet("{businessProfileId}/reports/monthly/detailed")]
    [RequireRole("Business", "Agent")]
    public async Task<ActionResult<MonthlySalesReportDetailedDto>> GetMonthlyReportDetailed(
        Guid businessProfileId,
        [FromQuery] int year,
        [FromQuery] int month)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized("User not authenticated");

            if (year < 2000 || year > DateTime.UtcNow.Year + 1)
                return BadRequest(new { error = "INVALID_YEAR", message = "Invalid year" });

            if (month < 1 || month > 12)
                return BadRequest(new { error = "INVALID_MONTH", message = "Month must be between 1 and 12" });

            var report = await _salesReportService.GetMonthlySalesReportDetailedAsync(businessProfileId, userId, year, month);
            return Ok(report);
        }
        catch (Domain.Exceptions.NotFoundException ex)
        {
            return NotFound(new { error = "NOT_FOUND", message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while generating the detailed report", error = ex.Message });
        }
    }
}
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using PlaySpace.Services.Interfaces;
using PlaySpace.Services.Services;
using PlaySpace.Services.BackgroundServices;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Repositories.Repositories;
using PlaySpace.Repositories.Data;
using PlaySpace.Domain.Configuration;
using PlaySpace.Domain.Models;
using PlaySpace.Api.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using PlaySpace.Domain.DTOs;

var builder = WebApplication.CreateBuilder(args);

// Add environment-specific Local configuration file (for local secrets)
var environment = builder.Environment.EnvironmentName;
builder.Configuration.AddJsonFile($"appsettings.{environment}.Local.json", optional: true, reloadOnChange: true);

var _configuration = builder.Configuration;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId()
    .WriteTo.Console()
    .Enrich.WithProperty("Environment", "Dev.Playspace.Api")
    //.WriteTo.Seq("http://seq:5341", apiKey: "4FfNY1An1QsIlsSOjzAR") //DEV
    .WriteTo.Seq("http://seq", apiKey: "EecDeq7jpbO2eWgFGQJ9") // UAT
    .CreateLogger();

// Use Serilog
builder.Host.UseSerilog();

Log.Information("🚀 Starting Spotto API...");

// Debug: Check environment variables
Log.Debug("Checking TPay environment variables...");
Log.Debug("TPayConfiguration__ApiKey: {ApiKey}", Environment.GetEnvironmentVariable("TPayConfiguration__ApiKey") != null ? "SET" : "NOT SET");
Log.Debug("TPayConfiguration__ApiPassword: {ApiPassword}", Environment.GetEnvironmentVariable("TPayConfiguration__ApiPassword") != null ? "SET" : "NOT SET");
Log.Debug("TPayConfiguration__MerchantId: {MerchantId}", Environment.GetEnvironmentVariable("TPayConfiguration__MerchantId") ?? "NOT SET");
Log.Debug("TPayConfiguration__BaseUrl: {BaseUrl}", Environment.GetEnvironmentVariable("TPayConfiguration__BaseUrl") ?? "NOT SET");

// Debug: Check configuration values
Log.Debug("Configuration Values:");
Log.Debug("Config TPayConfiguration:ApiKey: {ApiKey}", _configuration["TPayConfiguration:ApiKey"] != null ? "SET" : "NOT SET");
Log.Debug("Config TPayConfiguration:ApiPassword: {ApiPassword}", _configuration["TPayConfiguration:ApiPassword"] != null ? "SET" : "NOT SET");
Log.Debug("Config TPayConfiguration:MerchantId: {MerchantId}", _configuration["TPayConfiguration:MerchantId"] ?? "NOT SET");
Log.Debug("Config TPayConfiguration:BaseUrl: {BaseUrl}", _configuration["TPayConfiguration:BaseUrl"] ?? "NOT SET");
// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHttpClient(); // For KSeF API integration
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMobileApp", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure Options
builder.Services.AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.Key));

// Configure TPay
builder.Services.Configure<TPayConfiguration>(
    builder.Configuration.GetSection("TPayConfiguration"));

// Configure Email
builder.Services.Configure<EmailConfiguration>(
    builder.Configuration.GetSection("EmailConfiguration"));

// Configure FTP
builder.Services.Configure<FtpConfiguration>(
    builder.Configuration.GetSection("FtpConfiguration"));

// Configure KSeF
builder.Services.Configure<KSeFOptions>(
    builder.Configuration.GetSection("KSeFConfiguration"));

// Configure Frontend
builder.Services.Configure<FrontendConfiguration>(
    builder.Configuration.GetSection("FrontendConfiguration"));

// Add Entity Framework with IOptions
builder.Services.AddDbContext<PlaySpaceDbContext>((serviceProvider, options) =>
{
    var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>();
    options.UseNpgsql(databaseOptions.Value.DefaultConnection);
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "PlaySpace_issuer",
            ValidAudience = "PlaySpace_audience",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]))
        };
    });

// Add authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireVerifiedEmail", policy =>
        policy.RequireClaim("email_verified", "true"));
});
// Increase file size limit
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100 MB
});


// Register HTTP Client Factory
builder.Services.AddHttpClient();

// Add Memory Cache for payment status
builder.Services.AddMemoryCache();

// Register Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ITPayService, TPayService>();
builder.Services.AddHttpClient<ITPayJwsVerificationService, TPayJwsVerificationService>();
builder.Services.AddScoped<IPaymentCacheService, PaymentCacheService>();
builder.Services.AddScoped<IFacilityService, FacilityService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ITimeSlotService, TimeSlotService>();
builder.Services.AddScoped<IBusinessProfileService, BusinessProfileService>();
builder.Services.AddScoped<ITrainerProfileService, TrainerProfileService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IPendingTimeSlotReservationService, PendingTimeSlotReservationService>();
builder.Services.AddScoped<ITrainingService, TrainingService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<CategorySeedService>();
builder.Services.AddScoped<ISocialWallPostService, SocialWallPostService>();
builder.Services.AddScoped<ITPayDictionaryService, TPayDictionaryService>();
builder.Services.AddScoped<IPrivacySettingsService, PrivacySettingsService>();
builder.Services.AddScoped<IPushNotificationService, PlaySpace.Services.Implementation.ExpoPushNotificationService>();

// Background Services
builder.Services.AddSingleton<PlaySpace.Services.BackgroundServices.AutoReservationService>();
builder.Services.AddHostedService(provider => 
    provider.GetRequiredService<PlaySpace.Services.BackgroundServices.AutoReservationService>());
builder.Services.AddScoped<IGlobalSettingsService, GlobalSettingsService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAgentManagementService, AgentManagementService>();
builder.Services.AddScoped<IFtpStorageService, FtpStorageService>();
builder.Services.AddScoped<IFileMigrationService, FileMigrationService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IKSeFInvoiceService, KSeFInvoiceService>();
builder.Services.AddScoped<IKSeFApiService, KSeFApiService>();
builder.Services.AddScoped<IFAXmlGeneratorService, FAXmlGeneratorService>();
builder.Services.AddScoped<IInvoicePdfGeneratorService, InvoicePdfGeneratorService>();
builder.Services.AddScoped<IProductPurchaseService, ProductPurchaseService>();
builder.Services.AddScoped<IUserFavouriteService, UserFavouriteService>();
builder.Services.AddScoped<ISalesReportService, SalesReportService>();

// External Authentication Services
builder.Services.AddScoped<IExternalAuthService, ExternalAuthService>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<IAppleAuthService, AppleAuthService>();
builder.Services.AddScoped<IExternalProviderService, ExternalProviderService>();
builder.Services.AddSingleton<ICityLookupService>(provider => 
{
    var environment = provider.GetRequiredService<IWebHostEnvironment>();
    var dataFilePath = Path.Combine(environment.ContentRootPath, "Data", "data.json");
    return new CityLookupService(dataFilePath);
});

// Register Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IFacilityRepository, FacilityRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<ITimeSlotRepository, TimeSlotRepository>();
builder.Services.AddScoped<IBusinessProfileRepository, BusinessProfileRepository>();
builder.Services.AddScoped<IBusinessDateAvailabilityRepository, BusinessDateAvailabilityRepository>();
builder.Services.AddScoped<ITrainerProfileRepository, TrainerProfileRepository>();
builder.Services.AddScoped<ISearchRepository, SearchRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IPendingTimeSlotReservationRepository, PendingTimeSlotReservationRepository>();
builder.Services.AddScoped<IPendingTrainingParticipantRepository, PendingTrainingParticipantRepository>();
builder.Services.AddScoped<ITrainingRepository, TrainingRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ISocialWallPostRepository, SocialWallPostRepository>();
builder.Services.AddScoped<ITPayDictionaryRepository, TPayDictionaryRepository>();
builder.Services.AddScoped<IPrivacySettingsRepository, PrivacySettingsRepository>();
builder.Services.AddScoped<IGlobalSettingsRepository, GlobalSettingsRepository>();
builder.Services.AddScoped<IExternalAuthRepository, ExternalAuthRepository>();
builder.Services.AddScoped<IAgentInvitationRepository, AgentInvitationRepository>();
builder.Services.AddScoped<IBusinessProfileAgentRepository, BusinessProfileAgentRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IKSeFInvoiceRepository, KSeFInvoiceRepository>();
builder.Services.AddScoped<IProductPurchaseRepository, ProductPurchaseRepository>();
builder.Services.AddScoped<IProductUsageLogRepository, ProductUsageLogRepository>();
builder.Services.AddScoped<IUserFavouriteRepository, UserFavouriteRepository>();
builder.Services.AddScoped<ITrainerBusinessAssociationRepository, TrainerBusinessAssociationRepository>();
builder.Services.AddScoped<IBusinessParentChildAssociationRepository, BusinessParentChildAssociationRepository>();

// Trainer-Business Association Service
builder.Services.AddScoped<ITrainerBusinessAssociationService, TrainerBusinessAssociationService>();

// Business Parent-Child Association Service
builder.Services.AddScoped<IBusinessParentChildAssociationService, BusinessParentChildAssociationService>();

// Register AuthService
builder.Services.AddScoped<AuthService>();

// Register Database Seeder
//builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<TPayDictionarySeedService>();
builder.Services.AddScoped<GlobalSettingsInitializationService>();

// Register background services
builder.Services.AddHostedService<TPayDictionarySyncService>();

var app = builder.Build();

// Initialize global settings
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PlaySpaceDbContext>();
    await db.Database.MigrateAsync();

    // First seed TPay dictionaries (fallback data)
    var tpaySeeder = scope.ServiceProvider.GetRequiredService<TPayDictionarySeedService>();
    await tpaySeeder.SeedFallbackDataAsync();
    
    // Seed default categories
    var categorySeedService = scope.ServiceProvider.GetRequiredService<CategorySeedService>();
    await categorySeedService.SeedAsync();

    // Initialize global settings with default refund configuration
    var settingsInitializer = scope.ServiceProvider.GetRequiredService<GlobalSettingsInitializationService>();
    await settingsInitializer.InitializeDefaultSettingsAsync();
    
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var statusMessage = await settingsInitializer.GetSettingsStatusAsync();
    logger.LogInformation("Global Settings Status: {StatusMessage}", statusMessage);
}

// Configure the HTTP request pipeline.

// Add global exception handling - must be first in pipeline
app.UseGlobalExceptionHandling();

// Add global request logging to debug TPay webhook routing
app.Use(async (context, next) =>
{
    Log.Debug("=== INCOMING REQUEST ===");
    Log.Debug("METHOD: {Method}", context.Request.Method);
    Log.Debug("PATH: {Path}", context.Request.Path);
    Log.Debug("QUERY: {Query}", context.Request.QueryString);
    Log.Debug("FROM: {RemoteIp}", context.Connection.RemoteIpAddress);
    Log.Debug("CONTENT-TYPE: {ContentType}", context.Request.ContentType);
    Log.Debug("CONTENT-LENGTH: {ContentLength}", context.Request.ContentLength);
    Log.Debug("HAS-BODY: {HasBody}", context.Request.ContentLength > 0);
    Log.Debug("USER-AGENT: {UserAgent}", context.Request.Headers.UserAgent);
    
    // Log request body (skip for multipart/form-data to avoid logging large files)
    if (!context.Request.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true)
    {
        context.Request.EnableBuffering();
        using (var reader = new StreamReader(context.Request.Body, leaveOpen: true))
        {
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
            Log.Debug("BODY: '{Body}'", body);
        }
    }
    else
    {
        Log.Debug("BODY: [MULTIPART FILE DATA - CONTENT NOT LOGGED]");
    }
    
    Log.Debug("========================");
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Disabled HTTPS redirection - handled by Nginx reverse proxy
// app.UseHttpsRedirection();

// Use CORS - must be before Authentication/Authorization
app.UseCors("AllowMobileApp");

// Enable static files serving from wwwroot folder
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

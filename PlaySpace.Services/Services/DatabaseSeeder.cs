using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace PlaySpace.Services.Services;

public class DatabaseSeeder
{
    private readonly PlaySpaceDbContext _context;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(PlaySpaceDbContext context, IHostEnvironment environment, ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        // Seed default roles
        await SeedRolesAsync();

        // Seed default settings
        await SeedDefaultSettingsAsync();

        // Seed comprehensive data only in development environment
        if (_environment.IsDevelopment())
        {
            await SeedBasicTestDataAsync();
        }
    }

    private async Task SeedRolesAsync()
    {
        var defaultRoles = new[]
        {
            new { Name = "Player", Description = "Default Player role - all users have this role" },
            new { Name = "Business", Description = "Business role - allows facility management" },
            new { Name = "Trainer", Description = "Trainer role - allows training services" },
            new { Name = "Agent", Description = "Agent role - allows managing facility schedules for specific business profiles" }
        };

        foreach (var roleData in defaultRoles)
        {
            // Check if role already exists
            var existingRole = _context.Roles.FirstOrDefault(r => r.Name == roleData.Name);
            
            if (existingRole == null)
            {
                var role = new Role
                {
                    Id = Guid.NewGuid(),
                    Name = roleData.Name,
                    Description = roleData.Description,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Roles.Add(role);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedDevelopmentDataAsync()
    {
        try
        {
            // Check if data already exists to avoid duplicates
            //if (_context.Users.Any())
            //{
            //    _logger.LogInformation("Development data already exists, skipping seed");
            //    return;
            //}

            _logger.LogInformation("Loading development seed data...");

            var seedFilePath = Path.Combine(Directory.GetCurrentDirectory(), "seed-data.json");
            if (!File.Exists(seedFilePath))
            {
                _logger.LogWarning("Seed data file not found at: {SeedFilePath}", seedFilePath);
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(seedFilePath);
            var seedData = JsonSerializer.Deserialize<SeedData>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (seedData == null)
            {
                _logger.LogError("Failed to deserialize seed data");
                return;
            }

            // Seed in correct order to maintain foreign key relationships
            await SeedUsersAsync(seedData.Users);
            await SeedBusinessProfilesAsync(seedData.BusinessProfiles);
            await SeedBusinessScheduleTemplatesAsync(seedData.BusinessScheduleTemplates);
            await SeedTrainerProfilesAsync(seedData.TrainerProfiles);
            await SeedTrainerScheduleTemplatesAsync(seedData.TrainerScheduleTemplates);
            await SeedFacilitiesAsync(seedData.Facilities);
            await SeedPaymentsAsync(seedData.Payments);
            await SeedReservationsAsync(seedData.Reservations);
            await SeedTrainingsAsync(seedData.Trainings);
            await SeedTrainingSessionsAsync(seedData.TrainingSessions);
            await SeedTrainingParticipantsAsync(seedData.TrainingParticipants);

            _logger.LogInformation("Development seed data loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding development data");
            throw;
        }
    }

    private async Task SeedUsersAsync(List<User>? users)
    {
        if (users == null) return;

        foreach (var user in users)
        {
            if (!_context.Users.Any(u => u.Id == user.Id))
            {
                _context.Users.Add(user);
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedBusinessProfilesAsync(List<BusinessProfile>? businessProfiles)
    {
        if (businessProfiles == null) return;

        foreach (var profile in businessProfiles)
        {
            if (!_context.BusinessProfiles.Any(bp => bp.Id == profile.Id))
            {
                _context.BusinessProfiles.Add(profile);
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedBusinessScheduleTemplatesAsync(List<BusinessScheduleTemplate>? templates)
    {
        if (templates == null) return;

        foreach (var template in templates)
        {
            if (!_context.BusinessScheduleTemplates.Any(t => t.Id == template.Id))
            {
                _context.BusinessScheduleTemplates.Add(template);
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedTrainerProfilesAsync(List<TrainerProfile>? trainerProfiles)
    {
        if (trainerProfiles == null) return;

        foreach (var profile in trainerProfiles)
        {
            if (!_context.TrainerProfiles.Any(tp => tp.Id == profile.Id))
            {
                _context.TrainerProfiles.Add(profile);
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedTrainerScheduleTemplatesAsync(List<TrainerScheduleTemplate>? templates)
    {
        if (templates == null) return;

        foreach (var template in templates)
        {
            if (!_context.TrainerScheduleTemplates.Any(t => t.Id == template.Id))
            {
                _context.TrainerScheduleTemplates.Add(template);
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedFacilitiesAsync(List<Facility>? facilities)
    {
        if (facilities == null) return;

        foreach (var facility in facilities)
        {
            if (!_context.Facilities.Any(f => f.Id == facility.Id))
            {
                _context.Facilities.Add(facility);
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedPaymentsAsync(List<Payment>? payments)
    {
        if (payments == null) return;

        foreach (var payment in payments)
        {
            if (!_context.Payments.Any(p => p.Id == payment.Id))
            {
                _context.Payments.Add(payment);
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedReservationsAsync(List<Reservation>? reservations)
    {
        if (reservations == null) return;

        foreach (var reservation in reservations)
        {
            if (!_context.Reservations.Any(r => r.Id == reservation.Id))
            {
                _context.Reservations.Add(reservation);
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedTrainingsAsync(List<Training>? trainings)
    {
        if (trainings == null) return;

        foreach (var training in trainings)
        {
            if (!_context.Trainings.Any(t => t.Id == training.Id))
            {
                _context.Trainings.Add(training);
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedTrainingSessionsAsync(List<TrainingSession>? sessions)
    {
        if (sessions == null) return;

        foreach (var session in sessions)
        {
            if (!_context.TrainingSessions.Any(s => s.Id == session.Id))
            {
                _context.TrainingSessions.Add(session);
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedTrainingParticipantsAsync(List<TrainingParticipant>? participants)
    {
        if (participants == null) return;

        foreach (var participant in participants)
        {
            if (!_context.TrainingParticipants.Any(p => p.Id == participant.Id))
            {
                _context.TrainingParticipants.Add(participant);
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task SeedBasicTestDataAsync()
    {
        try
        {
            // Check if basic test data already exists
            if (_context.Users.Any(u => u.Email == "test@example.com"))
            {
                _logger.LogInformation("Basic test data already exists, skipping seed");
                return;
            }

            _logger.LogInformation("Seeding basic test data...");

            // Create 5 simple users
            var users = new List<User>();
            for (int i = 1; i <= 5; i++)
            {
                users.Add(new User
                {
                    Id = Guid.Parse($"1000000{i}-0000-0000-0000-000000000000"),
                    Email = $"user{i}@example.com",
                    Password = "hashedpassword123", // In real app, this would be properly hashed
                    FirstName = $"User{i}",
                    LastName = "Test",
                    Phone = $"12345678{i}",
                    DateOfBirth = $"1990-01-0{i}",
                    ActivityInterests = new List<string> { "fitness", "sports" }
                });
            }

            // Add one special user that will have business and trainer profiles
            var specialUser = new User
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000000"),
                Email = "test@example.com",
                Password = "hashedpassword123",
                FirstName = "John",
                LastName = "Smith",
                Phone = "123456789",
                DateOfBirth = "1985-05-15",
                ActivityInterests = new List<string> { "martial arts", "fitness", "training" }
            };
            users.Add(specialUser);

            // Add users to context
            foreach (var user in users)
            {
                if (!_context.Users.Any(u => u.Id == user.Id))
                {
                    _context.Users.Add(user);
                }
            }
            await _context.SaveChangesAsync();

            // Create business profile for special user
            var businessProfile = new BusinessProfile
            {
                Id = Guid.Parse("30000000-0000-0000-0000-000000000000"),
                UserId = specialUser.Id,
                Nip = "1234567890",
                CompanyName = "Smith Fitness Center",
                DisplayName = "Smith Fitness",
                Address = "123 Main Street",
                City = "Warsaw",
                PostalCode = "00-001",
                Email = "business@smithfitness.com",
                PhoneNumber = "123456789",
                PhoneCountry = "PL",
                LegalForm = 2, // "działalność gospodarcza"
                CategoryId = 78, // "Usługi finansowe" 
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (!_context.BusinessProfiles.Any(bp => bp.Id == businessProfile.Id || bp.Nip == businessProfile.Nip))
            {
                _context.BusinessProfiles.Add(businessProfile);
            }
            await _context.SaveChangesAsync();

            // Create trainer profile for special user
            var trainerProfile = new TrainerProfile
            {
                Id = Guid.Parse("40000000-0000-0000-0000-000000000000"),
                UserId = specialUser.Id,
                Nip = "9876543210",
                CompanyName = "Smith Personal Training",
                DisplayName = "John Smith PT",
                Address = "123 Main Street",
                City = "Warsaw",
                PostalCode = "00-001",
                Specializations = new List<string> { "Martial Arts", "Strength Training", "Flexibility" },
                HourlyRate = 150.00m,
                Description = "Experienced martial arts instructor and personal trainer",
                Certifications = new List<string> { "Certified Personal Trainer", "Black Belt Karate" },
                Languages = new List<string> { "Polish", "English" },
                ExperienceYears = 10,
                AssociatedBusinessIds = new List<string> { businessProfile.Id.ToString() },
                CategoryId = 74, // "Kursy i szkolenia"
                LegalForm = 2, // "działalność gospodarcza"
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (!_context.TrainerProfiles.Any(tp => tp.Id == trainerProfile.Id || tp.Nip == trainerProfile.Nip))
            {
                _context.TrainerProfiles.Add(trainerProfile);
            }
            await _context.SaveChangesAsync();

            // Create facility for the business
            var facility = new Facility
            {
                Id = Guid.Parse("50000000-0000-0000-0000-000000000000"),
                UserId = specialUser.Id,
                BusinessProfileId = businessProfile.Id,
                Name = "Smith Fitness Gym",
                Type = "Gym",
                Description = "Modern fitness facility with martial arts area",
                PricePerHour = 80.00m,
                Street = "123 Main Street",
                City = "Warsaw",
                PostalCode = "00-001",
                Country = "Poland",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (!_context.Facilities.Any(f => f.Id == facility.Id))
            {
                _context.Facilities.Add(facility);
            }
            await _context.SaveChangesAsync();

            // Create payment for the reservation
            var payment = new Payment
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000000"),
                UserId = users[0].Id, // First simple user makes the payment
                Amount = 230.00m,
                Status = "completed",
                Breakdown = "Facility: 80.00, Trainer: 150.00",
                CreatedAt = DateTime.UtcNow,
                IsRefunded = false,
                IsConsumed = false,
                TPayTransactionId = "test_txn_001",
                TPayStatus = "correct",
                TPayCompletedAt = DateTime.UtcNow,
                PaymentMethod = "card"
            };

            if (!_context.Payments.Any(p => p.Id == payment.Id))
            {
                _context.Payments.Add(payment);
            }
            await _context.SaveChangesAsync();

            // Create reservation
            var reservation = new Reservation
            {
                Id = Guid.Parse("70000000-0000-0000-0000-000000000000"),
                FacilityId = facility.Id,
                UserId = users[0].Id, // First simple user makes the reservation
                Date = DateTime.Today.AddDays(7), // Next week
                TimeSlots = new List<string> { "10:00", "11:00" },
                TotalPrice = 230.00m,
                Status = "Active",
                TrainerProfileId = trainerProfile.Id,
                TrainerPrice = 150.00m,
                PaymentId = payment.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (!_context.Reservations.Any(r => r.Id == reservation.Id))
            {
                _context.Reservations.Add(reservation);
            }
            await _context.SaveChangesAsync();

            // Create training
            var training = new Training
            {
                Id = Guid.Parse("80000000-0000-0000-0000-000000000000"),
                TrainerProfileId = trainerProfile.Id,
                FacilityId = facility.Id,
                ReservationId = reservation.Id,
                Title = "Martial Arts Fundamentals",
                Description = "Learn the basics of martial arts including stance, basic strikes, and flexibility",
                Specialization = "Martial Arts",
                Duration = 120, // 2 hours
                MaxParticipants = 6,
                Price = 80.00m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (!_context.Trainings.Any(t => t.Id == training.Id))
            {
                _context.Trainings.Add(training);
            }
            await _context.SaveChangesAsync();

            // Create training session
            var trainingSession = new TrainingSession
            {
                Id = Guid.Parse("81000000-0000-0000-0000-000000000000"),
                TrainingId = training.Id,
                Date = DateTime.Today.AddDays(7), // Same day as reservation
                StartTime = "10:00",
                EndTime = "12:00",
                CurrentParticipants = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (!_context.TrainingSessions.Any(ts => ts.Id == trainingSession.Id))
            {
                _context.TrainingSessions.Add(trainingSession);
            }
            await _context.SaveChangesAsync();

            // Create payments for participants
            var participantPayments = new List<Payment>();
            for (int i = 1; i <= 2; i++)
            {
                var participantPayment = new Payment
                {
                    Id = Guid.Parse($"6100000{i}-0000-0000-0000-000000000000"),
                    UserId = users[i].Id, // User2 and User3 participate
                    Amount = 80.00m,
                    Status = "completed",
                    Breakdown = "Training participation: 80.00",
                    CreatedAt = DateTime.UtcNow,
                    IsRefunded = false,
                    IsConsumed = false,
                    TPayTransactionId = $"test_txn_00{i + 1}",
                    TPayStatus = "correct",
                    TPayCompletedAt = DateTime.UtcNow,
                    PaymentMethod = "card"
                };

                if (!_context.Payments.Any(p => p.Id == participantPayment.Id))
                {
                    _context.Payments.Add(participantPayment);
                    participantPayments.Add(participantPayment);
                }
            }
            await _context.SaveChangesAsync();

            // Create training participants
            for (int i = 0; i < 2; i++)
            {
                var participant = new TrainingParticipant
                {
                    Id = Guid.Parse($"9000000{i + 1}-0000-0000-0000-000000000000"),
                    TrainingId = training.Id,
                    UserId = users[i + 1].Id, // User2 and User3
                    PaymentId = participantPayments[i].Id,
                    JoinedAt = DateTime.UtcNow.AddDays(-1),
                    Status = "Active",
                    Notes = $"Participant {i + 1} - interested in martial arts basics",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                if (!_context.TrainingParticipants.Any(tp => tp.Id == participant.Id))
                {
                    _context.TrainingParticipants.Add(participant);
                }
            }
            await _context.SaveChangesAsync();

            _logger.LogInformation("Basic test data seeded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding basic test data");
            throw;
        }
    }

    private async Task SeedDefaultSettingsAsync()
    {
        try
        {
            var defaultSettings = new[]
            {
                new { Key = SettingsKeys.RefundFeePercentage, Value = "20", Description = "Percentage fee deducted from refund amount (e.g., 20 = 20% fee, user gets 80% refund)" },
                new { Key = SettingsKeys.MaxRefundDaysAdvance, Value = "30", Description = "Maximum number of days in advance that refunds are allowed" },
                new { Key = SettingsKeys.EnableRefunds, Value = "true", Description = "Whether refunds are enabled system-wide" }
            };

            foreach (var settingData in defaultSettings)
            {
                // Check if setting already exists
                var existingSetting = _context.GlobalSettings.FirstOrDefault(s => s.Key == settingData.Key);
                if (existingSetting == null)
                {
                    var setting = new GlobalSettings
                    {
                        Id = Guid.NewGuid(),
                        Key = settingData.Key,
                        Value = settingData.Value,
                        Description = settingData.Description,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.GlobalSettings.Add(setting);
                    _logger.LogInformation("Added default setting: {Key} = {Value}", settingData.Key, settingData.Value);
                }
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding default settings");
            throw;
        }
    }
}

// Data transfer classes for JSON deserialization
public class SeedData
{
    public List<User>? Users { get; set; }
    public List<BusinessProfile>? BusinessProfiles { get; set; }
    public List<BusinessScheduleTemplate>? BusinessScheduleTemplates { get; set; }
    public List<TrainerProfile>? TrainerProfiles { get; set; }
    public List<TrainerScheduleTemplate>? TrainerScheduleTemplates { get; set; }
    public List<Facility>? Facilities { get; set; }
    public List<Payment>? Payments { get; set; }
    public List<Reservation>? Reservations { get; set; }
    public List<Training>? Trainings { get; set; }
    public List<TrainingSession>? TrainingSessions { get; set; }
    public List<TrainingParticipant>? TrainingParticipants { get; set; }
}
using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Data
{
    public class PlaySpaceDbContext : DbContext
    {
        public PlaySpaceDbContext(DbContextOptions<PlaySpaceDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Facility> Facilities { get; set; }
        public DbSet<TimeSlot> TimeSlots { get; set; }
        public DbSet<BusinessProfile> BusinessProfiles { get; set; }
        public DbSet<BusinessScheduleTemplate> BusinessScheduleTemplates { get; set; }
        public DbSet<TrainerProfile> TrainerProfiles { get; set; }
        public DbSet<TrainerScheduleTemplate> TrainerScheduleTemplates { get; set; }
        public DbSet<TrainerDateAvailability> TrainerDateAvailabilities { get; set; }
        public DbSet<BusinessDateAvailability> BusinessDateAvailabilities { get; set; }
        public DbSet<FacilityScheduleTemplate> FacilityScheduleTemplates { get; set; }
        public DbSet<FacilityDateAvailability> FacilityDateAvailabilities { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<ReservationSlot> ReservationSlots { get; set; }
        public DbSet<Training> Trainings { get; set; }
        public DbSet<TrainingSession> TrainingSessions { get; set; }
        public DbSet<TrainingParticipant> TrainingParticipants { get; set; }
        public DbSet<SocialWallPost> SocialWallPosts { get; set; }
        public DbSet<SocialWallPostLike> SocialWallPostLikes { get; set; }
        public DbSet<SocialWallPostComment> SocialWallPostComments { get; set; }
        public DbSet<PendingTimeSlotReservation> PendingTimeSlotReservations { get; set; }
        public DbSet<PendingTrainingParticipant> PendingTrainingParticipants { get; set; }
        public DbSet<TPayLegalForm> TPayLegalForms { get; set; }
        public DbSet<TPayCategory> TPayCategories { get; set; }
        public DbSet<TPayDictionarySync> TPayDictionarySyncs { get; set; }
        public DbSet<PrivacySettings> PrivacySettings { get; set; }
        public DbSet<ExternalAuth> ExternalAuths { get; set; }
        public DbSet<GlobalSettings> GlobalSettings { get; set; }
        public DbSet<AgentInvitation> AgentInvitations { get; set; }
        public DbSet<BusinessProfileAgent> BusinessProfileAgents { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<KSeFInvoice> KSeFInvoices { get; set; }
        public DbSet<ProductPurchase> ProductPurchases { get; set; }
        public DbSet<ProductUsageLog> ProductUsageLogs { get; set; }
        public DbSet<UserFavouriteBusinessProfile> UserFavouriteBusinessProfiles { get; set; }
        public DbSet<TrainerBusinessAssociation> TrainerBusinessAssociations { get; set; }
        public DbSet<BusinessParentChildAssociation> BusinessParentChildAssociations { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<CategoryTranslation> CategoryTranslations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User entity configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.Password)
                    .HasMaxLength(255);
                entity.Property(e => e.FirstName)
                    .IsRequired()
                    .HasMaxLength(100);
                entity.Property(e => e.LastName)
                    .IsRequired()
                    .HasMaxLength(100);
                entity.Property(e => e.Phone)
                    .HasMaxLength(20);
                entity.Property(e => e.RefreshToken)
                    .HasMaxLength(500);
                entity.Property(e => e.RefreshTokenExpiryTime);
                entity.Property(e => e.AuthProvider)
                    .IsRequired()
                    .HasDefaultValue(AuthProvider.Local)
                    .HasConversion<int>();
                entity.Property(e => e.IsEmailVerified)
                    .IsRequired()
                    .HasDefaultValue(false);
                entity.Property(e => e.DateOfBirth);
                entity.Property(e => e.ActivityInterests)
                    .HasConversion(
                        v => string.Join(',', v),
                        v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    )
                    .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                        (c1, c2) => c1.SequenceEqual(c2),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()
                    ));

                // Unique constraint on email
                entity.HasIndex(e => e.Email).IsUnique();
            });

            // Role entity configuration
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100);
                entity.Property(e => e.Description)
                    .HasMaxLength(500);
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Unique constraint on role name
                entity.HasIndex(e => e.Name).IsUnique();
            });

            // UserRole entity configuration
            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.RoleId });
                entity.Property(e => e.AssignedAt)
                    .IsRequired();

                entity.HasOne(e => e.User)
                    .WithMany(u => u.UserRoles)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Role)
                    .WithMany(r => r.UserRoles)
                    .HasForeignKey(e => e.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Facility entity configuration
            modelBuilder.Entity<Facility>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.Type)
                    .IsRequired()
                    .HasMaxLength(100);
                entity.Property(e => e.Description)
                    .HasMaxLength(1000);
                entity.Property(e => e.PricePerHour)
                    .HasColumnType("decimal(18,2)");
                entity.Property(e => e.UserId)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Address fields configuration
                entity.Property(e => e.Street)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.City)
                    .IsRequired()
                    .HasMaxLength(100);
                entity.Property(e => e.State)                   
                    .HasMaxLength(100);
                entity.Property(e => e.PostalCode)
                    .IsRequired()
                    .HasMaxLength(20);
                entity.Property(e => e.Country)                    
                    .HasMaxLength(100);
                entity.Property(e => e.AddressLine2)
                    .HasMaxLength(255);
                entity.Property(e => e.Latitude)
                    .HasColumnType("decimal(10,8)");
                entity.Property(e => e.Longitude)
                    .HasColumnType("decimal(11,8)");

                // Foreign key relationships
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasOne(e => e.BusinessProfile)
                    .WithMany()
                    .HasForeignKey(e => e.BusinessProfileId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // TimeSlot entity configuration
            modelBuilder.Entity<TimeSlot>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Time)
                    .IsRequired()
                    .HasMaxLength(10);
                entity.Property(e => e.FacilityId)
                    .IsRequired();
                entity.Property(e => e.Date);
                entity.Property(e => e.IsAllTime)
                    .IsRequired();
                entity.Property(e => e.IsAvailable)
                    .IsRequired();
                entity.Property(e => e.IsBooked)
                    .IsRequired();
                entity.Property(e => e.BookedByUserId);
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Foreign key relationships
                entity.HasOne(e => e.Facility)
                    .WithMany()
                    .HasForeignKey(e => e.FacilityId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.BookedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.BookedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Composite index for better performance
                entity.HasIndex(e => new { e.FacilityId, e.Date, e.Time });
            });

            // BusinessProfile entity configuration
            modelBuilder.Entity<BusinessProfile>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nip)
                    .IsRequired()
                    .HasMaxLength(20);
                entity.Property(e => e.CompanyName)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.DisplayName)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.Address)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.City)
                    .IsRequired()
                    .HasMaxLength(100);
                entity.Property(e => e.PostalCode)
                    .IsRequired()
                    .HasMaxLength(20);
                entity.Property(e => e.UserId)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // TPay registration fields configuration
                entity.Property(e => e.Email)
                    .HasMaxLength(255);
                entity.Property(e => e.PhoneNumber)
                    .HasMaxLength(20);
                entity.Property(e => e.PhoneCountry)
                    .HasMaxLength(10);
                entity.Property(e => e.Regon)
                    .HasMaxLength(20);
                entity.Property(e => e.Krs)
                    .HasMaxLength(20);
                entity.Property(e => e.LegalForm);
                entity.Property(e => e.CategoryId);
                entity.Property(e => e.Mcc)
                    .HasMaxLength(10);
                entity.Property(e => e.Website)
                    .HasMaxLength(500);
                entity.Property(e => e.WebsiteDescription)
                    .HasMaxLength(1000);
                entity.Property(e => e.ContactPersonName)
                    .HasMaxLength(100);
                entity.Property(e => e.ContactPersonSurname)
                    .HasMaxLength(100);

                // TPay merchant fields configuration
                entity.Property(e => e.TPayMerchantId)
                    .HasMaxLength(255);
                entity.Property(e => e.TPayAccountId)
                    .HasMaxLength(255);
                entity.Property(e => e.TPayPosId)
                    .HasMaxLength(255);
                entity.Property(e => e.TPayActivationLink);
                entity.Property(e => e.TPayVerificationStatus);
                entity.Property(e => e.TPayRegisteredAt);

                // Foreign key relationship
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Index on NIP (not unique - child profiles can share parent's NIP)
                // Uniqueness for standalone profiles is enforced in the service layer
                entity.HasIndex(e => e.Nip);

                // One business profile per user
                entity.HasIndex(e => e.UserId).IsUnique();

                // Unique constraint on TPay Merchant ID (if set)
                entity.HasIndex(e => e.TPayMerchantId)
                    .IsUnique()
                    .HasFilter("\"TPayMerchantId\" IS NOT NULL");

                // Foreign key relationships to TPay dictionaries
                entity.HasOne<TPayLegalForm>()
                    .WithMany()
                    .HasForeignKey(e => e.LegalForm)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne<TPayCategory>()
                    .WithMany()
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Parent-child business profile relationship (self-referencing)
                entity.HasOne(e => e.ParentBusinessProfile)
                    .WithMany(e => e.ChildBusinessProfiles)
                    .HasForeignKey(e => e.ParentBusinessProfileId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Index for parent lookup
                entity.HasIndex(e => e.ParentBusinessProfileId);
            });

            // BusinessScheduleTemplate entity configuration
            modelBuilder.Entity<BusinessScheduleTemplate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Time)
                    .IsRequired()
                    .HasMaxLength(10);
                entity.Property(e => e.ScheduleType)
                    .IsRequired()
                    .HasConversion<int>();
                entity.Property(e => e.IsAvailable)
                    .IsRequired();
                entity.Property(e => e.BusinessProfileId)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Foreign key relationship
                entity.HasOne(e => e.BusinessProfile)
                    .WithMany(bp => bp.ScheduleTemplates)
                    .HasForeignKey(e => e.BusinessProfileId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint: one time slot per schedule type per business
                entity.HasIndex(e => new { e.BusinessProfileId, e.ScheduleType, e.Time }).IsUnique();
            });

            // Payment entity configuration
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount)
                    .HasColumnType("decimal(18,2)");
            });

            // Reservation entity configuration
            modelBuilder.Entity<Reservation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FacilityId)
                    .IsRequired();
                entity.Property(e => e.UserId); // Nullable to support guest reservations
                entity.Property(e => e.Date)
                    .IsRequired();
                entity.Property(e => e.TimeSlots)
                    .HasConversion(
                        v => string.Join(',', v),
                        v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    );
                entity.Property(e => e.TotalPrice)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                entity.Property(e => e.RemainingPrice)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasMaxLength(50);
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();
                entity.Property(e => e.TrainerPrice)
                    .HasColumnType("decimal(18,2)");
                entity.Property(e => e.Notes)
                    .HasColumnType("text");  // Unlimited length
                entity.Property(e => e.CancellationNotes)
                    .HasColumnType("text");  // Unlimited length

                // Foreign key relationships
                entity.HasOne(e => e.Facility)
                    .WithMany()
                    .HasForeignKey(e => e.FacilityId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false); // Allow null for guest reservations

                entity.HasOne(e => e.TrainerProfile)
                    .WithMany()
                    .HasForeignKey(e => e.TrainerProfileId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Payment)
                    .WithMany()
                    .HasForeignKey(e => e.PaymentId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.SetNull);

                // Relationship to ReservationSlots
                entity.HasMany(e => e.Slots)
                    .WithOne(s => s.Reservation)
                    .HasForeignKey(s => s.ReservationId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Index for better performance
                entity.HasIndex(e => new { e.FacilityId, e.Date });
                entity.HasIndex(e => e.UserId);
            });

            // ReservationSlot entity configuration
            modelBuilder.Entity<ReservationSlot>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.ReservationId)
                    .IsRequired();

                entity.Property(e => e.TimeSlot)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.SlotPrice)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasDefaultValue("Active");

                entity.Property(e => e.CancelledAt)
                    .HasColumnType("timestamptz");

                entity.Property(e => e.CancellationReason)
                    .HasMaxLength(500);

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamptz")
                    .HasDefaultValueSql("NOW()")
                    .IsRequired();

                // Indexes for better query performance
                entity.HasIndex(e => e.ReservationId)
                    .HasDatabaseName("IX_ReservationSlots_ReservationId");

                entity.HasIndex(e => e.TimeSlot)
                    .HasDatabaseName("IX_ReservationSlots_TimeSlot");

                entity.HasIndex(e => e.Status)
                    .HasDatabaseName("IX_ReservationSlots_Status");
            });

            // TrainerProfile entity configuration
            modelBuilder.Entity<TrainerProfile>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nip)
                    .IsRequired(false)
                    .HasMaxLength(20);
                entity.Property(e => e.CompanyName)
                    .IsRequired(false)
                    .HasMaxLength(255);
                entity.Property(e => e.DisplayName)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.Address)
                    .IsRequired(false)
                    .HasMaxLength(255);
                entity.Property(e => e.City)
                    .IsRequired(false)
                    .HasMaxLength(100);
                entity.Property(e => e.PostalCode)
                    .IsRequired(false)
                    .HasMaxLength(20);
                entity.Property(e => e.UserId)
                    .IsRequired();
                entity.Property(e => e.Specializations)
                    .HasConversion(
                        v => string.Join(',', v),
                        v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    );
                entity.Property(e => e.HourlyRate)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                entity.Property(e => e.Certifications)
                    .HasConversion(
                        v => string.Join(',', v),
                        v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    );
                entity.Property(e => e.Languages)
                    .HasConversion(
                        v => string.Join(',', v),
                        v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    );
                entity.Property(e => e.AssociatedBusinessIds)
                    .HasConversion(
                        v => string.Join(',', v),
                        v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    )
                    .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                        (c1, c2) => c1.SequenceEqual(c2),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()
                    ));
                entity.Property(e => e.Rating)
                    .HasColumnType("decimal(3,2)");
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Foreign key relationship
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint on NIP
                entity.HasIndex(e => e.Nip).IsUnique();
                entity.HasIndex(e => e.UserId).IsUnique();
            });

            // TrainerBusinessAssociation entity configuration
            modelBuilder.Entity<TrainerBusinessAssociation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TrainerProfileId).IsRequired();
                entity.Property(e => e.BusinessProfileId).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.ConfirmationToken).HasMaxLength(100);
                entity.Property(e => e.RejectionReason).HasMaxLength(500);
                entity.Property(e => e.RequestedAt).IsRequired();

                // Foreign key relationships
                entity.HasOne(e => e.TrainerProfile)
                    .WithMany()
                    .HasForeignKey(e => e.TrainerProfileId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.BusinessProfile)
                    .WithMany()
                    .HasForeignKey(e => e.BusinessProfileId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint - one association per trainer-business pair
                entity.HasIndex(e => new { e.TrainerProfileId, e.BusinessProfileId }).IsUnique();

                // Index for token lookup
                entity.HasIndex(e => e.ConfirmationToken);
            });

            // BusinessParentChildAssociation entity configuration
            modelBuilder.Entity<BusinessParentChildAssociation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ChildBusinessProfileId).IsRequired();
                entity.Property(e => e.ParentBusinessProfileId).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.ConfirmationToken).HasMaxLength(100);
                entity.Property(e => e.RejectionReason).HasMaxLength(500);
                entity.Property(e => e.RequestedAt).IsRequired();

                // Foreign key relationships
                entity.HasOne(e => e.ChildBusinessProfile)
                    .WithMany()
                    .HasForeignKey(e => e.ChildBusinessProfileId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ParentBusinessProfile)
                    .WithMany()
                    .HasForeignKey(e => e.ParentBusinessProfileId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint - one association per child-parent pair
                entity.HasIndex(e => new { e.ChildBusinessProfileId, e.ParentBusinessProfileId }).IsUnique();

                // Index for token lookup
                entity.HasIndex(e => e.ConfirmationToken);

                // Index for quick lookup by child business
                entity.HasIndex(e => e.ChildBusinessProfileId);
            });

            // TrainerScheduleTemplate entity configuration
            modelBuilder.Entity<TrainerScheduleTemplate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TrainerProfileId)
                    .IsRequired();
                entity.Property(e => e.ScheduleType)
                    .IsRequired();
                entity.Property(e => e.Time)
                    .IsRequired()
                    .HasMaxLength(10);
                entity.Property(e => e.IsAvailable)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Foreign key relationship to TrainerProfile
                entity.HasOne(e => e.TrainerProfile)
                    .WithMany(tp => tp.ScheduleTemplates)
                    .HasForeignKey(e => e.TrainerProfileId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Foreign key relationship to BusinessProfile (optional - for business-assigned slots)
                entity.HasOne(e => e.AssociatedBusiness)
                    .WithMany()
                    .HasForeignKey(e => e.AssociatedBusinessId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);

                // Unique constraint: one time slot per schedule type per trainer
                entity.HasIndex(e => new { e.TrainerProfileId, e.ScheduleType, e.Time }).IsUnique();
            });

            // TrainerDateAvailability entity configuration
            modelBuilder.Entity<TrainerDateAvailability>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TrainerProfileId)
                    .IsRequired();
                entity.Property(e => e.Date)
                    .IsRequired();
                entity.Property(e => e.Time)
                    .IsRequired()
                    .HasMaxLength(10);
                entity.Property(e => e.IsAvailable)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Foreign key relationship to TrainerProfile
                entity.HasOne(e => e.TrainerProfile)
                    .WithMany(tp => tp.DateAvailabilities)
                    .HasForeignKey(e => e.TrainerProfileId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Foreign key relationship to BusinessProfile (optional - for business-assigned slots)
                entity.HasOne(e => e.AssociatedBusiness)
                    .WithMany()
                    .HasForeignKey(e => e.AssociatedBusinessId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);

                // Unique constraint: one time slot per date per trainer
                entity.HasIndex(e => new { e.TrainerProfileId, e.Date, e.Time }).IsUnique();
            });

            // BusinessDateAvailability entity configuration
            modelBuilder.Entity<BusinessDateAvailability>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BusinessProfileId)
                    .IsRequired();
                entity.Property(e => e.Date)
                    .IsRequired();
                entity.Property(e => e.Time)
                    .IsRequired()
                    .HasMaxLength(10);
                entity.Property(e => e.IsAvailable)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Foreign key relationship
                entity.HasOne(e => e.BusinessProfile)
                    .WithMany(bp => bp.DateAvailabilities)
                    .HasForeignKey(e => e.BusinessProfileId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint: one time slot per date per business
                entity.HasIndex(e => new { e.BusinessProfileId, e.Date, e.Time }).IsUnique();
            });

            // FacilityScheduleTemplate entity configuration
            modelBuilder.Entity<FacilityScheduleTemplate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FacilityId)
                    .IsRequired();
                entity.Property(e => e.ScheduleType)
                    .IsRequired();
                entity.Property(e => e.Time)
                    .IsRequired()
                    .HasMaxLength(10);
                entity.Property(e => e.IsAvailable)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Foreign key relationship
                entity.HasOne(e => e.Facility)
                    .WithMany(f => f.ScheduleTemplates)
                    .HasForeignKey(e => e.FacilityId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint: one time slot per schedule type per facility
                entity.HasIndex(e => new { e.FacilityId, e.ScheduleType, e.Time }).IsUnique();
            });

            // FacilityDateAvailability entity configuration
            modelBuilder.Entity<FacilityDateAvailability>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FacilityId)
                    .IsRequired();
                entity.Property(e => e.Date)
                    .IsRequired();
                entity.Property(e => e.Time)
                    .IsRequired()
                    .HasMaxLength(10);
                entity.Property(e => e.IsAvailable)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Foreign key relationship
                entity.HasOne(e => e.Facility)
                    .WithMany(f => f.DateAvailabilities)
                    .HasForeignKey(e => e.FacilityId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint: one time slot per date per facility
                entity.HasIndex(e => new { e.FacilityId, e.Date, e.Time }).IsUnique();
            });

            // Training entity configuration
            modelBuilder.Entity<Training>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TrainerProfileId)
                    .IsRequired();
                entity.Property(e => e.FacilityId)
                    .IsRequired();
                entity.Property(e => e.ReservationId)
                    .IsRequired(false);
                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.Description)
                    .HasMaxLength(1000);
                entity.Property(e => e.Duration)
                    .IsRequired();
                entity.Property(e => e.MaxParticipants)
                    .IsRequired();
                entity.Property(e => e.Price)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Foreign key relationships
                entity.HasOne(e => e.TrainerProfile)
                    .WithMany()
                    .HasForeignKey(e => e.TrainerProfileId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Facility)
                    .WithMany()
                    .HasForeignKey(e => e.FacilityId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Reservation)
                    .WithMany()
                    .HasForeignKey(e => e.ReservationId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes for better performance
                entity.HasIndex(e => e.TrainerProfileId);
                entity.HasIndex(e => e.FacilityId);
                entity.HasIndex(e => e.ReservationId);
            });

            // TrainingSession entity configuration
            modelBuilder.Entity<TrainingSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TrainingId)
                    .IsRequired();
                entity.Property(e => e.Date)
                    .IsRequired();
                entity.Property(e => e.StartTime)
                    .IsRequired()
                    .HasMaxLength(10);
                entity.Property(e => e.EndTime)
                    .IsRequired()
                    .HasMaxLength(10);
                entity.Property(e => e.CurrentParticipants)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Foreign key relationship
                entity.HasOne(e => e.Training)
                    .WithMany(t => t.Sessions)
                    .HasForeignKey(e => e.TrainingId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes for better performance
                entity.HasIndex(e => e.TrainingId);
                entity.HasIndex(e => new { e.TrainingId, e.Date });
            });

            // TrainingParticipant entity configuration
            modelBuilder.Entity<TrainingParticipant>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TrainingId)
                    .IsRequired();
                entity.Property(e => e.UserId)
                    .IsRequired();
                entity.Property(e => e.PaymentId)
                    .IsRequired();
                entity.Property(e => e.JoinedAt)
                    .IsRequired();
                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasMaxLength(50);
                entity.Property(e => e.Notes)
                    .HasMaxLength(500);
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Foreign key relationships
                entity.HasOne(e => e.Training)
                    .WithMany(t => t.Participants)
                    .HasForeignKey(e => e.TrainingId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Payment)
                    .WithMany()
                    .HasForeignKey(e => e.PaymentId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Unique constraint: one active participation per user per training
                entity.HasIndex(e => new { e.TrainingId, e.UserId }).IsUnique();

                // Indexes for better performance
                entity.HasIndex(e => e.TrainingId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.PaymentId);
                entity.HasIndex(e => new { e.UserId, e.Status });
            });

            // SocialWallPost entity configuration
            modelBuilder.Entity<SocialWallPost>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type)
                    .IsRequired()
                    .HasMaxLength(50);
                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.Content)
                    .IsRequired()
                    .HasMaxLength(2000);
                entity.Property(e => e.AuthorId)
                    .IsRequired();
                entity.Property(e => e.ReservationId)
                    .IsRequired(false);
                entity.Property(e => e.TrainingId)
                    .IsRequired(false);
                entity.Property(e => e.ImageUrl)
                    .HasMaxLength(500);
                entity.Property(e => e.Tags)
                    .HasConversion(
                        v => string.Join(',', v),
                        v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    );
                entity.Property(e => e.IsActive)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Foreign key relationships
                entity.HasOne(e => e.Author)
                    .WithMany()
                    .HasForeignKey(e => e.AuthorId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Reservation)
                    .WithMany()
                    .HasForeignKey(e => e.ReservationId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Training)
                    .WithMany()
                    .HasForeignKey(e => e.TrainingId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes for better performance
                entity.HasIndex(e => e.AuthorId);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => new { e.Type, e.IsActive });
            });

            // SocialWallPostLike entity configuration
            modelBuilder.Entity<SocialWallPostLike>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PostId)
                    .IsRequired();
                entity.Property(e => e.UserId)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();

                // Foreign key relationships
                entity.HasOne(e => e.Post)
                    .WithMany(p => p.Likes)
                    .HasForeignKey(e => e.PostId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint: one like per user per post
                entity.HasIndex(e => new { e.PostId, e.UserId }).IsUnique();

                // Indexes for better performance
                entity.HasIndex(e => e.PostId);
                entity.HasIndex(e => e.UserId);
            });

            // UserFavouriteBusinessProfile entity configuration
            modelBuilder.Entity<UserFavouriteBusinessProfile>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId)
                    .IsRequired();
                entity.Property(e => e.BusinessProfileId)
                    .IsRequired();
                entity.Property(e => e.AddedAt)
                    .IsRequired();

                // Foreign key relationships
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.BusinessProfile)
                    .WithMany()
                    .HasForeignKey(e => e.BusinessProfileId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint: one favourite per user per business profile
                entity.HasIndex(e => new { e.UserId, e.BusinessProfileId }).IsUnique();

                // Indexes for better performance
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.BusinessProfileId);
            });

            // SocialWallPostComment entity configuration
            modelBuilder.Entity<SocialWallPostComment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PostId)
                    .IsRequired();
                entity.Property(e => e.UserId)
                    .IsRequired();
                entity.Property(e => e.Content)
                    .IsRequired()
                    .HasMaxLength(1000);
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Foreign key relationships
                entity.HasOne(e => e.Post)
                    .WithMany(p => p.Comments)
                    .HasForeignKey(e => e.PostId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes for better performance
                entity.HasIndex(e => e.PostId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => new { e.PostId, e.CreatedAt });
            });

            // PendingTimeSlotReservation entity configuration
            modelBuilder.Entity<PendingTimeSlotReservation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FacilityId)
                    .IsRequired();
                entity.Property(e => e.UserId)
                    .IsRequired();
                entity.Property(e => e.Date)
                    .IsRequired();
                entity.Property(e => e.TimeSlots)
                    .HasConversion(
                        v => string.Join(',', v),
                        v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    );
                entity.Property(e => e.ExpiresAt)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();

                // Foreign key relationships
                entity.HasOne(e => e.Facility)
                    .WithMany()
                    .HasForeignKey(e => e.FacilityId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.TrainerProfile)
                    .WithMany()
                    .HasForeignKey(e => e.TrainerProfileId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes for better performance
                entity.HasIndex(e => new { e.FacilityId, e.Date });
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.ExpiresAt);
                entity.HasIndex(e => new { e.FacilityId, e.Date, e.ExpiresAt });
            });

            // PendingTrainingParticipant entity configuration
            modelBuilder.Entity<PendingTrainingParticipant>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TrainingId)
                    .IsRequired();
                entity.Property(e => e.UserId)
                    .IsRequired();
                entity.Property(e => e.Notes)
                    .HasMaxLength(500);
                entity.Property(e => e.ExpiresAt)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();

                // Foreign key relationships
                entity.HasOne(e => e.Training)
                    .WithMany()
                    .HasForeignKey(e => e.TrainingId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes for better performance
                entity.HasIndex(e => new { e.TrainingId, e.UserId });
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.ExpiresAt);
                entity.HasIndex(e => new { e.TrainingId, e.ExpiresAt });
            });

            // TPay Legal Forms entity configuration
            modelBuilder.Entity<TPayLegalForm>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever(); // TPay provides the ID
                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.IsActive)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();
                entity.Property(e => e.LastSyncedAt);

                // Indexes
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.LastSyncedAt);
            });

            // TPay Categories entity configuration
            modelBuilder.Entity<TPayCategory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever(); // TPay provides the ID
                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.ParentId);
                entity.Property(e => e.IsActive)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();
                entity.Property(e => e.LastSyncedAt);

                // Self-referencing relationship for parent/child categories
                entity.HasOne(e => e.Parent)
                    .WithMany(e => e.Children)
                    .HasForeignKey(e => e.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes
                entity.HasIndex(e => e.ParentId);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.LastSyncedAt);
            });

            // TPay Dictionary Sync entity configuration
            modelBuilder.Entity<TPayDictionarySync>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DictionaryType)
                    .IsRequired()
                    .HasMaxLength(50);
                entity.Property(e => e.LastSyncAt)
                    .IsRequired();
                entity.Property(e => e.IsSuccessful)
                    .IsRequired();
                entity.Property(e => e.ErrorMessage)
                    .HasMaxLength(1000);
                entity.Property(e => e.RecordsCount)
                    .IsRequired();
                entity.Property(e => e.SyncVersion)
                    .HasMaxLength(50);

                // Indexes
                entity.HasIndex(e => e.DictionaryType);
                entity.HasIndex(e => e.LastSyncAt);
                entity.HasIndex(e => e.IsSuccessful);
                entity.HasIndex(e => new { e.DictionaryType, e.LastSyncAt });
            });

            // PrivacySettings entity configuration
            modelBuilder.Entity<PrivacySettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId)
                    .IsRequired();
                entity.Property(e => e.Analytics)
                    .IsRequired()
                    .HasDefaultValue(false);
                entity.Property(e => e.CrashReports)
                    .IsRequired()
                    .HasDefaultValue(true);
                entity.Property(e => e.LocationTracking)
                    .IsRequired()
                    .HasDefaultValue(false);
                entity.Property(e => e.DataSharing)
                    .IsRequired()
                    .HasDefaultValue(false);
                entity.Property(e => e.MarketingEmails)
                    .IsRequired()
                    .HasDefaultValue(false);
                entity.Property(e => e.PushNotifications)
                    .IsRequired()
                    .HasDefaultValue(true);
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();
                entity.Property(e => e.Version)
                    .IsRequired()
                    .HasDefaultValue(1);

                // Foreign key relationship
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint: one privacy settings per user
                entity.HasIndex(e => e.UserId).IsUnique();

                // Index for better performance
                entity.HasIndex(e => e.UpdatedAt);
            });

            // ExternalAuth entity configuration
            modelBuilder.Entity<ExternalAuth>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId)
                    .IsRequired();
                entity.Property(e => e.Provider)
                    .IsRequired()
                    .HasConversion<int>();
                entity.Property(e => e.ExternalUserId)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.Email)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.DisplayName)
                    .HasMaxLength(255);
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Foreign key relationship
                entity.HasOne(e => e.User)
                    .WithMany(u => u.ExternalAuths)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraints
                entity.HasIndex(e => new { e.Provider, e.ExternalUserId }).IsUnique();
                entity.HasIndex(e => new { e.Provider, e.Email }).IsUnique();
                
                // Indexes for better performance
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Provider);
                entity.HasIndex(e => e.ExternalUserId);
                entity.HasIndex(e => e.Email);
            });

            // GlobalSettings entity configuration
            modelBuilder.Entity<GlobalSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Key)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.Value)
                    .IsRequired()
                    .HasMaxLength(2000);
                entity.Property(e => e.Description)
                    .HasMaxLength(1000);
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Unique constraint on key
                entity.HasIndex(e => e.Key).IsUnique();
                
                // Index for better performance
                entity.HasIndex(e => e.UpdatedAt);
            });

            // AgentInvitation entity configuration
            modelBuilder.Entity<AgentInvitation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.InvitationToken)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.BusinessProfileId)
                    .IsRequired();
                entity.Property(e => e.InvitedByUserId)
                    .IsRequired();
                entity.Property(e => e.ExpiresAt)
                    .IsRequired();
                entity.Property(e => e.IsUsed)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Relationships
                entity.HasOne(e => e.BusinessProfile)
                    .WithMany()
                    .HasForeignKey(e => e.BusinessProfileId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.InvitedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.InvitedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.AcceptedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.AcceptedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes
                entity.HasIndex(e => e.Email);
                entity.HasIndex(e => e.InvitationToken).IsUnique();
                entity.HasIndex(e => e.BusinessProfileId);
                entity.HasIndex(e => e.ExpiresAt);
            });

            // BusinessProfileAgent entity configuration
            modelBuilder.Entity<BusinessProfileAgent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BusinessProfileId)
                    .IsRequired();
                entity.Property(e => e.AgentUserId)
                    .IsRequired();
                entity.Property(e => e.AssignedByUserId)
                    .IsRequired();
                entity.Property(e => e.AssignedAt)
                    .IsRequired();
                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasColumnName("IsActive");
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                // Relationships
                entity.HasOne(e => e.BusinessProfile)
                    .WithMany()
                    .HasForeignKey(e => e.BusinessProfileId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.AgentUser)
                    .WithMany()
                    .HasForeignKey(e => e.AgentUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.AssignedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.AssignedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes
                entity.HasIndex(e => e.BusinessProfileId);
                entity.HasIndex(e => e.AgentUserId);
                entity.HasIndex(e => new { e.BusinessProfileId, e.AgentUserId }).IsUnique()
                    .HasFilter("\"IsActive\" = true"); // Unique constraint for active agents only
            });

            // Product entity configuration
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.Subtitle)
                    .HasMaxLength(500);
                entity.Property(e => e.Description)
                    .IsRequired()
                    .HasMaxLength(2000);
                entity.Property(e => e.Price)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                entity.Property(e => e.Usage)
                    .IsRequired();
                entity.Property(e => e.Period)
                    .IsRequired()
                    .HasConversion<int>();
                entity.Property(e => e.NumOfPeriods)
                    .IsRequired();
                entity.Property(e => e.StartDate)
                    .IsRequired();
                entity.Property(e => e.EndDate)
                    .IsRequired();
                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValue(true);
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();
                entity.Property(e => e.BusinessProfileId)
                    .IsRequired();
                entity.Property(e => e.UserId);

                // Foreign key relationships
                entity.HasOne(e => e.BusinessProfile)
                    .WithMany(bp => bp.Products)
                    .HasForeignKey(e => e.BusinessProfileId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes for better performance
                entity.HasIndex(e => new { e.BusinessProfileId, e.IsActive });
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CreatedAt);
            });

            // KSeFInvoice entity configuration
            modelBuilder.Entity<KSeFInvoice>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.PaymentId).IsRequired();
                entity.Property(e => e.BusinessProfileId).IsRequired();

                entity.Property(e => e.InvoiceNumber)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.KSeFReferenceNumber)
                    .HasMaxLength(255);

                entity.Property(e => e.SellerNIP)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(e => e.SellerName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.BuyerNIP)
                    .HasMaxLength(20);

                entity.Property(e => e.BuyerName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.NetAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.VATAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.GrossAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasMaxLength(50);

                // Foreign key relationships
                entity.HasOne(e => e.Payment)
                    .WithMany()
                    .HasForeignKey(e => e.PaymentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Reservation)
                    .WithMany()
                    .HasForeignKey(e => e.ReservationId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.BusinessProfile)
                    .WithMany()
                    .HasForeignKey(e => e.BusinessProfileId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes
                entity.HasIndex(e => e.PaymentId);
                entity.HasIndex(e => e.BusinessProfileId);
                entity.HasIndex(e => e.KSeFReferenceNumber);
                entity.HasIndex(e => e.InvoiceNumber);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAt);
            });

            // ProductPurchase entity configuration
            modelBuilder.Entity<ProductPurchase>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Price)
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.ProductTitle)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.ProductSubtitle)
                    .HasMaxLength(500);

                entity.Property(e => e.ProductDescription)
                    .IsRequired()
                    .HasMaxLength(2000);

                entity.Property(e => e.BusinessName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.ProductPeriod)
                    .HasConversion<int>();

                // Foreign key relationships
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Payment)
                    .WithMany()
                    .HasForeignKey(e => e.PaymentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.UsageLogs)
                    .WithOne(l => l.ProductPurchase)
                    .HasForeignKey(l => l.ProductPurchaseId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.ProductId);
                entity.HasIndex(e => e.PaymentId);
                entity.HasIndex(e => new { e.UserId, e.Status, e.ExpiryDate });
                entity.HasIndex(e => e.PurchaseDate);
            });

            // ProductUsageLog entity configuration
            modelBuilder.Entity<ProductUsageLog>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Notes)
                    .HasMaxLength(500);

                // Foreign key relationships
                entity.HasOne(e => e.ProductPurchase)
                    .WithMany(p => p.UsageLogs)
                    .HasForeignKey(e => e.ProductPurchaseId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Facility)
                    .WithMany()
                    .HasForeignKey(e => e.FacilityId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes
                entity.HasIndex(e => e.ProductPurchaseId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.UsageDate);
            });

            // Category entity configuration
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Slug)
                    .IsRequired()
                    .HasMaxLength(100);
                entity.Property(e => e.IsActive)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
                entity.Property(e => e.UpdatedAt)
                    .IsRequired();

                entity.HasIndex(e => e.Slug).IsUnique();
            });

            // CategoryTranslation entity configuration
            modelBuilder.Entity<CategoryTranslation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.LanguageCode)
                    .IsRequired()
                    .HasMaxLength(10);
                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(255);
                entity.Property(e => e.Description)
                    .HasColumnType("text");

                entity.HasOne(e => e.Category)
                    .WithMany(c => c.Translations)
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);

                // One translation per language per category
                entity.HasIndex(e => new { e.CategoryId, e.LanguageCode }).IsUnique();
            });
        }
    }
}
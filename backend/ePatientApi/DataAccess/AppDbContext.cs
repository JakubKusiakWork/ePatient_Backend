using Microsoft.EntityFrameworkCore;
using ePatientApi.Models;

namespace ePatientApi.DataAccess
{
    /// <summary>
    /// Entity Framework database context for the ePatient application.
    /// </summary>
    public class AppDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of <see cref="AppDbContext"/> with supplied options.
        /// </summary>
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<RegisteredDoctor> RegisteredDoctors { get; set; } = null!;
        public DbSet<RegisteredPatient> RegisteredPatients { get; set; } = null!;
        public DbSet<RegisteredPatient> Patients { get; set; } = null!;
        public DbSet<LoggedDoctor> LoggedDoctors { get; set; } = null!;
        public DbSet<LoggedUser> LoggedUsers { get; set; } = null!;
        public DbSet<WebAuthnCredentials> WebAuthnCredentials { get; set; } = null!;
        public DbSet<PatientExaminationData> PatientExaminations { get; set; } = null!;
        public DbSet<MedicalReport> MedicalReports { get; set; } = null!;
        public DbSet<AppointmentData> Appointments { get; set; } = null!;
        public DbSet<AppointmentSnapshot> Snapshots { get; set; } = null!;
        public DbSet<DoctorEmail> DoctorEmails { get; set; } = null!; 
        public DbSet<HealthCardEntity> HealthCards { get; set; } = null!;
        public DbSet<HealthCardVersion> HealthCardVersions { get; set; } = null!;
        public DbSet<ForgotPassword> ForgotPassword { get; set; }
        public DbSet<Pharmacy> Pharmacies { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<AvailabilityCheck> AvailabilityChecks { get; set; } = null!;
        public DbSet<Prescription> Prescriptions { get; set; } = null!;

        /// <summary>
        /// Configures the database schema and relationships.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RegisteredDoctor>(entity =>
            {
                entity.ToTable("registereddoctor");
                entity.HasKey(e => e.DoctorId);
                entity.Property(e => e.DoctorId).HasColumnName("id").UseIdentityColumn();
                entity.Property(e => e.DoctorFirstName).HasColumnName("firstname").IsRequired();
                entity.Property(e => e.DoctorLastName).HasColumnName("lastname").IsRequired();
                entity.Property(e => e.DoctorCode).HasColumnName("doctorcode").IsRequired();
                entity.Property(e => e.DoctorEmail).HasColumnName("doctor_email").IsRequired();
                entity.Property(e => e.Role).HasColumnName("role").IsRequired();
                entity.HasIndex(e => e.DoctorCode).IsUnique();
                entity.HasIndex(e => e.DoctorEmail).IsUnique();
            });
            modelBuilder.Entity<RegisteredPatient>(entity =>
            {
                entity.ToTable("registeredpatient");
                entity.HasKey(e => e.BirthNumber);
                entity.Property(e => e.BirthNumber).HasColumnName("birthnumber");
                entity.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
                entity.HasAlternateKey(e => e.Id).HasName("ux_registeredpatient_id");
                entity.Property(e => e.FirstName).HasColumnName("firstname");
                entity.Property(e => e.LastName).HasColumnName("lastname");
                entity.Property(e => e.Email).HasColumnName("email");
                entity.Property(e => e.PhoneNumber).HasColumnName("phonenumber");
                entity.Property(e => e.HashedPassword).HasColumnName("hashedpassword");
                entity.Property(e => e.Role).HasColumnName("role");
                entity.Property(e => e.Insurance).HasColumnName("insurance");
            });

            modelBuilder.Entity<LoggedDoctor>(entity =>
            {
                entity.ToTable("loggeddoctor");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
                entity.Property(e => e.DoctorFirstName).HasColumnName("doctorfirstname").IsRequired();
                entity.Property(e => e.DoctorLastName).HasColumnName("doctorlastname").IsRequired();
                entity.Property(e => e.DoctorCode).HasColumnName("doctorcode").IsRequired();
                entity.Property(e => e.Created_at).HasColumnName("created_at").IsRequired();
                entity.Property(e => e.HashedPassword).HasColumnName("hashedpassword").IsRequired();
            });

            modelBuilder.Entity<AppointmentData>(entity =>
            {
                entity.ToTable("appointments");
                entity.HasKey(e => e.AppointmentId);
                entity.Property(e => e.ReservationTime);
                entity.Property(e => e.ReservationDay);
                entity.Property<string>("PatientBirthNumber").HasColumnName("patient_birth_number");
                entity.Property<int?>("DoctorId").HasColumnName("doctor_id");
                entity.Property<string?>("DoctorName").HasColumnName("doctor_name");
            });

            modelBuilder.Entity<MedicalReport>(entity =>
            {
                entity.ToTable("medicalreports");
                entity.HasKey(e => e.ReportId);
                entity.Property(e => e.Medication).IsRequired();
                entity.Property(e => e.ExternalExaminations)
                .HasColumnName("externalexaminations")
                .IsRequired();
                entity.Property(e => e.AttendingDoctor).HasColumnName("attendingdoctor");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property<int?>("PatientId").HasColumnName("patient_id");
                entity.HasOne(m => m.Patient)
                .WithMany()
                .HasForeignKey(m => m.PatientBirthNumber);
            });
            modelBuilder.Entity<LoggedUser>(entity =>
            {
                entity.ToTable("loggeduser");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id").UseIdentityColumn();
                entity.Property(e => e.Username).HasColumnName("Username").IsRequired();
                entity.Property(e => e.FirstName).HasColumnName("FirstName").IsRequired();
                entity.Property(e => e.LastName).HasColumnName("LastName").IsRequired();
                entity.Property(e => e.Role).HasColumnName("role").IsRequired();
            });
            modelBuilder.Entity<WebAuthnCredentials>(entity =>
            {
                entity.ToTable("webauthncredentials");
                entity.HasIndex(e => new { e.UserId, e.UserType }).IsUnique();
            });
            modelBuilder.Entity<AppointmentSnapshot>(entity =>
            {
                entity.ToTable("appointmentsdetails");
                entity.HasKey(e => e.SnapshotId);
                entity.Property(e => e.SnapshotId).HasColumnName("snapshot_id").UseIdentityColumn();
                entity.Property(e => e.AppointmentId).HasColumnName("appointment_id");
                entity.Property(e => e.PatientId).HasColumnName("patient_id");
                entity.Property(e => e.BirthNumber).HasColumnName("birth_number");
                entity.Property(e => e.FirstName).HasColumnName("first_name");
                entity.Property(e => e.LastName).HasColumnName("last_name");
                entity.Property(e => e.Insurance).HasColumnName("insurance");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
            modelBuilder.Entity<HealthCardEntity>(entity =>
            {
                entity.ToTable("healthcards");
                entity.HasKey(e => e.HealthCardId);
                entity.Property(e => e.HealthCardId).HasColumnName("healthcard_id").UseIdentityColumn();
                entity.Property(e => e.PatientBirthNumber).HasColumnName("patient_birth_number");
                entity.Property(e => e.PatientId).HasColumnName("patient_id");
                entity.Property(e => e.BloodType).HasColumnName("blood_type");
                entity.Property(e => e.Labs).HasColumnName("labs");
                entity.Property(e => e.AdvanceDirectives).HasColumnName("advance_directives");
                entity.Property(e => e.ConsentPreferences).HasColumnName("consent_preferences");
                entity.HasOne(h => h.Patient)
                    .WithMany()
                    .HasForeignKey(h => h.PatientId)
                    .HasPrincipalKey("Id");
            });

            modelBuilder.Entity<HealthCardVersion>(entity =>
            {
                entity.ToTable("healthcard_versions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.HealthCardId).HasColumnName("healthcard_id");
                entity.Property(e => e.VersionNumber).HasColumnName("version_number");
                entity.Property(e => e.DataSnapshot).HasColumnName("data_snapshot").HasColumnType("jsonb");
                entity.Property(e => e.ModifiedBy).HasColumnName("modified_by");
                entity.Property(e => e.ModifiedAt).HasColumnName("modified_at");
                entity.Property(e => e.ChangeSummary).HasColumnName("change_summary");
            });


            modelBuilder.Entity<AppointmentData>()
                        .HasOne(a => a.Patient)
                        .WithMany(p => p.Appointments)
                        .HasForeignKey(a => a.PatientBirthNumber);

            modelBuilder.Entity<Pharmacy>(entity =>
            {
                entity.ToTable("pharmacies");
                entity.HasKey(e => e.PharmacyId);
                entity.Property(e => e.PharmacyId).HasColumnName("id").UseIdentityColumn();
                entity.Property(e => e.ExternalId).HasColumnName("external_id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.BaseUrl).HasColumnName("base_url");
                entity.HasIndex(e => e.ExternalId).IsUnique();
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("products");
                entity.HasKey(e => e.ProductId);
                entity.Property(e => e.ProductId).HasColumnName("id").UseIdentityColumn();
                entity.Property(e => e.ExternalCode).HasColumnName("external_code");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.HasIndex(e => e.ExternalCode).IsUnique(false);
            });

            modelBuilder.Entity<AvailabilityCheck>(entity =>
            {
                entity.ToTable("availability_checks");
                entity.HasKey(e => e.AvailabilityCheckId);
                entity.Property(e => e.AvailabilityCheckId).HasColumnName("id").UseIdentityColumn();
                entity.Property(e => e.PharmacyId).HasColumnName("pharmacy_id");
                entity.Property(e => e.ProductId).HasColumnName("product_id");
                entity.Property(e => e.Timestamp).HasColumnName("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.Price).HasColumnName("price");
                entity.Property(e => e.DetailsJson).HasColumnName("details").HasColumnType("jsonb");
                entity.Property(e => e.ScraperVersion).HasColumnName("scraper_version");
                entity.HasOne(e => e.Pharmacy).WithMany(p => p.AvailabilityChecks).HasForeignKey(e => e.PharmacyId);
                entity.HasOne(e => e.Product).WithMany(p => p.AvailabilityChecks).HasForeignKey(e => e.ProductId);
            });

            modelBuilder.Entity<ForgotPassword>(entity =>
            {
                entity.HasKey(f => f.Id);

                entity.HasOne(f => f.Patient)
                    .WithMany()
                    .HasForeignKey(f => f.PatientBirthNumber);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
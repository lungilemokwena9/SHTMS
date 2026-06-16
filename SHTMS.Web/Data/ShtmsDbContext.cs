using Microsoft.EntityFrameworkCore;
using SHTMS.Web.Models;

namespace SHTMS.Web.Data
{
    public class ShtmsDbContext : DbContext
    {
        public ShtmsDbContext(DbContextOptions<ShtmsDbContext> options) : base(options) { }

        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Nurse> Nurses { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Consultation> Consultations { get; set; }
        public DbSet<PatientVitals> PatientVitals { get; set; }
        public DbSet<Medication> Medications { get; set; }
        public DbSet<Prescription> Prescriptions { get; set; }
        public DbSet<Admission> Admissions { get; set; }
        public DbSet<Billing> Billings { get; set; }
        public DbSet<HealthReport> HealthReports { get; set; }
        public DbSet<AIAlert> AIAlerts { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Billing: BalanceAmount is a generated column — don't let EF try to insert it
            modelBuilder.Entity<Billing>()
                .Property(b => b.BalanceAmount)
                .HasComputedColumnSql("(`TotalAmount` - `PaidAmount`)", stored: true);

            // Enum-like string columns
            modelBuilder.Entity<Patient>()
                .Property(p => p.Gender)
                .HasColumnType("enum('Male','Female','Other')");

            modelBuilder.Entity<Patient>()
                .Property(p => p.BloodType)
                .HasColumnType("enum('A+','A-','B+','B-','AB+','AB-','O+','O-','Unknown')")
                .HasDefaultValue("Unknown");

            modelBuilder.Entity<Appointment>()
                .Property(a => a.AppointmentType)
                .HasColumnType("enum('InPerson','Telemedicine')")
                .HasDefaultValue("InPerson");

            modelBuilder.Entity<Appointment>()
                .Property(a => a.Status)
                .HasColumnType("enum('Scheduled','Confirmed','Completed','Cancelled','NoShow')")
                .HasDefaultValue("Scheduled");

            modelBuilder.Entity<Admission>()
                .Property(a => a.Status)
                .HasColumnType("enum('Admitted','Discharged','Transferred')")
                .HasDefaultValue("Admitted");

            modelBuilder.Entity<Billing>()
                .Property(b => b.PaymentStatus)
                .HasColumnType("enum('Pending','PartiallyPaid','Paid','Waived')")
                .HasDefaultValue("Pending");

            modelBuilder.Entity<Billing>()
                .Property(b => b.PaymentMethod)
                .HasColumnType("enum('Cash','Card','Insurance','EFT','Waived')")
                .HasDefaultValue("Cash");
        }
    }
}

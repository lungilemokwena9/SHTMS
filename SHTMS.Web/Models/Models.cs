using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SHTMS.Web.Models
{
    // ─── Role ────────────────────────────────────────────────
    [Table("Roles")]
    public class Role
    {
        [Key] public int RoleID { get; set; }
        [Required, StringLength(50)] public string RoleName { get; set; } = "";
        public string? Description { get; set; }
        public ICollection<User> Users { get; set; } = new List<User>();
    }

    // ─── User ────────────────────────────────────────────────
    [Table("Users")]
    public class User
    {
        [Key] public int UserID { get; set; }
        [Required, StringLength(100)] public string Username { get; set; } = "";
        [Required, StringLength(255)] public string PasswordHash { get; set; } = "";
        [Required, StringLength(150)] public string Email { get; set; } = "";
        public int RoleID { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? LastLogin { get; set; }
        [ForeignKey("RoleID")] public Role? Role { get; set; }
    }

    // ─── Patient ─────────────────────────────────────────────
    [Table("Patients")]
    public class Patient
    {
        [Key] public int PatientID { get; set; }
        public int? UserID { get; set; }
        [Required, StringLength(100), Display(Name = "First Name")] public string FirstName { get; set; } = "";
        [Required, StringLength(100), Display(Name = "Last Name")] public string LastName { get; set; } = "";
        [Required, DataType(DataType.Date), Display(Name = "Date of Birth")] public DateTime DateOfBirth { get; set; }
        [Required] public string Gender { get; set; } = "";
        [StringLength(20), Display(Name = "ID Number")] public string? IDNumber { get; set; }
        [StringLength(20), Display(Name = "Phone")] public string? PhoneNumber { get; set; }
        [StringLength(150)] public string? Email { get; set; }
        public string? Address { get; set; }
        [Display(Name = "Blood Type")] public string BloodType { get; set; } = "Unknown";
        public string? Allergies { get; set; }
        public string? ProfilePhoto { get; set; }
        [Display(Name = "Emergency Contact")] public string? EmergencyContact { get; set; }
        [Display(Name = "Emergency Phone")] public string? EmergencyPhone { get; set; }
        public DateTime RegistrationDate { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        [ForeignKey("UserID")] public User? User { get; set; }
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<Admission> Admissions { get; set; } = new List<Admission>();

        [NotMapped] public string FullName => $"{FirstName} {LastName}";
        [NotMapped] public int Age => DateTime.Now.Year - DateOfBirth.Year - (DateTime.Now.DayOfYear < DateOfBirth.DayOfYear ? 1 : 0);
    }

    // ─── Doctor ──────────────────────────────────────────────
    [Table("Doctors")]
    public class Doctor
    {
        [Key] public int DoctorID { get; set; }
        public int? UserID { get; set; }
        [Required, StringLength(100), Display(Name = "First Name")] public string FirstName { get; set; } = "";
        [Required, StringLength(100), Display(Name = "Last Name")] public string LastName { get; set; } = "";
        [Required, StringLength(100)] public string Specialization { get; set; } = "";
        [Required, StringLength(50), Display(Name = "License No.")] public string LicenseNumber { get; set; } = "";
        [StringLength(20)] public string? PhoneNumber { get; set; }
        [StringLength(150)] public string? Email { get; set; }
        public string? Department { get; set; }
        public bool IsAvailable { get; set; } = true;
        public string? ProfilePhoto { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [ForeignKey("UserID")] public User? User { get; set; }
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

        [NotMapped] public string FullName => $"Dr. {FirstName} {LastName}";
    }

    // ─── Nurse ───────────────────────────────────────────────
    [Table("Nurses")]
    public class Nurse
    {
        [Key] public int NurseID { get; set; }
        public int? UserID { get; set; }
        [Required, StringLength(100), Display(Name = "First Name")] public string FirstName { get; set; } = "";
        [Required, StringLength(100), Display(Name = "Last Name")] public string LastName { get; set; } = "";
        [Required, StringLength(50), Display(Name = "License No.")] public string LicenseNumber { get; set; } = "";
        public string? Department { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [ForeignKey("UserID")] public User? User { get; set; }
        [NotMapped] public string FullName => $"{FirstName} {LastName}";
    }

    // ─── Appointment ─────────────────────────────────────────
    [Table("Appointments")]
    public class Appointment
    {
        [Key] public int AppointmentID { get; set; }
        [Required] public int PatientID { get; set; }
        [Required] public int DoctorID { get; set; }
        [Required, Display(Name = "Appointment Date")] public DateTime AppointmentDate { get; set; }
        [Required, Display(Name = "Type")] public string AppointmentType { get; set; } = "InPerson";
        public string Status { get; set; } = "Scheduled";
        [Display(Name = "Reason for Visit")] public string? ReasonForVisit { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [ForeignKey("PatientID")] public Patient? Patient { get; set; }
        [ForeignKey("DoctorID")] public Doctor? Doctor { get; set; }
    }

    // ─── Consultation ────────────────────────────────────────
    [Table("Consultations")]
    public class Consultation
    {
        [Key] public int ConsultationID { get; set; }
        [Required] public int AppointmentID { get; set; }
        [Required] public int PatientID { get; set; }
        [Required] public int DoctorID { get; set; }
        [Required, Display(Name = "Consultation Type")] public string ConsultationType { get; set; } = "InPerson";
        public DateTime ConsultationDate { get; set; } = DateTime.Now;
        public string? Diagnosis { get; set; }
        [Display(Name = "Treatment Plan")] public string? TreatmentPlan { get; set; }
        [Display(Name = "Follow-up Required")] public bool FollowUpRequired { get; set; } = false;
        [DataType(DataType.Date), Display(Name = "Follow-up Date")] public DateTime? FollowUpDate { get; set; }
        [Display(Name = "Duration (mins)")] public int? DurationMinutes { get; set; }
        public string? VideoSessionURL { get; set; }

        [ForeignKey("AppointmentID")] public Appointment? Appointment { get; set; }
        [ForeignKey("PatientID")] public Patient? Patient { get; set; }
        [ForeignKey("DoctorID")] public Doctor? Doctor { get; set; }
        public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
    }

    // ─── PatientVitals ───────────────────────────────────────
    [Table("PatientVitals")]
    public class PatientVitals
    {
        [Key] public int VitalsID { get; set; }
        [Required] public int PatientID { get; set; }
        public int? NurseID { get; set; }
        public int? ConsultationID { get; set; }
        public DateTime RecordedDate { get; set; } = DateTime.Now;
        [Display(Name = "Temperature (°C)")] public decimal? Temperature { get; set; }
        [Display(Name = "BP Systolic")] public int? BloodPressureSys { get; set; }
        [Display(Name = "BP Diastolic")] public int? BloodPressureDia { get; set; }
        [Display(Name = "Heart Rate (bpm)")] public int? HeartRate { get; set; }
        [Display(Name = "Respiratory Rate")] public int? RespiratoryRate { get; set; }
        [Display(Name = "O2 Saturation (%)")] public decimal? OxygenSaturation { get; set; }
        [Display(Name = "Weight (kg)")] public decimal? Weight { get; set; }
        [Display(Name = "Height (cm)")] public decimal? Height { get; set; }
        public decimal? BMI { get; set; }
        public string? Notes { get; set; }
        public bool AIAlertTriggered { get; set; } = false;

        [ForeignKey("PatientID")] public Patient? Patient { get; set; }
        [ForeignKey("NurseID")] public Nurse? Nurse { get; set; }
        [ForeignKey("ConsultationID")] public Consultation? Consultation { get; set; }

        [NotMapped] public string BloodPressure => $"{BloodPressureSys}/{BloodPressureDia} mmHg";
    }

    // ─── Medication ──────────────────────────────────────────
    [Table("Medications")]
    public class Medication
    {
        [Key] public int MedicationID { get; set; }
        [Required, StringLength(200), Display(Name = "Medication Name")] public string MedicationName { get; set; } = "";
        [StringLength(200), Display(Name = "Generic Name")] public string? GenericName { get; set; }
        public string? Category { get; set; }
        [Display(Name = "Dosage Form")] public string DosageForm { get; set; } = "Tablet";
        public string? Strength { get; set; }
        [Display(Name = "Stock Quantity")] public int StockQuantity { get; set; } = 0;
        [Display(Name = "Reorder Level")] public int ReorderLevel { get; set; } = 10;
        [Display(Name = "Unit Price (R)")] public decimal? UnitPrice { get; set; }
        [DataType(DataType.Date), Display(Name = "Expiry Date")] public DateTime? ExpiryDate { get; set; }
        public string? Manufacturer { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        [NotMapped] public bool IsLowStock => StockQuantity <= ReorderLevel;
    }

    // ─── Prescription ────────────────────────────────────────
    [Table("Prescriptions")]
    public class Prescription
    {
        [Key] public int PrescriptionID { get; set; }
        [Required] public int ConsultationID { get; set; }
        [Required] public int PatientID { get; set; }
        [Required] public int DoctorID { get; set; }
        [Required] public int MedicationID { get; set; }
        [Required, StringLength(100)] public string Dosage { get; set; } = "";
        [Required, StringLength(100)] public string Frequency { get; set; } = "";
        [Required, Display(Name = "Duration (Days)")] public int DurationDays { get; set; }
        public string? Instructions { get; set; }
        public bool IsDispensed { get; set; } = false;
        public DateTime? DispensedDate { get; set; }
        public DateTime PrescriptionDate { get; set; } = DateTime.Now;

        [ForeignKey("ConsultationID")] public Consultation? Consultation { get; set; }
        [ForeignKey("PatientID")] public Patient? Patient { get; set; }
        [ForeignKey("DoctorID")] public Doctor? Doctor { get; set; }
        [ForeignKey("MedicationID")] public Medication? Medication { get; set; }
    }

    // ─── Admission ───────────────────────────────────────────
    [Table("Admissions")]
    public class Admission
    {
        [Key] public int AdmissionID { get; set; }
        [Required] public int PatientID { get; set; }
        [Required] public int DoctorID { get; set; }
        public DateTime AdmissionDate { get; set; } = DateTime.Now;
        public DateTime? DischargeDate { get; set; }
        public string? Ward { get; set; }
        [Display(Name = "Bed Number")] public string? BedNumber { get; set; }
        [Required, Display(Name = "Admission Reason")] public string AdmissionReason { get; set; } = "";
        [Display(Name = "Discharge Notes")] public string? DischargeNotes { get; set; }
        public string Status { get; set; } = "Admitted";

        [ForeignKey("PatientID")] public Patient? Patient { get; set; }
        [ForeignKey("DoctorID")] public Doctor? Doctor { get; set; }
    }

    // ─── Billing ─────────────────────────────────────────────
    [Table("Billing")]
    public class Billing
    {
        [Key] public int BillingID { get; set; }
        [Required] public int PatientID { get; set; }
        public int? ConsultationID { get; set; }
        public int? AdmissionID { get; set; }
        [Required, Display(Name = "Total Amount (R)")] public decimal TotalAmount { get; set; }
        [Display(Name = "Paid Amount (R)")] public decimal PaidAmount { get; set; } = 0;
        [Display(Name = "Payment Status")] public string PaymentStatus { get; set; } = "Pending";
        [Display(Name = "Payment Method")] public string PaymentMethod { get; set; } = "Cash";
        public DateTime BillingDate { get; set; } = DateTime.Now;
        public DateTime? PaidDate { get; set; }
        [Display(Name = "Invoice Number")] public string? InvoiceNumber { get; set; }
        public string? Notes { get; set; }

        [ForeignKey("PatientID")] public Patient? Patient { get; set; }
        [ForeignKey("ConsultationID")] public Consultation? Consultation { get; set; }
        [ForeignKey("AdmissionID")] public Admission? Admission { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public decimal BalanceAmount { get; private set; }
    }

    // ─── HealthReport ────────────────────────────────────────
    [Table("HealthReports")]
    public class HealthReport
    {
        [Key] public int ReportID { get; set; }
        [Required] public int PatientID { get; set; }
        [Required] public int GeneratedByUserID { get; set; }
        public DateTime ReportDate { get; set; } = DateTime.Now;
        [Required, Display(Name = "Report Type")] public string ReportType { get; set; } = "General";
        public string? ReportContent { get; set; }
        public string? FilePath { get; set; }

        [ForeignKey("PatientID")] public Patient? Patient { get; set; }
        [ForeignKey("GeneratedByUserID")] public User? GeneratedBy { get; set; }
    }

    // ─── AIAlert ─────────────────────────────────────────────
    [Table("AIAlerts")]
    public class AIAlert
    {
        [Key] public int AlertID { get; set; }
        [Required] public int PatientID { get; set; }
        public int? VitalsID { get; set; }
        [Required, Display(Name = "Alert Type")] public string AlertType { get; set; } = "";
        [Required, Display(Name = "Alert Message")] public string AlertMessage { get; set; } = "";
        [Required] public string Severity { get; set; } = "Medium";
        public DateTime AlertDate { get; set; } = DateTime.Now;
        public bool IsAcknowledged { get; set; } = false;
        public int? AcknowledgedByUserID { get; set; }
        public DateTime? AcknowledgedDate { get; set; }

        [ForeignKey("PatientID")] public Patient? Patient { get; set; }
        [ForeignKey("VitalsID")] public PatientVitals? Vitals { get; set; }
    }

    // ─── Notification ─────────────────────────────────────────
    [Table("Notifications")]
    public class Notification
    {
        [Key] public int NotificationID { get; set; }
        [Required] public int UserID { get; set; }
        [Required, StringLength(500)] public string Message { get; set; } = "";
        [StringLength(100)] public string? LinkUrl { get; set; }
        [StringLength(50)] public string Category { get; set; } = "General";
        [StringLength(20)] public string Icon { get; set; } = "bell";
        public bool IsRead { get; set; } = false;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ReadDate { get; set; }

        [ForeignKey("UserID")] public User? User { get; set; }
    }

    // ─── View Models ─────────────────────────────────────────
    public class LoginViewModel
    {
        [Required] public string Username { get; set; } = "";
        [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
        public bool RememberMe { get; set; }
        public string? ReturnUrl { get; set; }
    }

    public class DashboardViewModel
    {
        public int TotalPatients { get; set; }
        public int TodayAppointments { get; set; }
        public int ActiveAdmissions { get; set; }
        public int PendingBills { get; set; }
        public int LowStockMedications { get; set; }
        public int UnacknowledgedAlerts { get; set; }
        public int MyAppointments { get; set; }
        public int MyPrescriptions { get; set; }
        public int MyOutstandingBills { get; set; }
        public List<Appointment> UpcomingAppointments { get; set; } = new();
        public List<AIAlert> RecentAlerts { get; set; } = new();
        public string UserRole { get; set; } = "";
        public string UserName { get; set; } = "";
    }

    public class RegisterViewModel
    {
        // ── Account Info ──
        [Required, StringLength(100), Display(Name = "Username")]
        public string Username { get; set; } = "";

        [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password), Display(Name = "Password")]
        public string Password { get; set; } = "";

        [Required, DataType(DataType.Password), Compare("Password", ErrorMessage = "Passwords do not match."), Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = "";

        [Required, StringLength(150), EmailAddress, Display(Name = "Email")]
        public string Email { get; set; } = "";

        // ── Personal Info ──
        [Required, StringLength(100), Display(Name = "First Name")]
        public string FirstName { get; set; } = "";

        [Required, StringLength(100), Display(Name = "Last Name")]
        public string LastName { get; set; } = "";

        [Required, DataType(DataType.Date), Display(Name = "Date of Birth")]
        public DateTime DateOfBirth { get; set; }

        [Required, Display(Name = "Gender")]
        public string Gender { get; set; } = "";

        [StringLength(20), Display(Name = "ID Number")]
        public string? IDNumber { get; set; }

        [Required, StringLength(20), Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = "";

        [StringLength(200), Display(Name = "Address")]
        public string? Address { get; set; }

        [Display(Name = "Blood Type")]
        public string BloodType { get; set; } = "Unknown";

        [StringLength(500), Display(Name = "Allergies")]
        public string? Allergies { get; set; }

        [StringLength(100), Display(Name = "Emergency Contact Name")]
        public string? EmergencyContact { get; set; }

        [StringLength(20), Display(Name = "Emergency Contact Phone")]
        public string? EmergencyPhone { get; set; }

        // ── Medical Info ──
        [StringLength(500), Display(Name = "Medical History / Reason for Registration")]
        public string? MedicalNotes { get; set; }
    }

    public class SummaryReportViewModel
    {
        public int TotalPatients { get; set; }
        public int NewPatients { get; set; }
        public int TotalAppointments { get; set; }
        public int CompletedAppts { get; set; }
        public int TotalAdmissions { get; set; }
        public int CurrentlyAdmitted { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCollected { get; set; }
        public decimal OutstandingRevenue { get; set; }
        public int PrescriptionsIssued { get; set; }
        public int AIAlertsGenerated { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    // AI VIEW MODELS
    // ═══════════════════════════════════════════════════════════
    public class SymptomCheckerViewModel
    {
        public string? Symptoms { get; set; }
        public int? Age { get; set; }
        public string? Gender { get; set; }
        public List<SymptomResult> Results { get; set; } = new();
    }

    public class SymptomResult
    {
        public string Symptom { get; set; } = "";
        public string PossibleCondition { get; set; } = "";
        public string Recommendation { get; set; } = "";
        public string UrgencyLevel { get; set; } = "Low";
        public string RecommendedSpecialty { get; set; } = "";
        public bool IsAgeModified { get; set; }
    }

    public class MedicationAssistantViewModel
    {
        public string ActionType { get; set; } = "interaction";
        public List<int>? SelectedMedicationIds { get; set; }
        public List<string>? SelectedMedicationNames { get; set; }
        public string? MedicationName { get; set; }
        public decimal? PatientWeight { get; set; }
        public int? PatientAge { get; set; }
        public List<InteractionResult> InteractionResults { get; set; } = new();
        public DosageResult? DosageResult { get; set; }
        public MedicationInfoResult? MedicationInfo { get; set; }
    }

    public class InteractionResult
    {
        public string Medications { get; set; } = "";
        public string Severity { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class DosageResult
    {
        public string Medication { get; set; } = "";
        public string Dosage { get; set; } = "";
        public string Calculation { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    public class MedicationInfoResult
    {
        public string Name { get; set; } = "";
        public string GenericName { get; set; } = "";
        public string Category { get; set; } = "";
        public string DosageForm { get; set; } = "";
        public string Strength { get; set; } = "";
        public string StockStatus { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public decimal? UnitPrice { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string Info { get; set; } = "";
    }

    public class ImageScannerViewModel
    {
        public string ScanType { get; set; } = "prescription";
        public string? UploadedFilePath { get; set; }
        public DateTime? ScanDate { get; set; }
        public ScanResult? ScanResults { get; set; }
    }

    public class ScanResult
    {
        public string DetectedText { get; set; } = "";
        public int Confidence { get; set; }
        public Dictionary<string, string> ExtractedData { get; set; } = new();
        public string AnalysisNotes { get; set; } = "";
    }

    public class ClinicalSupportViewModel
    {
        public List<AIAlert> RecentAlerts { get; set; } = new();
        public List<Patient> Patients { get; set; } = new();
        public int UnacknowledgedCount { get; set; }
        public int TotalAlerts { get; set; }
    }
}

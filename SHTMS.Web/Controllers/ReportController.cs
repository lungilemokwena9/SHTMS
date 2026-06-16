using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SHTMS.Web.Data;
using SHTMS.Web.Models;

namespace SHTMS.Web.Controllers
{
    [Authorize]
    public class ReportController : Controller
    {
        private readonly ShtmsDbContext _db;
        public ReportController(ShtmsDbContext db) { _db = db; }

        private string CurrentRole => User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";

        private bool IsPatient => CurrentRole == "Patient";

        private async Task<int?> GetCurrentDoctorIdAsync()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return null;

            return await _db.Doctors
                .Where(d => d.UserID == userId)
                .Select(d => (int?)d.DoctorID)
                .FirstOrDefaultAsync();
        }

        private async Task<int?> GetCurrentPatientIdAsync()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
                return null;

            return await _db.Patients
                .Where(p => p.UserID == userId && p.IsActive)
                .Select(p => (int?)p.PatientID)
                .FirstOrDefaultAsync();
        }

        private bool CanAccessManagementReports() => CurrentRole == "Admin" || CurrentRole == "Doctor";

        private IActionResult? ForbidPatientAccess()
        {
            return IsPatient ? Forbid() : null;
        }

        // GET: /Report — Report menu
        public IActionResult Index()
        {
            if (!CanAccessManagementReports())
                return Forbid();

            return View();
        }

        // ── REPORT 1: Summary Dashboard (management) ──────────
        public async Task<IActionResult> Summary(DateTime? from, DateTime? to)
        {
            if (!CanAccessManagementReports()) return Forbid();
            from ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            to   ??= DateTime.Now;

            ViewBag.From = from.Value.ToString("yyyy-MM-dd");
            ViewBag.To   = to.Value.ToString("yyyy-MM-dd");

            // Role-based: doctors see only their patients' data
            int? doctorId = null;
            if (CurrentRole == "Doctor")
            {
                doctorId = await GetCurrentDoctorIdAsync();
            }

            var patientQuery = _db.Patients.Where(p => p.IsActive);
            var apptQuery = _db.Appointments.AsQueryable();
            var admQuery = _db.Admissions.AsQueryable();
            var billQuery = _db.Billings.AsQueryable();
            var rxQuery = _db.Prescriptions.AsQueryable();

            if (doctorId.HasValue)
            {
                apptQuery = apptQuery.Where(a => a.DoctorID == doctorId);
                admQuery = admQuery.Where(a => a.DoctorID == doctorId);
                rxQuery = rxQuery.Where(p => p.DoctorID == doctorId);
                billQuery = billQuery.Where(b => _db.Appointments.Any(a => a.DoctorID == doctorId && a.PatientID == b.PatientID));
            }

            var vm = new SummaryReportViewModel
            {
                From = from.Value, To = to.Value,
                TotalPatients      = await patientQuery.CountAsync(),
                NewPatients        = await patientQuery.CountAsync(p => p.RegistrationDate >= from && p.RegistrationDate <= to),
                TotalAppointments  = await apptQuery.CountAsync(a => a.AppointmentDate >= from && a.AppointmentDate <= to),
                CompletedAppts     = await apptQuery.CountAsync(a => a.AppointmentDate >= from && a.AppointmentDate <= to && a.Status == "Completed"),
                TotalAdmissions    = await admQuery.CountAsync(a => a.AdmissionDate >= from && a.AdmissionDate <= to),
                CurrentlyAdmitted  = await admQuery.CountAsync(a => a.Status == "Admitted"),
                TotalRevenue       = await billQuery.Where(b => b.BillingDate >= from && b.BillingDate <= to).SumAsync(b => (decimal?)b.TotalAmount) ?? 0,
                TotalCollected     = await billQuery.Where(b => b.BillingDate >= from && b.BillingDate <= to).SumAsync(b => (decimal?)b.PaidAmount) ?? 0,
                PrescriptionsIssued= await rxQuery.CountAsync(p => p.PrescriptionDate >= from && p.PrescriptionDate <= to),
                AIAlertsGenerated  = await _db.AIAlerts.CountAsync(a => a.AlertDate >= from && a.AlertDate <= to),
            };
            return View(vm);
        }

        // ── REPORT 2: Patient Demographics Report ─────────────
        public async Task<IActionResult> Patients(string? gender, string? bloodType, DateTime? from, DateTime? to)
        {
            if (!CanAccessManagementReports()) return Forbid();
            from ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            to   ??= DateTime.Now;

            var query = _db.Patients
                .Include(p => p.Appointments)
                .Where(p => p.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(gender)) query = query.Where(p => p.Gender == gender);
            if (!string.IsNullOrWhiteSpace(bloodType)) query = query.Where(p => p.BloodType == bloodType);
            query = query.Where(p => p.RegistrationDate >= from && p.RegistrationDate <= to);

            ViewBag.From       = from.Value.ToString("yyyy-MM-dd");
            ViewBag.To         = to.Value.ToString("yyyy-MM-dd");
            ViewBag.GenderFilter = gender;
            ViewBag.BloodFilter  = bloodType;
            ViewBag.GenderList   = new SelectList(new[] { "Male", "Female", "Other" });
            ViewBag.BloodList    = new SelectList(new[] { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-", "Unknown" });
            ViewBag.TotalPatients    = await _db.Patients.CountAsync(p => p.IsActive);
            ViewBag.NewRegistrations = await _db.Patients.CountAsync(p => p.RegistrationDate >= from && p.RegistrationDate <= to);

            return View(await query.OrderBy(p => p.LastName).ToListAsync());
        }

        // ── REPORT 3: Appointment Report ──────────────────────
        public async Task<IActionResult> Appointments(DateTime? from, DateTime? to, string? status, int? doctorId)
        {
            if (!CanAccessManagementReports()) return Forbid();
            from ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            to   ??= DateTime.Now;

            var query = _db.Appointments
                .Include(a => a.Patient).Include(a => a.Doctor)
                .Where(a => a.AppointmentDate >= from && a.AppointmentDate <= to)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);
            if (doctorId.HasValue) query = query.Where(a => a.DoctorID == doctorId);

            // Role-based filter: doctors only see their own appointments
            if (CurrentRole == "Doctor")
            {
                var currentDoctorId = await GetCurrentDoctorIdAsync();
                if (currentDoctorId.HasValue) query = query.Where(a => a.DoctorID == currentDoctorId);
            }

            ViewBag.From       = from.Value.ToString("yyyy-MM-dd");
            ViewBag.To         = to.Value.ToString("yyyy-MM-dd");
            ViewBag.StatusFilter = status;
            ViewBag.DoctorFilter = doctorId;
            ViewBag.StatusList = new SelectList(new[] { "Scheduled", "Confirmed", "Completed", "Cancelled", "NoShow" });
            ViewBag.DoctorList = new SelectList(
                await _db.Doctors.OrderBy(d => d.LastName).Select(d => new { d.DoctorID, Name = "Dr. " + d.FirstName + " " + d.LastName }).ToListAsync(),
                "DoctorID", "Name", doctorId);

            return View(await query.OrderBy(a => a.AppointmentDate).ToListAsync());
        }

        // ── REPORT 4: Admission Report ────────────────────────
        public async Task<IActionResult> Admissions(DateTime? from, DateTime? to, string? status, string? ward)
        {
            if (!CanAccessManagementReports()) return Forbid();
            from ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            to   ??= DateTime.Now;

            var query = _db.Admissions
                .Include(a => a.Patient).Include(a => a.Doctor)
                .Where(a => a.AdmissionDate >= from && a.AdmissionDate <= to)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);
            if (!string.IsNullOrWhiteSpace(ward)) query = query.Where(a => a.Ward == ward);

            // Role-based: doctors see only their admissions
            if (CurrentRole == "Doctor")
            {
                var currentDoctorId = await GetCurrentDoctorIdAsync();
                if (currentDoctorId.HasValue) query = query.Where(a => a.DoctorID == currentDoctorId);
            }

            ViewBag.From        = from.Value.ToString("yyyy-MM-dd");
            ViewBag.To          = to.Value.ToString("yyyy-MM-dd");
            ViewBag.StatusFilter = status;
            ViewBag.WardFilter   = ward;
            ViewBag.StatusList   = new SelectList(new[] { "Admitted", "Discharged", "Transferred" });
            ViewBag.WardList     = new SelectList(new[] { "General Ward", "Cardiology Ward", "Paediatric Ward", "ICU", "Emergency Ward", "Gynaecology Ward" });
            ViewBag.TotalAdmissions  = await _db.Admissions.CountAsync(a => a.AdmissionDate >= from && a.AdmissionDate <= to);
            ViewBag.CurrentlyAdmitted = await _db.Admissions.CountAsync(a => a.Status == "Admitted");

            return View(await query.OrderByDescending(a => a.AdmissionDate).ToListAsync());
        }

        // ── REPORT 5: Prescription Report ─────────────────────
        public async Task<IActionResult> Prescriptions(DateTime? from, DateTime? to, int? doctorId, bool? dispensedOnly)
        {
            if (!CanAccessManagementReports()) return Forbid();
            from ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            to   ??= DateTime.Now;

            var query = _db.Prescriptions
                .Include(p => p.Patient)
                .Include(p => p.Doctor)
                .Include(p => p.Medication)
                .Where(p => p.PrescriptionDate >= from && p.PrescriptionDate <= to)
                .AsQueryable();

            if (doctorId.HasValue) query = query.Where(p => p.DoctorID == doctorId);
            if (dispensedOnly == true) query = query.Where(p => p.IsDispensed);

            // Role-based: doctors see only their prescriptions
            if (CurrentRole == "Doctor")
            {
                var currentDoctorId = await GetCurrentDoctorIdAsync();
                if (currentDoctorId.HasValue) query = query.Where(p => p.DoctorID == currentDoctorId);
            }

            ViewBag.From          = from.Value.ToString("yyyy-MM-dd");
            ViewBag.To            = to.Value.ToString("yyyy-MM-dd");
            ViewBag.DoctorFilter  = doctorId;
            ViewBag.DispensedOnly = dispensedOnly;
            ViewBag.DoctorList    = new SelectList(
                await _db.Doctors.OrderBy(d => d.LastName).Select(d => new { d.DoctorID, Name = "Dr. " + d.FirstName + " " + d.LastName }).ToListAsync(),
                "DoctorID", "Name", doctorId);
            ViewBag.TotalPrescriptions = await _db.Prescriptions.CountAsync(p => p.PrescriptionDate >= from && p.PrescriptionDate <= to);
            ViewBag.TotalDispensed     = await _db.Prescriptions.CountAsync(p => p.PrescriptionDate >= from && p.PrescriptionDate <= to && p.IsDispensed);

            return View(await query.OrderByDescending(p => p.PrescriptionDate).ToListAsync());
        }

        // ── REPORT 6: Billing / Revenue Report ───────────────
        public async Task<IActionResult> Revenue(DateTime? from, DateTime? to, string? paymentStatus)
        {
            if (!CanAccessManagementReports()) return Forbid();
            from ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            to   ??= DateTime.Now;

            var query = _db.Billings
                .Include(b => b.Patient)
                .Where(b => b.BillingDate >= from && b.BillingDate <= to)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(paymentStatus)) query = query.Where(b => b.PaymentStatus == paymentStatus);

            // Role-based: doctors see only bills for their patients
            if (CurrentRole == "Doctor")
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (doctorId.HasValue)
                {
                    var doctorPatientIds = await _db.Appointments
                        .Where(a => a.DoctorID == doctorId.Value)
                        .Select(a => a.PatientID)
                        .Distinct()
                        .ToListAsync();
                    query = query.Where(b => doctorPatientIds.Contains(b.PatientID));
                }
            }

            var bills = await query.OrderByDescending(b => b.BillingDate).ToListAsync();

            ViewBag.From          = from.Value.ToString("yyyy-MM-dd");
            ViewBag.To            = to.Value.ToString("yyyy-MM-dd");
            ViewBag.StatusFilter  = paymentStatus;
            ViewBag.StatusList    = new SelectList(new[] { "Pending", "PartiallyPaid", "Paid", "Waived" });
            ViewBag.TotalRevenue  = bills.Sum(b => b.TotalAmount);
            ViewBag.TotalCollected= bills.Sum(b => b.PaidAmount);
            ViewBag.TotalOutstanding = bills.Sum(b => b.BalanceAmount);

            return View(bills);
        }

        // ── REPORT 7: Medication Inventory Report (multi-table) ──
        public async Task<IActionResult> Inventory(bool? lowStock, string? category)
        {
            if (!CanAccessManagementReports()) return Forbid();
            var query = _db.Medications
                .Where(m => m.IsActive)
                .AsQueryable();

            if (lowStock == true) query = query.Where(m => m.StockQuantity <= m.ReorderLevel);
            if (!string.IsNullOrWhiteSpace(category)) query = query.Where(m => m.Category == category);

            // Role-based: doctors see only medications they've prescribed
            if (CurrentRole == "Doctor")
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (doctorId.HasValue)
                {
                    var doctorMedIds = await _db.Prescriptions
                        .Where(p => p.DoctorID == doctorId.Value)
                        .Select(p => p.MedicationID)
                        .Distinct()
                        .ToListAsync();
                    query = query.Where(m => doctorMedIds.Contains(m.MedicationID));
                }
            }

            var medications = await query.OrderBy(m => m.MedicationName).ToListAsync();

            // Get prescription counts per medication (multi-table join)
            var medIds = medications.Select(m => m.MedicationID).ToList();
            var prescriptionCounts = await _db.Prescriptions
                .Where(p => medIds.Contains(p.MedicationID))
                .GroupBy(p => p.MedicationID)
                .Select(g => new { MedicationID = g.Key, Count = g.Count(), LastPrescribed = g.Max(p => p.PrescriptionDate) })
                .ToListAsync();

            ViewBag.LowStock     = lowStock;
            ViewBag.CategoryFilter = category;
            ViewBag.CategoryList = new SelectList(new[] { "Analgesic", "Antibiotic", "Anti-inflammatory", "Beta-blocker", "Bronchodilator", "Antidiabetic", "Antihypertensive", "Antifungal", "Antiviral", "Vitamin", "Other" });
            ViewBag.TotalItems    = await _db.Medications.CountAsync(m => m.IsActive);
            ViewBag.LowStockCount = await _db.Medications.CountAsync(m => m.IsActive && m.StockQuantity <= m.ReorderLevel);
            ViewBag.PrescriptionCounts = prescriptionCounts.ToDictionary(pc => pc.MedicationID, pc => (pc.Count, pc.LastPrescribed));

            return View(medications);
        }

        // ═══════════════════════════════════════════════════════
        // CSV EXPORTS
        // ═══════════════════════════════════════════════════════

        public async Task<IActionResult> ExportBillingCsv(DateTime? from, DateTime? to, string? paymentStatus)
        {
            if (!CanAccessManagementReports()) return Forbid();
            from ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            to   ??= DateTime.Now;

            var bills = await _db.Billings
                .Include(b => b.Patient)
                .Where(b => b.BillingDate >= from && b.BillingDate <= to)
                .OrderByDescending(b => b.BillingDate)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(paymentStatus))
                bills = bills.Where(b => b.PaymentStatus == paymentStatus).ToList();

            if (CurrentRole == "Doctor")
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (doctorId.HasValue)
                {
                    var doctorPatientIds = await _db.Appointments
                        .Where(a => a.DoctorID == doctorId.Value)
                        .Select(a => a.PatientID)
                        .Distinct()
                        .ToListAsync();
                    bills = bills.Where(b => doctorPatientIds.Contains(b.PatientID)).ToList();
                }
            }

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Invoice Number,Patient,Total Amount,Paid Amount,Balance,Status,Method,Date");
            foreach (var b in bills)
            {
                csv.AppendLine($"{b.InvoiceNumber},{b.Patient?.FullName},{b.TotalAmount:F2},{b.PaidAmount:F2},{b.BalanceAmount:F2},{b.PaymentStatus},{b.PaymentMethod},{b.BillingDate:yyyy-MM-dd}");
            }

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"SHTMS_Billing_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
        }

        public async Task<IActionResult> ExportAppointmentsCsv(DateTime? from, DateTime? to, string? status, int? doctorId)
        {
            if (!CanAccessManagementReports()) return Forbid();
            from ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            to   ??= DateTime.Now;

            var appts = await _db.Appointments
                .Include(a => a.Patient).Include(a => a.Doctor)
                .Where(a => a.AppointmentDate >= from && a.AppointmentDate <= to)
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(status))
                appts = appts.Where(a => a.Status == status).ToList();

            if (doctorId.HasValue)
                appts = appts.Where(a => a.DoctorID == doctorId).ToList();

            if (CurrentRole == "Doctor")
            {
                var currentDoctorId = await GetCurrentDoctorIdAsync();
                if (currentDoctorId.HasValue)
                    appts = appts.Where(a => a.DoctorID == currentDoctorId.Value).ToList();
            }

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("ID,Patient,Doctor,Date,Type,Status,Reason");
            foreach (var a in appts)
            {
                csv.AppendLine($"{a.AppointmentID},{a.Patient?.FullName},{a.Doctor?.FullName},{a.AppointmentDate:yyyy-MM-dd HH:mm},{a.AppointmentType},{a.Status},{a.ReasonForVisit?.Replace(",", ";")}");
            }

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"SHTMS_Appointments_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
        }

        public async Task<IActionResult> ExportInventoryCsv(bool? lowStock)
        {
            if (!CanAccessManagementReports()) return Forbid();
            var query = _db.Medications.Where(m => m.IsActive).AsQueryable();
            if (lowStock == true) query = query.Where(m => m.StockQuantity <= m.ReorderLevel);

            var meds = await query.OrderBy(m => m.MedicationName).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Medication Name,Generic Name,Category,Dosage Form,Strength,Stock Quantity,Reorder Level,Unit Price,Expiry Date,Manufacturer");
            foreach (var m in meds)
            {
                csv.AppendLine($"{m.MedicationName},{m.GenericName},{m.Category},{m.DosageForm},{m.Strength},{m.StockQuantity},{m.ReorderLevel},{m.UnitPrice:F2},{m.ExpiryDate:yyyy-MM-dd},{m.Manufacturer}");
            }

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"SHTMS_Inventory_{DateTime.Now:yyyyMMdd}.csv");
        }

        public async Task<IActionResult> ExportSummaryCsv(DateTime? from, DateTime? to)
        {
            if (!CanAccessManagementReports()) return Forbid();
            from ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            to   ??= DateTime.Now;

            int? doctorId = null;
            if (CurrentRole == "Doctor")
                doctorId = await GetCurrentDoctorIdAsync();

            var patientQuery = _db.Patients.Where(p => p.IsActive);
            var apptQuery = _db.Appointments.AsQueryable();
            var admQuery = _db.Admissions.AsQueryable();
            var billQuery = _db.Billings.AsQueryable();
            var rxQuery = _db.Prescriptions.AsQueryable();

            if (doctorId.HasValue)
            {
                apptQuery = apptQuery.Where(a => a.DoctorID == doctorId);
                admQuery = admQuery.Where(a => a.DoctorID == doctorId);
                rxQuery = rxQuery.Where(p => p.DoctorID == doctorId);
                billQuery = billQuery.Where(b => _db.Appointments.Any(a => a.DoctorID == doctorId && a.PatientID == b.PatientID));
            }

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Metric,Value");
            csv.AppendLine($"Total Active Patients,{await patientQuery.CountAsync()}");
            csv.AppendLine($"New Patients ({from:yyyy-MM-dd} to {to:yyyy-MM-dd}),{await patientQuery.CountAsync(p => p.RegistrationDate >= from && p.RegistrationDate <= to)}");
            csv.AppendLine($"Total Appointments,{await apptQuery.CountAsync(a => a.AppointmentDate >= from && a.AppointmentDate <= to)}");
            csv.AppendLine($"Completed Appointments,{await apptQuery.CountAsync(a => a.AppointmentDate >= from && a.AppointmentDate <= to && a.Status == "Completed")}");
            csv.AppendLine($"Total Admissions,{await admQuery.CountAsync(a => a.AdmissionDate >= from && a.AdmissionDate <= to)}");
            csv.AppendLine($"Currently Admitted,{await admQuery.CountAsync(a => a.Status == "Admitted")}");
            csv.AppendLine($"Total Revenue (R),{await billQuery.Where(b => b.BillingDate >= from && b.BillingDate <= to).SumAsync(b => (decimal?)b.TotalAmount) ?? 0:F2}");
            csv.AppendLine($"Total Collected (R),{await billQuery.Where(b => b.BillingDate >= from && b.BillingDate <= to).SumAsync(b => (decimal?)b.PaidAmount) ?? 0:F2}");
            csv.AppendLine($"Prescriptions Issued,{await rxQuery.CountAsync(p => p.PrescriptionDate >= from && p.PrescriptionDate <= to)}");
            csv.AppendLine($"AI Alerts Generated,{await _db.AIAlerts.CountAsync(a => a.AlertDate >= from && a.AlertDate <= to)}");

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"SHTMS_Summary_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
        }

        public async Task<IActionResult> ExportPatientsCsv(string? gender, string? bloodType, DateTime? from, DateTime? to)
        {
            if (!CanAccessManagementReports()) return Forbid();
            from ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            to   ??= DateTime.Now;
            var query = _db.Patients.Where(p => p.IsActive).AsQueryable();
            if (!string.IsNullOrWhiteSpace(gender)) query = query.Where(p => p.Gender == gender);
            if (!string.IsNullOrWhiteSpace(bloodType)) query = query.Where(p => p.BloodType == bloodType);
            query = query.Where(p => p.RegistrationDate >= from && p.RegistrationDate <= to);

            var patients = await query.OrderBy(p => p.LastName).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("First Name,Last Name,Date of Birth,Age,Gender,Blood Type,ID Number,Phone,Email,Address,Allergies,Emergency Contact,Emergency Phone,Registration Date");
            foreach (var p in patients)
            {
                csv.AppendLine($"{p.FirstName},{p.LastName},{p.DateOfBirth:yyyy-MM-dd},{p.Age},{p.Gender},{p.BloodType},{p.IDNumber},{p.PhoneNumber},{p.Email},{p.Address?.Replace(",", ";")},{p.Allergies?.Replace(",", ";")},{p.EmergencyContact},{p.EmergencyPhone},{p.RegistrationDate:yyyy-MM-dd}");
            }

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"SHTMS_Patients_{DateTime.Now:yyyyMMdd}.csv");
        }

        public async Task<IActionResult> ExportAdmissionsCsv(DateTime? from, DateTime? to, string? status, string? ward)
        {
            if (!CanAccessManagementReports()) return Forbid();
            from ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            to   ??= DateTime.Now;

            var admissions = await _db.Admissions
                .Include(a => a.Patient).Include(a => a.Doctor)
                .Where(a => a.AdmissionDate >= from && a.AdmissionDate <= to)
                .OrderByDescending(a => a.AdmissionDate)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(status))
                admissions = admissions.Where(a => a.Status == status).ToList();

            if (!string.IsNullOrWhiteSpace(ward))
                admissions = admissions.Where(a => a.Ward == ward).ToList();

            if (CurrentRole == "Doctor")
            {
                var currentDoctorId = await GetCurrentDoctorIdAsync();
                if (currentDoctorId.HasValue)
                    admissions = admissions.Where(a => a.DoctorID == currentDoctorId.Value).ToList();
            }

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("ID,Patient,Doctor,Ward,Bed,Admission Date,Discharge Date,Reason,Status,Discharge Notes");
            foreach (var a in admissions)
            {
                csv.AppendLine($"{a.AdmissionID},{a.Patient?.FullName},{a.Doctor?.FullName},{a.Ward},{a.BedNumber},{a.AdmissionDate:yyyy-MM-dd},{a.DischargeDate:yyyy-MM-dd},{a.AdmissionReason?.Replace(",", ";")},{a.Status},{a.DischargeNotes?.Replace(",", ";")}");
            }

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"SHTMS_Admissions_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
        }

        public async Task<IActionResult> ExportPrescriptionsCsv(DateTime? from, DateTime? to, int? doctorId, bool? dispensedOnly)
        {
            if (!CanAccessManagementReports()) return Forbid();
            from ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            to   ??= DateTime.Now;

            var prescriptions = await _db.Prescriptions
                .Include(p => p.Patient).Include(p => p.Doctor).Include(p => p.Medication)
                .Where(p => p.PrescriptionDate >= from && p.PrescriptionDate <= to)
                .OrderByDescending(p => p.PrescriptionDate)
                .ToListAsync();

            if (doctorId.HasValue)
                prescriptions = prescriptions.Where(p => p.DoctorID == doctorId).ToList();

            if (dispensedOnly == true)
                prescriptions = prescriptions.Where(p => p.IsDispensed).ToList();

            if (CurrentRole == "Doctor")
            {
                var currentDoctorId = await GetCurrentDoctorIdAsync();
                if (currentDoctorId.HasValue)
                    prescriptions = prescriptions.Where(p => p.DoctorID == currentDoctorId.Value).ToList();
            }

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("ID,Patient,Doctor,Medication,Dosage,Frequency,Duration (Days),Instructions,Dispensed,Dispensed Date,Prescription Date");
            foreach (var p in prescriptions)
            {
                csv.AppendLine($"{p.PrescriptionID},{p.Patient?.FullName},{p.Doctor?.FullName},{p.Medication?.MedicationName},{p.Dosage},{p.Frequency},{p.DurationDays},{p.Instructions?.Replace(",", ";")},{p.IsDispensed},{p.DispensedDate:yyyy-MM-dd},{p.PrescriptionDate:yyyy-MM-dd}");
            }

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"SHTMS_Prescriptions_{from:yyyyMMdd}_{to:yyyyMMdd}.csv");
        }
    }

    // ── Summary Report ViewModel ──────────────────────────────
    public class SummaryReportViewModel
    {
        public DateTime From { get; set; }
        public DateTime To   { get; set; }
        public int     TotalPatients       { get; set; }
        public int     NewPatients         { get; set; }
        public int     TotalAppointments   { get; set; }
        public int     CompletedAppts      { get; set; }
        public int     TotalAdmissions     { get; set; }
        public int     CurrentlyAdmitted   { get; set; }
        public decimal TotalRevenue        { get; set; }
        public decimal TotalCollected      { get; set; }
        public int     PrescriptionsIssued { get; set; }
        public int     AIAlertsGenerated   { get; set; }
        public decimal OutstandingRevenue  => TotalRevenue - TotalCollected;
    }
}

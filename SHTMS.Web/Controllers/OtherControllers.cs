using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SHTMS.Web.Data;
using SHTMS.Web.Models;
using System.Security.Claims;

// ═══════════════════════════════════════════════════════════════
// ADMISSION CONTROLLER
// ═══════════════════════════════════════════════════════════════
namespace SHTMS.Web.Controllers
{
    [Authorize]
    public class AdmissionController : Controller
    {
        private readonly ShtmsDbContext _db;
        public AdmissionController(ShtmsDbContext db) { _db = db; }

        private string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? "";

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        private async Task<int?> GetCurrentPatientIdAsync()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return null;

            return await _db.Patients
                .Where(p => p.UserID == userId.Value && p.IsActive)
                .Select(p => (int?)p.PatientID)
                .FirstOrDefaultAsync();
        }

        private async Task<int?> GetCurrentDoctorIdAsync()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return null;

            return await _db.Doctors
                .Where(d => d.UserID == userId.Value)
                .Select(d => (int?)d.DoctorID)
                .FirstOrDefaultAsync();
        }

        private async Task<bool> CanAccessAdmissionAsync(Admission admission)
        {
            if (CurrentRole == "Admin")
                return true;

            if (CurrentRole == "Nurse")
                return true;

            if (CurrentRole == "Patient")
            {
                var patientId = await GetCurrentPatientIdAsync();
                return patientId.HasValue && patientId.Value == admission.PatientID;
            }

            if (CurrentRole == "Doctor")
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                return doctorId.HasValue && doctorId.Value == admission.DoctorID;
            }

            return false;
        }

        private static string NormalizeAdmissionStatus(string? value)
        {
            return value switch
            {
                "Discharged" => "Discharged",
                "Transferred" => "Transferred",
                _ => "Admitted"
            };
        }

        public async Task<IActionResult> Index(string? status)
        {
            var query = _db.Admissions.Include(a => a.Patient).Include(a => a.Doctor).AsQueryable();

            // ── ROLE-BASED FILTERING ──
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (role == "Patient" && int.TryParse(userIdClaim, out int patientUserId))
            {
                var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserID == patientUserId);
                if (patient != null)
                    query = query.Where(a => a.PatientID == patient.PatientID);
                else
                    query = query.Where(a => false);
            }
            else if (role == "Doctor" && int.TryParse(userIdClaim, out int doctorUserId))
            {
                var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserID == doctorUserId);
                if (doctor != null)
                    query = query.Where(a => a.DoctorID == doctor.DoctorID);
            }

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(a => a.Status == status);

            ViewBag.StatusFilter = status;
            ViewBag.StatusList   = new List<string> { "Admitted", "Discharged", "Transferred" };
            return View(await query.OrderByDescending(a => a.AdmissionDate).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var admission = await _db.Admissions
                .Include(a => a.Patient).Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.AdmissionID == id);
            if (admission == null) return NotFound();
            if (!await CanAccessAdmissionAsync(admission)) return Forbid();
            return View(admission);
        }

        [Authorize(Roles = "Admin,Doctor,Nurse")]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Doctor,Nurse")]
        public async Task<IActionResult> Create(Admission model)
        {
            ModelState.Remove("Patient"); ModelState.Remove("Doctor");
            model.Status = NormalizeAdmissionStatus(model.Status);

            if (CurrentRole == "Doctor")
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return Forbid();

                model.DoctorID = doctorId.Value;
            }

            if (CurrentRole == "Patient")
                return Forbid();

            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

            _db.Admissions.Add(model);
            await _db.SaveChangesAsync();

            // ── Notify patient ──
            var admPatient = await _db.Patients.FindAsync(model.PatientID);
            if (admPatient != null && admPatient.UserID.HasValue)
                await NotificationController.CreateNotification(_db, admPatient.UserID.Value,
                    $"You have been admitted to {model.Ward} (Bed {model.BedNumber})",
                    $"/Admission/Details/{model.AdmissionID}", "Admission", "bed");

            TempData["Success"] = "Patient admitted successfully.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,Doctor")]
        public async Task<IActionResult> Edit(int id)
        {
            var a = await _db.Admissions.FindAsync(id);
            if (a == null) return NotFound();
            if (!await CanAccessAdmissionAsync(a)) return Forbid();
            if (a.Status == "Discharged")
            {
                TempData["Error"] = "Discharged admissions cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }
            await PopulateDropdowns(a.PatientID, a.DoctorID);
            return View(a);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Doctor")]
        public async Task<IActionResult> Edit(int id, Admission model)
        {
            if (id != model.AdmissionID) return BadRequest();
            ModelState.Remove("Patient"); ModelState.Remove("Doctor");
            model.Status = NormalizeAdmissionStatus(model.Status);

            var existing = await _db.Admissions.FirstOrDefaultAsync(a => a.AdmissionID == id);
            if (existing == null) return NotFound();
            if (!await CanAccessAdmissionAsync(existing)) return Forbid();
            if (existing.Status == "Discharged")
            {
                TempData["Error"] = "Discharged admissions cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (CurrentRole == "Doctor")
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return Forbid();

                model.DoctorID = doctorId.Value;
            }

            if (!ModelState.IsValid) { await PopulateDropdowns(model.PatientID, model.DoctorID); return View(model); }

            existing.PatientID = model.PatientID;
            existing.DoctorID = model.DoctorID;
            existing.Ward = model.Ward;
            existing.BedNumber = model.BedNumber;
            existing.AdmissionReason = model.AdmissionReason;
            existing.DischargeNotes = model.DischargeNotes;
            existing.Status = model.Status;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Admission record updated.";
            return RedirectToAction(nameof(Index));
        }

        // Discharge action
        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Doctor")]
        public async Task<IActionResult> Discharge(int id, string dischargeNotes)
        {
            var admission = await _db.Admissions.FindAsync(id);
            if (admission == null) return NotFound();
            if (!await CanAccessAdmissionAsync(admission)) return Forbid();

            if (admission.Status == "Discharged")
            {
                TempData["Error"] = "Admission was already discharged.";
                return RedirectToAction(nameof(Details), new { id });
            }

            admission.Status        = "Discharged";
            admission.DischargeDate  = DateTime.Now;
            admission.DischargeNotes = dischargeNotes;
            await _db.SaveChangesAsync();

            var existingBill = await _db.Billings.FirstOrDefaultAsync(b => b.AdmissionID == admission.AdmissionID);
            if (existingBill == null)
            {
                _db.Billings.Add(new Billing
                {
                    PatientID = admission.PatientID,
                    AdmissionID = admission.AdmissionID,
                    TotalAmount = 0,
                    PaidAmount = 0,
                    PaymentStatus = "Pending",
                    PaymentMethod = "Cash",
                    BillingDate = DateTime.Now,
                    Notes = $"Auto-generated on discharge for admission #{admission.AdmissionID}"
                });
                await _db.SaveChangesAsync();
            }

            // ── Notify patient ──
            var disPatient = await _db.Patients.FindAsync(admission.PatientID);
            if (disPatient != null && disPatient.UserID.HasValue)
                await NotificationController.CreateNotification(_db, disPatient.UserID.Value,
                    "You have been discharged. Please follow up with your doctor if needed.",
                    $"/Admission/Details/{admission.AdmissionID}", "Admission", "check-circle");

            TempData["Success"] = "Patient discharged.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var a = await _db.Admissions.Include(x => x.Patient).Include(x => x.Doctor)
                .FirstOrDefaultAsync(x => x.AdmissionID == id);
            if (a == null) return NotFound();
            return View(a);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var a = await _db.Admissions.FindAsync(id);
            if (a != null) { _db.Admissions.Remove(a); await _db.SaveChangesAsync(); }
            TempData["Success"] = "Admission record deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdowns(int? selPatient = null, int? selDoctor = null)
        {
            ViewBag.PatientList = new SelectList(
                await _db.Patients.Where(p => p.IsActive).OrderBy(p => p.LastName)
                    .Select(p => new { p.PatientID, Name = p.FirstName + " " + p.LastName }).ToListAsync(),
                "PatientID", "Name", selPatient);
            ViewBag.DoctorList = new SelectList(
                await _db.Doctors.OrderBy(d => d.LastName)
                    .Select(d => new { d.DoctorID, Name = "Dr. " + d.FirstName + " " + d.LastName }).ToListAsync(),
                "DoctorID", "Name", selDoctor);
            ViewBag.WardList = new SelectList(new[] { "General Ward", "Cardiology Ward", "Paediatric Ward", "ICU", "Emergency Ward", "Gynaecology Ward" });
            ViewBag.StatusList = new SelectList(new[] { "Admitted", "Discharged", "Transferred" });
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// MEDICATION CONTROLLER
// ═══════════════════════════════════════════════════════════════
namespace SHTMS.Web.Controllers
{
    [Authorize]
    public class MedicationController : Controller
    {
        private readonly ShtmsDbContext _db;
        public MedicationController(ShtmsDbContext db) { _db = db; }

        public async Task<IActionResult> Index(string? search, bool? lowStock)
        {
            var query = _db.Medications.Where(m => m.IsActive).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(m => m.MedicationName.Contains(search) || (m.GenericName != null && m.GenericName.Contains(search)));
            if (lowStock == true)
                query = query.Where(m => m.StockQuantity <= m.ReorderLevel);

            ViewBag.Search   = search;
            ViewBag.LowStock = lowStock;
            return View(await query.OrderBy(m => m.MedicationName).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var med = await _db.Medications.FindAsync(id);
            if (med == null) return NotFound();
            return View(med);
        }

        [Authorize(Roles = "Admin,Pharmacist")]
        public IActionResult Create() { PopulateDropdowns(); return View(); }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Pharmacist")]
        public async Task<IActionResult> Create(Medication model)
        {
            if (!ModelState.IsValid) { PopulateDropdowns(); return View(model); }
            _db.Medications.Add(model);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Medication added to inventory.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,Pharmacist")]
        public async Task<IActionResult> Edit(int id)
        {
            var med = await _db.Medications.FindAsync(id);
            if (med == null) return NotFound();
            PopulateDropdowns();
            return View(med);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Pharmacist")]
        public async Task<IActionResult> Edit(int id, Medication model)
        {
            if (id != model.MedicationID) return BadRequest();
            if (!ModelState.IsValid) { PopulateDropdowns(); return View(model); }
            model.LastUpdated = DateTime.Now;
            _db.Update(model);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Medication updated.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var med = await _db.Medications.FindAsync(id);
            if (med == null) return NotFound();
            return View(med);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var med = await _db.Medications.FindAsync(id);
            if (med != null) { med.IsActive = false; await _db.SaveChangesAsync(); }
            TempData["Success"] = "Medication deactivated.";
            return RedirectToAction(nameof(Index));
        }

        private void PopulateDropdowns()
        {
            ViewBag.DosageFormList = new SelectList(new[] { "Tablet", "Capsule", "Syrup", "Injection", "Cream", "Drops", "Inhaler", "Other" });
            ViewBag.CategoryList   = new SelectList(new[] { "Analgesic", "Antibiotic", "Anti-inflammatory", "Beta-blocker", "Bronchodilator", "Antidiabetic", "Antihypertensive", "Antifungal", "Antiviral", "Vitamin", "Other" });
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// BILLING CONTROLLER
// ═══════════════════════════════════════════════════════════════
namespace SHTMS.Web.Controllers
{
    [Authorize]
    public class BillingController : Controller
    {
        private readonly ShtmsDbContext _db;
        public BillingController(ShtmsDbContext db) { _db = db; }

        private string CurrentRole => User.FindFirstValue(ClaimTypes.Role) ?? "";

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        private async Task<int?> GetCurrentPatientIdAsync()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return null;

            return await _db.Patients
                .Where(p => p.UserID == userId.Value && p.IsActive)
                .Select(p => (int?)p.PatientID)
                .FirstOrDefaultAsync();
        }

        private async Task<int?> GetCurrentDoctorIdAsync()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return null;

            return await _db.Doctors
                .Where(d => d.UserID == userId.Value)
                .Select(d => (int?)d.DoctorID)
                .FirstOrDefaultAsync();
        }

        public async Task<IActionResult> Index(string? status)
        {
            var query = _db.Billings.Include(b => b.Patient).AsQueryable();

            // ── ROLE-BASED FILTERING ──
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (role == "Patient" && int.TryParse(userIdClaim, out int patientUserId))
            {
                var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserID == patientUserId);
                if (patient != null)
                    query = query.Where(b => b.PatientID == patient.PatientID);
                else
                    query = query.Where(b => false);
            }

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(b => b.PaymentStatus == status);

            ViewBag.StatusFilter = status;
            ViewBag.StatusList   = new List<string> { "Pending", "PartiallyPaid", "Paid", "Waived" };
            return View(await query.OrderByDescending(b => b.BillingDate).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var bill = await _db.Billings
                .Include(b => b.Patient)
                .Include(b => b.Consultation).ThenInclude(c => c!.Doctor)
                .Include(b => b.Admission)
                .FirstOrDefaultAsync(b => b.BillingID == id);
            if (bill == null) return NotFound();
            if (CurrentRole == "Admin")
            {
                return View(bill);
            }

            if (CurrentRole == "Patient")
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue || patientId.Value != bill.PatientID) return Forbid();
            }
            else if (CurrentRole == "Doctor")
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue) return Forbid();

                var ownsConsultation = bill.Consultation?.DoctorID == doctorId.Value;
                var ownsAdmission = bill.Admission?.DoctorID == doctorId.Value;
                if (!ownsConsultation && !ownsAdmission) return Forbid();
            }
            else
            {
                return Forbid();
            }
            return View(bill);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Billing model)
        {
            ModelState.Remove("Patient"); ModelState.Remove("Consultation"); ModelState.Remove("Admission");
            model.PaymentStatus = NormalizePaymentStatus(model.PaymentStatus);
            model.PaymentMethod = NormalizePaymentMethod(model.PaymentMethod);
            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

            model.InvoiceNumber = $"INV-{DateTime.Now.Year}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
            _db.Billings.Add(model);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Invoice {model.InvoiceNumber} created.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var bill = await _db.Billings.FindAsync(id);
            if (bill == null) return NotFound();
            await PopulateDropdowns(bill.PatientID);
            return View(bill);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, Billing model)
        {
            if (id != model.BillingID) return BadRequest();
            ModelState.Remove("Patient"); ModelState.Remove("Consultation"); ModelState.Remove("Admission");
            model.PaymentStatus = NormalizePaymentStatus(model.PaymentStatus);
            model.PaymentMethod = NormalizePaymentMethod(model.PaymentMethod);
            if (!ModelState.IsValid) { await PopulateDropdowns(model.PatientID); return View(model); }

            if (model.PaymentStatus == "Paid" && model.PaidDate == null)
                model.PaidDate = DateTime.Now;

            if (model.PaymentStatus != "Paid")
                model.PaidDate = null;

            _db.Update(model);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Billing record updated.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var bill = await _db.Billings.Include(b => b.Patient).FirstOrDefaultAsync(b => b.BillingID == id);
            if (bill == null) return NotFound();
            return View(bill);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bill = await _db.Billings.FindAsync(id);
            if (bill != null) { _db.Billings.Remove(bill); await _db.SaveChangesAsync(); }
            TempData["Success"] = "Billing record deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdowns(int? selPatient = null)
        {
            ViewBag.PatientList = new SelectList(
                await _db.Patients.Where(p => p.IsActive).OrderBy(p => p.LastName)
                    .Select(p => new { p.PatientID, Name = p.FirstName + " " + p.LastName }).ToListAsync(),
                "PatientID", "Name", selPatient);
            ViewBag.PaymentStatusList = new SelectList(new[] { "Pending", "PartiallyPaid", "Paid", "Waived" });
            ViewBag.PaymentMethodList = new SelectList(new[] { "Cash", "Card", "Insurance", "EFT", "Waived" });
        }

        private static string NormalizePaymentStatus(string? value)
        {
            return value switch
            {
                "Paid" => "Paid",
                "Waived" => "Waived",
                "PartiallyPaid" => "PartiallyPaid",
                "Partial" => "PartiallyPaid",
                "Overdue" => "Pending",
                _ => "Pending"
            };
        }

        private static string NormalizePaymentMethod(string? value)
        {
            return value switch
            {
                "Cash" => "Cash",
                "Card" => "Card",
                "Insurance" => "Insurance",
                "Medical Aid" => "Insurance",
                "EFT" => "EFT",
                "Waived" => "Waived",
                _ => "Cash"
            };
        }
    }
}

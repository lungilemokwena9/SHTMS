using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SHTMS.Web.Data;
using SHTMS.Web.Models;
using System.Security.Claims;

// ═══════════════════════════════════════════════════════════════
// PRESCRIPTION CONTROLLER
// ═══════════════════════════════════════════════════════════════
namespace SHTMS.Web.Controllers
{
    [Authorize]
    public class PrescriptionController : Controller
    {
        private readonly ShtmsDbContext _db;
        public PrescriptionController(ShtmsDbContext db) { _db = db; }

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

        private async Task<bool> CanAccessPrescriptionAsync(Prescription prescription)
        {
            if (CurrentRole == "Admin" || CurrentRole == "Pharmacist")
                return true;

            if (CurrentRole == "Patient")
            {
                var patientId = await GetCurrentPatientIdAsync();
                return patientId.HasValue && patientId.Value == prescription.PatientID;
            }

            if (CurrentRole == "Doctor")
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                return doctorId.HasValue && doctorId.Value == prescription.DoctorID;
            }

            return false;
        }

        private static string NormalizeFrequency(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Once daily" : value.Trim();
        }

        private async Task<bool> TryRestoreStockAsync(Prescription prescription)
        {
            var med = await _db.Medications.FirstOrDefaultAsync(m => m.MedicationID == prescription.MedicationID);
            if (med == null)
                return false;

            med.StockQuantity = Math.Max(0, med.StockQuantity + 1);
            med.LastUpdated = DateTime.Now;
            return true;
        }

        private static bool HasAvailableStock(Medication? medication)
        {
            return medication != null && medication.StockQuantity > 0;
        }

        public async Task<IActionResult> Index(string? search)
        {
            var query = _db.Prescriptions
                .Include(p => p.Patient)
                .Include(p => p.Doctor)
                .Include(p => p.Medication)
                .AsQueryable();

            // ── ROLE-BASED FILTERING ──
            var role = CurrentRole;
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (role == "Patient" && int.TryParse(userIdClaim, out int patientUserId))
            {
                var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserID == patientUserId);
                if (patient != null)
                    query = query.Where(p => p.PatientID == patient.PatientID);
                else
                    query = query.Where(p => false);
            }
            else if (role == "Doctor" && int.TryParse(userIdClaim, out int doctorUserId))
            {
                var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserID == doctorUserId);
                if (doctor != null)
                    query = query.Where(p => p.DoctorID == doctor.DoctorID);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(p =>
                    p.Patient!.FirstName.ToLower().Contains(search) ||
                    p.Patient.LastName.ToLower().Contains(search) ||
                    p.Medication!.MedicationName.ToLower().Contains(search));
            }

            ViewBag.Search = search;
            return View(await query.OrderByDescending(p => p.PrescriptionDate).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var rx = await _db.Prescriptions
                .Include(p => p.Patient)
                .Include(p => p.Doctor)
                .Include(p => p.Medication)
                .Include(p => p.Consultation)
                .FirstOrDefaultAsync(p => p.PrescriptionID == id);
            if (rx == null) return NotFound();
            if (!await CanAccessPrescriptionAsync(rx)) return Forbid();
            return View(rx);
        }

        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> Create(Prescription model)
        {
            ModelState.Remove("Patient"); ModelState.Remove("Doctor");
            ModelState.Remove("Medication"); ModelState.Remove("Consultation");

            model.Frequency = NormalizeFrequency(model.Frequency);
            model.IsDispensed = false;
            model.DispensedDate = null;

            if (CurrentRole == "Doctor")
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return Forbid();

                model.DoctorID = doctorId.Value;
            }

            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

            // Check stock
            var med = await _db.Medications.FindAsync(model.MedicationID);
            if (!HasAvailableStock(med))
            {
                ModelState.AddModelError("MedicationID", "Selected medication is out of stock.");
                await PopulateDropdowns();
                return View(model);
            }

            // Reduce stock by 1
            med.StockQuantity -= 1;
            med.LastUpdated = DateTime.Now;

            _db.Prescriptions.Add(model);
            await _db.SaveChangesAsync();

            // ── Notify patient ──
            var rxPatient = await _db.Patients.FindAsync(model.PatientID);
            if (rxPatient != null && rxPatient.UserID.HasValue)
                await NotificationController.CreateNotification(_db, rxPatient.UserID.Value,
                    $"New prescription issued: {med.MedicationName} — {model.Dosage}",
                    $"/Prescription/Details/{model.PrescriptionID}", "Prescription", "prescription");

            TempData["Success"] = "Prescription generated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var rx = await _db.Prescriptions.FindAsync(id);
            if (rx == null) return NotFound();
            if (!await CanAccessPrescriptionAsync(rx)) return Forbid();
            if (rx.IsDispensed)
            {
                TempData["Error"] = "Dispensed prescriptions cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }
            await PopulateDropdowns(rx.PatientID, rx.DoctorID, rx.MedicationID, rx.ConsultationID);
            return View(rx);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> Edit(int id, Prescription model)
        {
            if (id != model.PrescriptionID) return BadRequest();
            ModelState.Remove("Patient"); ModelState.Remove("Doctor");
            ModelState.Remove("Medication"); ModelState.Remove("Consultation");

            var existing = await _db.Prescriptions
                .Include(p => p.Medication)
                .FirstOrDefaultAsync(p => p.PrescriptionID == id);
            if (existing == null) return NotFound();
            if (!await CanAccessPrescriptionAsync(existing)) return Forbid();
            if (existing.IsDispensed)
            {
                TempData["Error"] = "Dispensed prescriptions cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            model.Frequency = NormalizeFrequency(model.Frequency);
            model.IsDispensed = existing.IsDispensed;
            model.DispensedDate = existing.DispensedDate;

            if (CurrentRole == "Doctor")
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return Forbid();

                model.DoctorID = doctorId.Value;
            }

            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

            if (existing.MedicationID != model.MedicationID)
            {
                var oldMed = await _db.Medications.FirstOrDefaultAsync(m => m.MedicationID == existing.MedicationID);
                var newMed = await _db.Medications.FirstOrDefaultAsync(m => m.MedicationID == model.MedicationID);

                if (!HasAvailableStock(newMed))
                {
                    ModelState.AddModelError("MedicationID", "Selected medication is out of stock.");
                    await PopulateDropdowns(model.PatientID, model.DoctorID, model.MedicationID, model.ConsultationID);
                    return View(model);
                }

                if (oldMed != null)
                {
                    oldMed.StockQuantity += 1;
                    oldMed.LastUpdated = DateTime.Now;
                }

                newMed.StockQuantity -= 1;
                newMed.LastUpdated = DateTime.Now;
            }

            existing.PatientID = model.PatientID;
            existing.DoctorID = model.DoctorID;
            existing.MedicationID = model.MedicationID;
            existing.ConsultationID = model.ConsultationID;
            existing.Dosage = model.Dosage;
            existing.Frequency = model.Frequency;
            existing.DurationDays = model.DurationDays;
            existing.Instructions = model.Instructions;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Prescription updated.";
            return RedirectToAction(nameof(Index));
        }

        // Mark as dispensed
        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Pharmacist,Admin")]
        public async Task<IActionResult> Dispense(int id)
        {
            var rx = await _db.Prescriptions.FindAsync(id);
            if (rx == null) return NotFound();
            if (rx.IsDispensed)
            {
                TempData["Error"] = "Prescription has already been dispensed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            rx.IsDispensed   = true;
            rx.DispensedDate = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Prescription marked as dispensed.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var rx = await _db.Prescriptions
                .Include(p => p.Patient).Include(p => p.Medication)
                .FirstOrDefaultAsync(p => p.PrescriptionID == id);
            if (rx == null) return NotFound();
            if (!await CanAccessPrescriptionAsync(rx)) return Forbid();
            if (rx.IsDispensed)
            {
                TempData["Error"] = "Dispensed prescriptions cannot be deleted.";
                return RedirectToAction(nameof(Details), new { id });
            }
            return View(rx);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var rx = await _db.Prescriptions.FindAsync(id);
            if (rx != null)
            {
                if (rx.IsDispensed)
                {
                    TempData["Error"] = "Dispensed prescriptions cannot be deleted.";
                    return RedirectToAction(nameof(Index));
                }

                await TryRestoreStockAsync(rx);
                _db.Prescriptions.Remove(rx);
                await _db.SaveChangesAsync();
            }
            TempData["Success"] = "Prescription deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdowns(int? selPat = null, int? selDoc = null, int? selMed = null, int? selConsult = null)
        {
            ViewBag.PatientList = new SelectList(
                await _db.Patients.Where(p => p.IsActive).OrderBy(p => p.LastName)
                    .Select(p => new { p.PatientID, Name = p.FirstName + " " + p.LastName }).ToListAsync(),
                "PatientID", "Name", selPat);

            ViewBag.DoctorList = new SelectList(
                await _db.Doctors.OrderBy(d => d.LastName)
                    .Select(d => new { d.DoctorID, Name = "Dr. " + d.FirstName + " " + d.LastName }).ToListAsync(),
                "DoctorID", "Name", selDoc);

            ViewBag.MedicationList = new SelectList(
                await _db.Medications.Where(m => m.IsActive && m.StockQuantity > 0).OrderBy(m => m.MedicationName)
                    .Select(m => new { m.MedicationID, Name = m.MedicationName + " " + m.Strength + " (" + m.DosageForm + ") — Stock: " + m.StockQuantity }).ToListAsync(),
                "MedicationID", "Name", selMed);

            ViewBag.ConsultationList = new SelectList(
                await _db.Consultations.OrderByDescending(c => c.ConsultationDate)
                    .Select(c => new { c.ConsultationID, Name = "Consult #" + c.ConsultationID + " — " + c.ConsultationDate.ToString("dd MMM yyyy") }).ToListAsync(),
                "ConsultationID", "Name", selConsult);

            ViewBag.FrequencyList = new SelectList(new[] {
                "Once daily", "Twice daily", "Three times daily", "Four times daily",
                "Every 4 hours", "Every 6 hours", "Every 8 hours", "Every 12 hours",
                "Once weekly", "As needed (PRN)"
            });
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// VITALS CONTROLLER
// ═══════════════════════════════════════════════════════════════
namespace SHTMS.Web.Controllers
{
    [Authorize]
    public class VitalsController : Controller
    {
        private readonly ShtmsDbContext _db;
        public VitalsController(ShtmsDbContext db) { _db = db; }

        public async Task<IActionResult> Index(int? patientId)
        {
            var query = _db.PatientVitals
                .Include(v => v.Patient)
                .Include(v => v.Nurse)
                .AsQueryable();

            // ── ROLE-BASED FILTERING ──
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (role == "Patient" && int.TryParse(userIdClaim, out int patientUserId))
            {
                var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserID == patientUserId);
                if (patient != null)
                    query = query.Where(v => v.PatientID == patient.PatientID);
                else
                    query = query.Where(v => false);
            }
            else if (patientId.HasValue)
            {
                query = query.Where(v => v.PatientID == patientId);
            }

            ViewBag.PatientFilter = patientId;
            ViewBag.PatientList   = new SelectList(
                await _db.Patients.Where(p => p.IsActive).OrderBy(p => p.LastName)
                    .Select(p => new { p.PatientID, Name = p.FirstName + " " + p.LastName }).ToListAsync(),
                "PatientID", "Name", patientId);

            return View(await query.OrderByDescending(v => v.RecordedDate).ToListAsync());
        }

        [Authorize(Roles = "Nurse,Doctor,Admin")]
        public async Task<IActionResult> Create(int? patientId)
        {
            await PopulateDropdowns(patientId);
            var model = new PatientVitals { PatientID = patientId ?? 0, RecordedDate = DateTime.Now };
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Nurse,Doctor,Admin")]
        public async Task<IActionResult> Create(PatientVitals model)
        {
            if (model == null) return BadRequest();
            ModelState.Remove("Patient"); ModelState.Remove("Nurse"); ModelState.Remove("Consultation");

            if (!ModelState.IsValid) { await PopulateDropdowns(model.PatientID); return View(model); }

            // Auto-calculate BMI if height and weight provided
            if (model.Height.HasValue && model.Weight.HasValue && model.Height.GetValueOrDefault() > 0)
            {
                decimal heightM = model.Height.Value / 100;
                model.BMI = Math.Round(model.Weight.Value / (heightM * heightM), 1);
            }

            // AI Alert check — flag critical vitals
            model.AIAlertTriggered = false;
            string? alertMsg = null;
            string alertSeverity = "Medium";
            string alertType = "EmergencyVitals";

            if (model.OxygenSaturation.HasValue && model.OxygenSaturation < 90) { alertMsg = $"Critical: O2 saturation {model.OxygenSaturation}% — below 90%"; alertSeverity = "Critical"; alertType = "LowOxygen"; model.AIAlertTriggered = true; }
            else if (model.BloodPressureSys.HasValue && model.BloodPressureSys > 180) { alertMsg = $"Critical: Blood pressure {model.BloodPressureSys}/{model.BloodPressureDia ?? 0} mmHg"; alertSeverity = "Critical"; alertType = "AbnormalBP"; model.AIAlertTriggered = true; }
            else if (model.Temperature.HasValue && model.Temperature > 40) { alertMsg = $"High fever: Temperature {model.Temperature}°C"; alertSeverity = "High"; alertType = "HighTemperature"; model.AIAlertTriggered = true; }
            else if (model.HeartRate.HasValue && (model.HeartRate > 150 || model.HeartRate < 40)) { alertMsg = $"Abnormal heart rate: {model.HeartRate} bpm"; alertSeverity = "High"; alertType = "EmergencyVitals"; model.AIAlertTriggered = true; }

            _db.PatientVitals.Add(model);
            await _db.SaveChangesAsync();

            // Create AI alert if triggered
            if (model.AIAlertTriggered && alertMsg != null)
            {
                _db.AIAlerts.Add(new AIAlert {
                    PatientID = model.PatientID, VitalsID = model.VitalsID,
                    AlertType = alertType, AlertMessage = alertMsg, Severity = alertSeverity
                });
                await _db.SaveChangesAsync();
                TempData["Error"] = $"⚠️ AI ALERT: {alertMsg}";
            }
            else
            {
                TempData["Success"] = "Patient vitals recorded successfully.";
            }

            return RedirectToAction(nameof(Index), new { patientId = model.PatientID });
        }

        [Authorize(Roles = "Nurse,Doctor,Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var v = await _db.PatientVitals.FindAsync(id);
            if (v == null) return NotFound();
            await PopulateDropdowns(v.PatientID, v.NurseID);
            return View(v);
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Nurse,Doctor,Admin")]
        public async Task<IActionResult> Edit(int id, PatientVitals model)
        {
            if (model == null) return BadRequest();
            if (id != model.VitalsID) return BadRequest();
            ModelState.Remove("Patient"); ModelState.Remove("Nurse"); ModelState.Remove("Consultation");
            if (!ModelState.IsValid) { await PopulateDropdowns(model.PatientID); return View(model); }
            _db.Update(model);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Vitals updated.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var v = await _db.PatientVitals.Include(x => x.Patient).FirstOrDefaultAsync(x => x.VitalsID == id);
            if (v == null) return NotFound();
            return View(v);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var v = await _db.PatientVitals.FindAsync(id);
            if (v != null) { _db.PatientVitals.Remove(v); await _db.SaveChangesAsync(); }
            TempData["Success"] = "Vitals record deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdowns(int? selPat = null, int? selNurse = null)
        {
            ViewBag.PatientList = new SelectList(
                await _db.Patients.Where(p => p.IsActive).OrderBy(p => p.LastName)
                    .Select(p => new { p.PatientID, Name = p.FirstName + " " + p.LastName }).ToListAsync(),
                "PatientID", "Name", selPat);

            ViewBag.NurseList = new SelectList(
                await _db.Nurses.OrderBy(n => n.LastName)
                    .Select(n => new { n.NurseID, Name = n.FirstName + " " + n.LastName + " (" + n.Department + ")" }).ToListAsync(),
                "NurseID", "Name", selNurse);
        }
    }
}

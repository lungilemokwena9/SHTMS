using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SHTMS.Web.Data;
using SHTMS.Web.Models;
using System.Security.Claims;

namespace SHTMS.Web.Controllers
{
    [Authorize]
    public class AppointmentController : Controller
    {
        private readonly ShtmsDbContext _db;
        public AppointmentController(ShtmsDbContext db) { _db = db; }

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

        private static string NormalizeAppointmentType(string? value)
        {
            if (string.Equals(value, "Telemedicine", StringComparison.OrdinalIgnoreCase))
                return "Telemedicine";

            return "InPerson";
        }

        private static string NormalizeAppointmentStatus(string? value)
        {
            return value switch
            {
                "Confirmed" => "Confirmed",
                "Completed" => "Completed",
                "Cancelled" => "Cancelled",
                "NoShow" => "NoShow",
                _ => "Scheduled"
            };
        }

        private async Task<bool> HasAppointmentConflictAsync(int doctorId, DateTime appointmentDate, int? excludeAppointmentId = null)
        {
            var windowStart = appointmentDate.AddMinutes(-30);
            var windowEnd = appointmentDate.AddMinutes(30);

            return await _db.Appointments.AnyAsync(a =>
                a.DoctorID == doctorId &&
                a.AppointmentID != excludeAppointmentId &&
                a.Status != "Cancelled" &&
                a.Status != "NoShow" &&
                a.AppointmentDate >= windowStart &&
                a.AppointmentDate < windowEnd);
        }

        private async Task<bool> CanAccessAppointmentAsync(Appointment appt)
        {
            if (CurrentRole == "Admin")
                return true;

            if (CurrentRole == "Nurse")
                return true;

            if (CurrentRole == "Patient")
            {
                var patientId = await GetCurrentPatientIdAsync();
                return patientId.HasValue && patientId.Value == appt.PatientID;
            }

            if (CurrentRole == "Doctor")
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                return doctorId.HasValue && doctorId.Value == appt.DoctorID;
            }

            return false;
        }

        private async Task<bool> CanModifyAppointmentAsync(Appointment appt)
        {
            if (!await CanAccessAppointmentAsync(appt))
                return false;

            if (CurrentRole == "Admin" || CurrentRole == "Nurse")
                return true;

            return appt.AppointmentDate > DateTime.Now && appt.Status != "Completed";
        }

        // GET: /Appointment
        public async Task<IActionResult> Index(string? status, string? type)
        {
            var query = _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .AsQueryable();

            // ── ROLE-BASED FILTERING ──
            var role = CurrentRole;
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
            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(a => a.AppointmentType == type);

            ViewBag.StatusFilter = status;
            ViewBag.TypeFilter   = type;
            ViewBag.StatusList   = new List<string> { "Scheduled", "Confirmed", "Completed", "Cancelled", "NoShow" };
            ViewBag.TypeList     = new List<string> { "InPerson", "Telemedicine" };

            var list = await query.OrderByDescending(a => a.AppointmentDate).ToListAsync();
            return View(list);
        }

        // GET: /Appointment/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var appt = await _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.AppointmentID == id);
            if (appt == null) return NotFound();
            if (!await CanAccessAppointmentAsync(appt)) return Forbid();
            return View(appt);
        }

        // GET: /Appointment/Create
        // GET: /Appointment/Create?patientId=5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(int? patientId)
        {
            await PopulateDropdowns();
            var model = new Appointment
            {
                AppointmentDate = DateTime.Now.AddDays(1).Date.AddHours(9),
                Status = "Scheduled"
            };
            if (patientId.HasValue)
            {
                model.PatientID = patientId.Value;
                // Pre-select this patient in the dropdown
                ViewBag.SelectedPatient = patientId.Value;
            }
            return View(model);
        }

        // POST: /Appointment/Create
        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Appointment model)
        {
            ModelState.Remove("Patient");
            ModelState.Remove("Doctor");

            model.AppointmentType = NormalizeAppointmentType(model.AppointmentType);
            model.Status = NormalizeAppointmentStatus(model.Status);

            if (CurrentRole == "Patient")
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return Forbid();

                model.PatientID = patientId.Value;
            }

            if (CurrentRole == "Doctor")
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return Forbid();

                model.DoctorID = doctorId.Value;
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdowns();
                return View(model);
            }

            bool conflict = await HasAppointmentConflictAsync(model.DoctorID, model.AppointmentDate);

            if (conflict)
            {
                ModelState.AddModelError("AppointmentDate", "The selected doctor already has an appointment within 30 minutes of this time.");
                await PopulateDropdowns();
                return View(model);
            }

            _db.Appointments.Add(model);
            await _db.SaveChangesAsync();

            // ── Send notifications ──
            var patient = await _db.Patients.FindAsync(model.PatientID);
            var doctor = await _db.Doctors.FindAsync(model.DoctorID);
            if (patient != null && patient.UserID.HasValue)
                await NotificationController.CreateNotification(_db, patient.UserID.Value,
                    $"New appointment booked for {model.AppointmentDate:dd MMM yyyy HH:mm}",
                    $"/Appointment/Details/{model.AppointmentID}", "Appointment", "calendar-check");
            if (doctor != null && doctor.UserID.HasValue)
                await NotificationController.CreateNotification(_db, doctor.UserID.Value,
                    $"New appointment with patient on {model.AppointmentDate:dd MMM yyyy HH:mm}",
                    $"/Appointment/Details/{model.AppointmentID}", "Appointment", "calendar-check");

            TempData["Success"] = "Appointment booked successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Appointment/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var appt = await _db.Appointments.FindAsync(id);
            if (appt == null) return NotFound();
            if (!await CanModifyAppointmentAsync(appt)) return Forbid();
            await PopulateDropdowns(appt.PatientID, appt.DoctorID);
            return View(appt);
        }

        // POST: /Appointment/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Appointment model)
        {
            if (id != model.AppointmentID) return BadRequest();
            ModelState.Remove("Patient");
            ModelState.Remove("Doctor");

            var existing = await _db.Appointments.FirstOrDefaultAsync(a => a.AppointmentID == id);
            if (existing == null) return NotFound();
            if (!await CanModifyAppointmentAsync(existing)) return Forbid();

            model.AppointmentType = NormalizeAppointmentType(model.AppointmentType);
            model.Status = NormalizeAppointmentStatus(model.Status);

            if (CurrentRole == "Patient" || CurrentRole == "Doctor")
            {
                model.PatientID = existing.PatientID;
                model.DoctorID = existing.DoctorID;
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(model.PatientID, model.DoctorID);
                return View(model);
            }

            if (await HasAppointmentConflictAsync(model.DoctorID, model.AppointmentDate, model.AppointmentID))
            {
                ModelState.AddModelError("AppointmentDate", "The selected doctor already has an appointment within 30 minutes of this time.");
                await PopulateDropdowns(model.PatientID, model.DoctorID);
                return View(model);
            }

            existing.PatientID = model.PatientID;
            existing.DoctorID = model.DoctorID;
            existing.AppointmentDate = model.AppointmentDate;
            existing.AppointmentType = model.AppointmentType;
            existing.Status = model.Status;
            existing.ReasonForVisit = model.ReasonForVisit;
            existing.Notes = model.Notes;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Appointment updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Appointment/Cancel/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var appt = await _db.Appointments.FindAsync(id);
            if (appt == null) return NotFound();
            if (!await CanModifyAppointmentAsync(appt)) return Forbid();

            if (appt.Status == "Cancelled")
            {
                TempData["Success"] = "Appointment was already cancelled.";
                return RedirectToAction(nameof(Index));
            }

            if (appt.AppointmentDate <= DateTime.Now && CurrentRole != "Admin")
            {
                TempData["Error"] = "Past appointments cannot be cancelled by this user.";
                return RedirectToAction(nameof(Index));
            }

            appt.Status = "Cancelled";
            await _db.SaveChangesAsync();
            TempData["Success"] = "Appointment cancelled.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Appointment/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var appt = await _db.Appointments
                .Include(a => a.Patient).Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.AppointmentID == id);
            if (appt == null) return NotFound();
            return View(appt);
        }

        // POST: /Appointment/Delete/5
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var appt = await _db.Appointments.FindAsync(id);
            if (appt != null) { _db.Appointments.Remove(appt); await _db.SaveChangesAsync(); }
            TempData["Success"] = "Appointment deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdowns(int? selectedPatient = null, int? selectedDoctor = null)
        {
            ViewBag.PatientList = new SelectList(
                await _db.Patients.Where(p => p.IsActive).OrderBy(p => p.LastName)
                    .Select(p => new { p.PatientID, Name = p.FirstName + " " + p.LastName }).ToListAsync(),
                "PatientID", "Name", selectedPatient);

            ViewBag.DoctorList = new SelectList(
                await _db.Doctors.Where(d => d.IsAvailable).OrderBy(d => d.LastName)
                    .Select(d => new { d.DoctorID, Name = "Dr. " + d.FirstName + " " + d.LastName + " (" + d.Specialization + ")" }).ToListAsync(),
                "DoctorID", "Name", selectedDoctor);

            ViewBag.StatusList = new SelectList(new[] { "Scheduled", "Confirmed", "Completed", "Cancelled", "NoShow" });
            ViewBag.TypeList   = new SelectList(new[] { "InPerson", "Telemedicine" });
        }
    }
}

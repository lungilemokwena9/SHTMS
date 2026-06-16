using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHTMS.Web.Data;
using SHTMS.Web.Models;
using System.Security.Claims;

namespace SHTMS.Web.Controllers
{
    [Authorize]
    public class PatientController : Controller
    {
        private readonly ShtmsDbContext _db;
        public PatientController(ShtmsDbContext db) { _db = db; }

        // GET: /Patient  — LIST
        public async Task<IActionResult> Index(string? search)
        {
            var query = _db.Patients.Where(p => p.IsActive).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(p =>
                    p.FirstName.ToLower().Contains(search) ||
                    p.LastName.ToLower().Contains(search) ||
                    (p.IDNumber != null && p.IDNumber.Contains(search)) ||
                    (p.PhoneNumber != null && p.PhoneNumber.Contains(search)));
            }

            ViewBag.Search = search;
            var patients = await query.OrderBy(p => p.LastName).ToListAsync();
            return View(patients);
        }

        // GET: /Patient/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var patient = await _db.Patients
                .Include(p => p.Appointments).ThenInclude(a => a.Doctor)
                .Include(p => p.Admissions).ThenInclude(a => a.Doctor)
                .FirstOrDefaultAsync(p => p.PatientID == id);

            if (patient == null) return NotFound();
            return View(patient);
        }

        // GET: /Patient/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            PopulateDropdowns();
            return View();
        }

        // POST: /Patient/Create
        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Patient model)
        {
            // Remove navigation properties from validation
            ModelState.Remove("User");
            ModelState.Remove("Appointments");
            ModelState.Remove("Admissions");

            if (!ModelState.IsValid)
            {
                PopulateDropdowns();
                return View(model);
            }

            // Check duplicate ID number
            if (!string.IsNullOrWhiteSpace(model.IDNumber))
            {
                bool exists = await _db.Patients.AnyAsync(p => p.IDNumber == model.IDNumber);
                if (exists)
                {
                    ModelState.AddModelError("IDNumber", "A patient with this ID number already exists.");
                    PopulateDropdowns();
                    return View(model);
                }
            }

            model.RegistrationDate = DateTime.Now;
            _db.Patients.Add(model);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Patient {model.FullName} registered successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Patient/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var patient = await _db.Patients.FindAsync(id);
            if (patient == null) return NotFound();
            PopulateDropdowns();
            return View(patient);
        }

        // POST: /Patient/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Patient model)
        {
            if (id != model.PatientID) return BadRequest();

            ModelState.Remove("User");
            ModelState.Remove("Appointments");
            ModelState.Remove("Admissions");

            if (!ModelState.IsValid)
            {
                PopulateDropdowns();
                return View(model);
            }

            try
            {
                _db.Update(model);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Patient record updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Unable to save changes. Please try again.");
                PopulateDropdowns();
                return View(model);
            }
        }

        // GET: /Patient/Delete/5
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<IActionResult> Delete(int id)
        {
            var patient = await _db.Patients.FindAsync(id);
            if (patient == null) return NotFound();
            return View(patient);
        }

        // POST: /Patient/Delete/5
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var patient = await _db.Patients.FindAsync(id);
            if (patient == null) return NotFound();

            // Soft delete
            patient.IsActive = false;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Patient record deactivated.";
            return RedirectToAction(nameof(Index));
        }

        private void PopulateDropdowns()
        {
            ViewBag.GenderList = new List<string> { "Male", "Female", "Other" };
            ViewBag.BloodTypeList = new List<string> { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-", "Unknown" };
        }
    }
}

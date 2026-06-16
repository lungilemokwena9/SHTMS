using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SHTMS.Web.Data;
using SHTMS.Web.Models;

namespace SHTMS.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly ShtmsDbContext _db;
        public UserController(ShtmsDbContext db) { _db = db; }

        // GET: /User — List all users
        public async Task<IActionResult> Index()
        {
            var users = await _db.Users.Include(u => u.Role).OrderBy(u => u.Username).ToListAsync();
            return View(users);
        }

        // GET: /User/Create — Register new system user
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            return View();
        }

        // POST: /User/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid) { await PopulateDropdowns(); return View(model); }

            // Check unique username
            if (await _db.Users.AnyAsync(u => u.Username == model.Username))
            {
                ModelState.AddModelError("Username", "This username is already taken.");
                await PopulateDropdowns(); return View(model);
            }
            // Check unique email
            if (await _db.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "This email is already registered.");
                await PopulateDropdowns(); return View(model);
            }

            var user = new User
            {
                Username     = model.Username,
                Email        = model.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                RoleID       = model.RoleID,
                IsActive     = true,
                CreatedDate  = DateTime.Now
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // If role is Patient, create patient record linked to this user
            if ((await _db.Roles.FindAsync(model.RoleID))?.RoleName == "Patient" && model.CreateLinkedRecord)
            {
                _db.Patients.Add(new Patient
                {
                    UserID = user.UserID,
                    FirstName = model.FirstName ?? "Unknown",
                    LastName  = model.LastName  ?? "Unknown",
                    DateOfBirth = model.DateOfBirth ?? DateTime.Today.AddYears(-30),
                    Gender    = model.Gender ?? "Other",
                    Email     = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    RegistrationDate = DateTime.Now
                });
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = $"User '{model.Username}' created successfully as {(await _db.Roles.FindAsync(model.RoleID))?.RoleName}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /User/ToggleActive
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.IsActive = !user.IsActive;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"User {user.Username} has been {(user.IsActive ? "activated" : "deactivated")}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /User/ResetPassword
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                TempData["Error"] = "Password must be at least 6 characters.";
                return RedirectToAction(nameof(Index));
            }
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Password reset for {user.Username}.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdowns()
        {
            ViewBag.RoleList = new SelectList(
                await _db.Roles.OrderBy(r => r.RoleName).ToListAsync(),
                "RoleID", "RoleName");
            ViewBag.GenderList = new SelectList(new[] { "Male", "Female", "Other" });
        }
    }

    // ── View Model for user creation ──────────────────────────
    public class CreateUserViewModel
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string Username { get; set; } = "";
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress]
        public string Email { get; set; } = "";
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.MinLength(6)]
        public string Password { get; set; } = "";
        [System.ComponentModel.DataAnnotations.Required]
        public int RoleID { get; set; }

        // Only used if role is Patient + CreateLinkedRecord is true
        public bool CreateLinkedRecord { get; set; } = false;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? PhoneNumber { get; set; }
    }
}

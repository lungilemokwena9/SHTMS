using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SHTMS.Web.Data;
using SHTMS.Web.Models;
using BCrypt.Net;

namespace SHTMS.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly ShtmsDbContext _db;

        public AccountController(ShtmsDbContext db)
        {
            _db = db;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username == model.Username && u.IsActive);

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid username or password.");
                return View(model);
            }

            // Check if this is a demo account with placeholder hash
            bool isDemoHash = user.PasswordHash.StartsWith("$2a$11$examplehash");

            if (isDemoHash)
            {
                // Demo accounts: plain password comparison
                if (model.Password != "Password123!")
                {
                    ModelState.AddModelError("", "Invalid username or password.");
                    return View(model);
                }
            }
            else
            {
                // Real accounts: BCrypt verification
                try
                {
                    if (!BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
                    {
                        ModelState.AddModelError("", "Invalid username or password.");
                        return View(model);
                    }
                }
                catch
                {
                    ModelState.AddModelError("", "Invalid username or password.");
                    return View(model);
                }
            }

            // Update last login
            user!.LastLogin = DateTime.Now;
            await _db.SaveChangesAsync();

            // Build claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name,           user.Username),
                new Claim(ClaimTypes.Email,          user.Email),
                new Claim(ClaimTypes.Role,           user.Role?.RoleName ?? "Patient"),
                new Claim("UserID",                  user.UserID.ToString()),
                new Claim("RoleName",                user.Role?.RoleName ?? "Patient")
            };

            var identity  = new ClaimsIdentity(claims, "ShtmsCookieAuth");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("ShtmsCookieAuth", principal,
                new AuthenticationProperties { IsPersistent = model.RememberMe });

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        // POST: /Account/Logout
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("ShtmsCookieAuth");
            return RedirectToAction("Login");
        }

        // GET: /Account/AccessDenied
        public IActionResult AccessDenied() => View();

        // GET: /Account/Register — Public patient self-registration
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");

            PopulateRegisterDropdowns();
            return View(new RegisterViewModel { DateOfBirth = DateTime.Now.AddYears(-30) });
        }

        // POST: /Account/Register
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");

            if (!ModelState.IsValid)
            {
                PopulateRegisterDropdowns();
                return View(model);
            }

            // Check duplicate username
            if (await _db.Users.AnyAsync(u => u.Username == model.Username))
            {
                ModelState.AddModelError("Username", "This username is already taken.");
                PopulateRegisterDropdowns();
                return View(model);
            }

            // Check duplicate email
            if (await _db.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "This email is already registered.");
                PopulateRegisterDropdowns();
                return View(model);
            }

            // Check duplicate ID number
            if (!string.IsNullOrWhiteSpace(model.IDNumber))
            {
                if (await _db.Patients.AnyAsync(p => p.IDNumber == model.IDNumber))
                {
                    ModelState.AddModelError("IDNumber", "A patient with this ID number already exists.");
                    PopulateRegisterDropdowns();
                    return View(model);
                }
            }

            // Get or create Patient role
            var patientRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == "Patient");
            if (patientRole == null)
            {
                patientRole = new Role { RoleName = "Patient", Description = "Patient role for self-registered users" };
                _db.Roles.Add(patientRole);
                await _db.SaveChangesAsync();
            }

            // Create User account
            var user = new User
            {
                Username = model.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                Email = model.Email,
                RoleID = patientRole.RoleID,
                IsActive = true,
                CreatedDate = DateTime.Now
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Create Patient record linked to user
            var patient = new Patient
            {
                UserID = user.UserID,
                FirstName = model.FirstName,
                LastName = model.LastName,
                DateOfBirth = model.DateOfBirth,
                Gender = model.Gender,
                IDNumber = model.IDNumber,
                PhoneNumber = model.PhoneNumber,
                Email = model.Email,
                Address = model.Address,
                BloodType = model.BloodType,
                Allergies = model.Allergies,
                EmergencyContact = model.EmergencyContact,
                EmergencyPhone = model.EmergencyPhone,
                RegistrationDate = DateTime.Now,
                IsActive = true
            };
            _db.Patients.Add(patient);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Welcome, {patient.FullName}! Your account has been created. Please log in to view your patient dashboard.";

            return RedirectToAction("Login");
        }

        private void PopulateRegisterDropdowns()
        {
            ViewBag.GenderList = new List<string> { "Male", "Female", "Other" };
            ViewBag.BloodTypeList = new List<string> { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-", "Unknown" };
        }
    }
}

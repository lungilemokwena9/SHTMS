using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHTMS.Web.Data;
using SHTMS.Web.Models;
using System.Security.Claims;

namespace SHTMS.Web.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ShtmsDbContext _db;
        public DashboardController(ShtmsDbContext db) { _db = db; }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            var role  = User.FindFirstValue(ClaimTypes.Role) ?? "";
            var userId = int.TryParse(User.FindFirstValue("UserID"), out var parsedUserId) ? parsedUserId : 0;

            if (role == "Patient")
            {
                var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserID == userId && p.IsActive);
                if (patient != null)
                {
                    var patientId = patient.PatientID;

                    return View(new DashboardViewModel
                    {
                        UserRole = role,
                        UserName = User.Identity?.Name ?? "",
                        MyAppointments = await _db.Appointments.CountAsync(a => a.PatientID == patientId && a.AppointmentDate >= DateTime.Now && a.Status != "Cancelled"),
                        MyPrescriptions = await _db.Prescriptions.CountAsync(p => p.PatientID == patientId),
                        MyOutstandingBills = await _db.Billings.CountAsync(b => b.PatientID == patientId && b.PaymentStatus == "Pending"),
                        UnacknowledgedAlerts = await _db.AIAlerts.CountAsync(a => a.PatientID == patientId && !a.IsAcknowledged),
                        UpcomingAppointments = await _db.Appointments
                            .Include(a => a.Patient)
                            .Include(a => a.Doctor)
                            .Where(a => a.PatientID == patientId && a.AppointmentDate >= DateTime.Now && a.Status != "Cancelled")
                            .OrderBy(a => a.AppointmentDate)
                            .Take(5)
                            .ToListAsync(),
                        RecentAlerts = await _db.AIAlerts
                            .Include(a => a.Patient)
                            .Where(a => a.PatientID == patientId && !a.IsAcknowledged)
                            .OrderByDescending(a => a.AlertDate)
                            .Take(5)
                            .ToListAsync()
                    });
                }
            }

            var vm = new DashboardViewModel
            {
                UserRole = role,
                UserName = User.Identity?.Name ?? "",
                TotalPatients       = await _db.Patients.CountAsync(p => p.IsActive),
                TodayAppointments   = await _db.Appointments.CountAsync(a => a.AppointmentDate.Date == today),
                ActiveAdmissions    = await _db.Admissions.CountAsync(a => a.Status == "Admitted"),
                PendingBills        = await _db.Billings.CountAsync(b => b.PaymentStatus == "Pending"),
                LowStockMedications = await _db.Medications.CountAsync(m => m.StockQuantity <= m.ReorderLevel && m.IsActive),
                UnacknowledgedAlerts= await _db.AIAlerts.CountAsync(a => !a.IsAcknowledged),

                UpcomingAppointments = await _db.Appointments
                    .Include(a => a.Patient)
                    .Include(a => a.Doctor)
                    .Where(a => a.AppointmentDate >= DateTime.Now && a.Status != "Cancelled")
                    .OrderBy(a => a.AppointmentDate)
                    .Take(5)
                    .ToListAsync(),

                RecentAlerts = await _db.AIAlerts
                    .Include(a => a.Patient)
                    .Where(a => !a.IsAcknowledged)
                    .OrderByDescending(a => a.AlertDate)
                    .Take(5)
                    .ToListAsync()
            };

            return View(vm);
        }
    }
}

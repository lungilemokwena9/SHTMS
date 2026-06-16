using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHTMS.Web.Data;
using SHTMS.Web.Models;
using System.Security.Claims;

namespace SHTMS.Web.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly ShtmsDbContext _db;

        public NotificationController(ShtmsDbContext db)
        {
            _db = db;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        // GET: /Notification/Index — full notification list
        public async Task<IActionResult> Index()
        {
            int userId = GetCurrentUserId();
            var notifications = await _db.Notifications
                .Where(n => n.UserID == userId)
                .OrderByDescending(n => n.CreatedDate)
                .Take(100)
                .ToListAsync();

            ViewBag.UnreadCount = notifications.Count(n => !n.IsRead);
            return View(notifications);
        }

        // POST: /Notification/MarkRead/5
        [HttpPost]
        public async Task<IActionResult> MarkRead(int id)
        {
            int userId = GetCurrentUserId();
            var notif = await _db.Notifications
                .FirstOrDefaultAsync(n => n.NotificationID == id && n.UserID == userId);

            if (notif != null && !notif.IsRead)
            {
                notif.IsRead = true;
                notif.ReadDate = DateTime.Now;
                await _db.SaveChangesAsync();
            }

            return Ok();
        }

        // POST: /Notification/MarkAllRead
        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            int userId = GetCurrentUserId();
            var unread = await _db.Notifications
                .Where(n => n.UserID == userId && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
            {
                n.IsRead = true;
                n.ReadDate = DateTime.Now;
            }

            await _db.SaveChangesAsync();
            return Ok();
        }

        // GET: /Notification/GetUnreadCount — returns JSON for AJAX polling
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            int userId = GetCurrentUserId();
            int count = await _db.Notifications
                .CountAsync(n => n.UserID == userId && !n.IsRead);

            return Json(new { count });
        }

        // GET: /Notification/GetRecent — returns JSON for dropdown
        [HttpGet]
        public async Task<IActionResult> GetRecent()
        {
            int userId = GetCurrentUserId();
            var notifications = await _db.Notifications
                .Where(n => n.UserID == userId)
                .OrderByDescending(n => n.CreatedDate)
                .Take(10)
                .Select(n => new
                {
                    n.NotificationID,
                    n.Message,
                    n.LinkUrl,
                    n.Category,
                    n.Icon,
                    n.IsRead,
                    CreatedDate = n.CreatedDate.ToString("dd MMM HH:mm")
                })
                .ToListAsync();

            int unreadCount = notifications.Count(n => n.IsRead == false);
            return Json(new { notifications, unreadCount });
        }

        // ── Helper: create a notification (called from other controllers) ──
        public static async Task CreateNotification(ShtmsDbContext db, int userId,
            string message, string? linkUrl = null, string category = "General", string icon = "bell")
        {
            db.Notifications.Add(new Notification
            {
                UserID = userId,
                Message = message,
                LinkUrl = linkUrl,
                Category = category,
                Icon = icon,
                IsRead = false,
                CreatedDate = DateTime.Now
            });
            await db.SaveChangesAsync();
        }
    }
}

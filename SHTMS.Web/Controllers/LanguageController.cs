using Microsoft.AspNetCore.Mvc;
using SHTMS.Web.Services;

namespace SHTMS.Web.Controllers;

public class LanguageController : Controller
{
    [HttpPost]
    public IActionResult Set(string lang, string? returnUrl)
    {
        if (LocalizationService.SupportedLanguages.ContainsKey(lang))
        {
            Response.Cookies.Append("SHTMS.Language", lang, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            });
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction("Index", "Dashboard");
    }
}
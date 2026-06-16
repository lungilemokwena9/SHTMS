using System.Text.Json;

namespace SHTMS.Web.Services;

/// <summary>
/// JSON-based localization service supporting all 11 SA official languages.
/// </summary>
public class LocalizationService
{
    private readonly IWebHostEnvironment _env;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly Dictionary<string, Dictionary<string, string>> _cache = new();
    private static readonly object _lock = new();

    public static readonly Dictionary<string, string> SupportedLanguages = new()
    {
        { "en", "English" },
        { "af", "Afrikaans" },
        { "zu", "isiZulu" },
        { "xh", "isiXhosa" },
        { "st", "Sesotho" },
        { "tn", "Setswana" },
        { "nso", "Sepedi" },
        { "ts", "Xitsonga" },
        { "ss", "siSwati" },
        { "ve", "Tshivenda" },
        { "nr", "isiNdebele" }
    };

    public LocalizationService(IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor)
    {
        _env = env;
        _httpContextAccessor = httpContextAccessor;
    }

    public string CurrentLanguage
    {
        get
        {
            var cookie = _httpContextAccessor.HttpContext?.Request.Cookies["SHTMS.Language"];
            if (!string.IsNullOrEmpty(cookie) && SupportedLanguages.ContainsKey(cookie))
                return cookie;
            return "en"; // default
        }
    }

    public string T(string key, string? fallback = null)
    {
        var lang = CurrentLanguage;
        var resources = LoadResources(lang);

        if (resources.TryGetValue(key, out var value))
            return value;

        // Try English fallback
        if (lang != "en")
        {
            var enResources = LoadResources("en");
            if (enResources.TryGetValue(key, out var enValue))
                return enValue;
        }

        return fallback ?? key;
    }

    private Dictionary<string, string> LoadResources(string lang)
    {
        if (_cache.TryGetValue(lang, out var cached))
            return cached;

        lock (_lock)
        {
            if (_cache.TryGetValue(lang, out cached))
                return cached;

            var filePath = Path.Combine(_env.ContentRootPath, "Resources", $"lang.{lang}.json");
            if (!File.Exists(filePath))
            {
                // Fall back to English
                if (lang != "en")
                    return LoadResources("en");
                return new Dictionary<string, string>();
            }

            var json = File.ReadAllText(filePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>();
            _cache[lang] = dict;
            return dict;
        }
    }

    /// <summary>
    /// Returns all translation keys for a given prefix (e.g., "sidebar.").
    /// </summary>
    public Dictionary<string, string> GetByPrefix(string prefix)
    {
        var lang = CurrentLanguage;
        var resources = LoadResources(lang);
        return resources
            .Where(kvp => kvp.Key.StartsWith(prefix))
            .ToDictionary(kvp => kvp.Key.Substring(prefix.Length), kvp => kvp.Value);
    }
}
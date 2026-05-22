using System.Text.Json;

namespace api_test.Services
{
    // ── JSON shape (mirrors ar.json) ─────────────────────────────────────────

    internal sealed class MedTranslation
    {
        public string? name { get; set; }
        public string? description { get; set; }
    }

    internal sealed class TranslationFile
    {
        public Dictionary<string, MedTranslation> medications { get; set; } = new();
        public Dictionary<string, string> interactions { get; set; } = new();
        public Dictionary<string, string> notifications { get; set; } = new();
        public Dictionary<string, string> dosageForms { get; set; } = new();
    }

    // ── Interface ────────────────────────────────────────────────────────────

    public interface ITranslationService
    {
        string GetMedName(int medId, string lang);
        string? GetMedDescription(int medId, string lang);
        string GetInteractionReason(string reason, string lang);
        string GetNotificationTitle(string type, string lang,
                                     int? retryCount = null, int? maxRetry = null);
        string GetNotificationMessage(string type, string lang,
                                       string medName, string dosage);
        string GetNotificationText(string key, string lang, string fallback,
                                   params string[] args);
        string GetDosageForm(string code, string lang);

        /// <summary>
        /// Looks up a medication ID by its translated name (any language).
        /// Returns null if not found — caller falls back to DB trade-name search.
        /// </summary>
        int? FindMedIdByName(string name);
    }

    // ── Implementation ───────────────────────────────────────────────────────

    public sealed class TranslationService : ITranslationService
    {
        private readonly Dictionary<string, TranslationFile> _langs = new();
        private readonly ILogger<TranslationService> _logger;

        public TranslationService(
            ILogger<TranslationService> logger,
            IWebHostEnvironment env)
        {
            _logger = logger;
            LoadLanguage("ar", env);
        }

        // ── Public API ───────────────────────────────────────────────────────

        public string GetMedName(int medId, string lang)
        {
            var t = GetFile(lang);
            if (t is null) return string.Empty;

            return t.medications.TryGetValue(medId.ToString(), out var med)
                   && !string.IsNullOrWhiteSpace(med.name)
                ? med.name
                : string.Empty;
        }

        public string? GetMedDescription(int medId, string lang)
        {
            var t = GetFile(lang);
            if (t is null) return null;

            return t.medications.TryGetValue(medId.ToString(), out var med)
                ? med.description
                : null;
        }

        public string GetInteractionReason(string reason, string lang)
        {
            if (string.IsNullOrWhiteSpace(reason)) return reason;

            var t = GetFile(lang);
            if (t is null) return reason;

            if (t.interactions.TryGetValue(reason.Trim(), out var exact))
                return exact;

            var parts = reason.Split(',', StringSplitOptions.TrimEntries);
            var translated = parts.Select(part =>
                t.interactions.TryGetValue(part, out var tr) ? tr : part);

            return string.Join("، ", translated);
        }

        public string GetNotificationTitle(string type, string lang,
                                           int? retryCount = null, int? maxRetry = null)
        {
            if (retryCount.HasValue && maxRetry.HasValue
                && string.Equals(type, "DoseReminderRetry", StringComparison.OrdinalIgnoreCase))
                return GetNotificationText(
                    "DoseReminderRetry",
                    lang,
                    $"Dose Reminder (retry {retryCount.Value}/{maxRetry.Value})",
                    retryCount.Value.ToString(),
                    maxRetry.Value.ToString());

            return GetNotificationText(type, lang, type);
        }

        public string GetNotificationMessage(string type, string lang,
                                             string medName, string dosage)
        {
            var key = $"{type}Message";
            return GetNotificationText(key, lang, string.Empty, medName, dosage);
        }

        public string GetNotificationText(string key, string lang, string fallback,
                                          params string[] args)
        {
            var t = GetFile(lang);
            var template = t is not null
                && !string.IsNullOrWhiteSpace(key)
                && t.notifications.TryGetValue(key, out var translated)
                && !string.IsNullOrWhiteSpace(translated)
                    ? translated
                    : fallback;

            return FormatTemplate(template, args);
        }

        public string GetDosageForm(string code, string lang)
        {
            if (string.IsNullOrWhiteSpace(code)) return code;

            var t = GetFile(lang);
            if (t is null) return code;

            return t.dosageForms.TryGetValue(code.Trim(), out var form) ? form : code;
        }

        public int? FindMedIdByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            foreach (var lang in _langs.Values)
            {
                var match = lang.medications.FirstOrDefault(x =>
                    x.Value.name?.Trim().Equals(
                        name.Trim(), StringComparison.OrdinalIgnoreCase) == true);

                if (match.Key != null)
                    return int.Parse(match.Key);
            }

            return null;
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private TranslationFile? GetFile(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang) || lang.ToLower() == "en")
                return null;

            return _langs.TryGetValue(lang.ToLower(), out var file) ? file : null;
        }

        private static string FormatTemplate(string template, params string[] args)
        {
            var result = template ?? string.Empty;

            for (var i = 0; i < args.Length; i++)
                result = result.Replace($"{{{i}}}", args[i] ?? string.Empty);

            return result;
        }

        private void LoadLanguage(string lang, IWebHostEnvironment env)
        {
            var path = Path.Combine(env.ContentRootPath, "Resources", $"{lang}.json");

            if (!File.Exists(path))
            {
                _logger.LogWarning("Translation file not found: {Path}", path);
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                var file = JsonSerializer.Deserialize<TranslationFile>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (file is not null)
                {
                    _langs[lang] = file;
                    _logger.LogInformation(
                        "Loaded {Count} medication translations for lang='{Lang}'",
                        file.medications.Count, lang);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to load translation file for lang='{Lang}'", lang);
            }
        }
    }
}

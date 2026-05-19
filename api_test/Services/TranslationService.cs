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

        string GetDosageForm(string code, string lang);
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
            if (string.IsNullOrWhiteSpace(reason))
                return reason;

            var t = GetFile(lang);
            if (t is null)
                return reason;

            if (t.interactions.TryGetValue(reason.Trim(), out var exact))
                return exact;

            var parts = reason.Split(',', StringSplitOptions.TrimEntries);

            var translated = parts.Select(part =>
                t.interactions.TryGetValue(part, out var translatedPart)
                    ? translatedPart
                    : part);

            return string.Join("، ", translated);
        }

        public string GetNotificationTitle(
            string type,
            string lang,
            int? retryCount = null,
            int? maxRetry = null)
        {
            var t = GetFile(lang);
            if (t is null)
                return type;

            if (retryCount.HasValue &&
                maxRetry.HasValue &&
                t.notifications.TryGetValue("DoseReminderRetry", out var retryTemplate))
            {
                return retryTemplate
                    .Replace("{0}", retryCount.Value.ToString())
                    .Replace("{1}", maxRetry.Value.ToString());
            }

            return t.notifications.TryGetValue(type, out var title)
                ? title
                : type;
        }

        public string GetNotificationMessage(
            string type,
            string lang,
            string medName,
            string dosage)
        {
            var t = GetFile(lang);
            if (t is null)
                return string.Empty;

            var key = $"{type}Message";

            if (!t.notifications.TryGetValue(key, out var template))
                return string.Empty;

            return template
                .Replace("{0}", medName)
                .Replace("{1}", dosage);
        }

        public string GetDosageForm(string code, string lang)
        {
            if (string.IsNullOrWhiteSpace(code))
                return code;

            var t = GetFile(lang);
            if (t is null)
                return code;

            return t.dosageForms.TryGetValue(code.Trim(), out var translated)
                ? translated
                : code;
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private TranslationFile? GetFile(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang) || lang.ToLower() == "en")
                return null;

            return _langs.TryGetValue(lang.ToLower(), out var file)
                ? file
                : null;
        }

        private void LoadLanguage(string lang, IWebHostEnvironment env)
        {
            var path = Path.Combine(
                env.ContentRootPath,
                "Resources",
                $"{lang}.json");

            if (!File.Exists(path))
            {
                _logger.LogWarning("Translation file not found: {Path}", path);
                return;
            }

            try
            {
                var json = File.ReadAllText(path);

                var file = JsonSerializer.Deserialize<TranslationFile>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (file is not null)
                {
                    _langs[lang] = file;

                    _logger.LogInformation(
                        "Loaded {Count} medication translations for lang='{Lang}'",
                        file.medications.Count,
                        lang);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to load translation file for lang='{Lang}'",
                    lang);
            }
        }
    }
}
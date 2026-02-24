using System.IO;
using System.Text.Json;

namespace CrosshairFlex.Desktop.Services;

public sealed class LocalizationService
{
    private readonly Dictionary<string, string> _texts = new(StringComparer.OrdinalIgnoreCase);
    private string _language = "en";

    public string CurrentLanguage => _language;

    public void Load(string language)
    {
        var requested = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim().ToLowerInvariant();
        _texts.Clear();
        _language = requested;

        foreach (var candidate in EnumerateLanguageCandidates(requested))
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            var json = File.ReadAllText(candidate);
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (values is null)
            {
                continue;
            }

            foreach (var kv in values)
            {
                _texts[kv.Key] = kv.Value;
            }

            return;
        }
    }

    public string T(string key, string fallback)
    {
        return _texts.TryGetValue(key, out var value) ? value : fallback;
    }

    private static IEnumerable<string> EnumerateLanguageCandidates(string language)
    {
        var baseDir = AppContext.BaseDirectory;
        var sourceDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
        yield return Path.Combine(baseDir, "Resources", "Languages", $"{language}.json");
        yield return Path.Combine(sourceDir, "Resources", "Languages", $"{language}.json");

        if (!string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(baseDir, "Resources", "Languages", "en.json");
            yield return Path.Combine(sourceDir, "Resources", "Languages", "en.json");
        }
    }
}

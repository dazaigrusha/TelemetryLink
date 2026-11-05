using System.Globalization;

namespace TelemetryLink.Cli;

/// <summary>
/// Найпростіший розбір аргументів: --ключ значення і --прапорець.
///
/// Свідомо без зовнішньої бібліотеки: сто рядків тут зрозуміліші за
/// залежність, яку доведеться пояснювати на захисті.
/// </summary>
public sealed class CommandLineOptions
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);

    public string Command { get; }

    public CommandLineOptions(string[] args)
    {
        Command = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal)
            ? args[0].ToLowerInvariant()
            : "help";

        for (int i = Command == "help" ? 0 : 1; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;

            string key = args[i][2..];
            bool hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            _values[key] = hasValue ? args[++i] : null;
        }
    }

    public bool Has(string key) => _values.ContainsKey(key);

    public string? Value(string key) => _values.GetValueOrDefault(key);

    public string Value(string key, string fallback) => _values.GetValueOrDefault(key) ?? fallback;

    public string Require(string key) =>
        _values.GetValueOrDefault(key) ?? throw new ArgumentException($"Не задано обов'язковий параметр --{key}.");

    public int Value(string key, int fallback) =>
        _values.TryGetValue(key, out string? text) && int.TryParse(text, out int parsed) ? parsed : fallback;

    public double Value(string key, double fallback) =>
        _values.TryGetValue(key, out string? text) &&
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : fallback;

    /// <summary>Список через кому: --scenario calm,working,emergency</summary>
    public string[] List(string key, params string[] fallback)
    {
        string? text = _values.GetValueOrDefault(key);
        return string.IsNullOrWhiteSpace(text)
            ? fallback
            : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

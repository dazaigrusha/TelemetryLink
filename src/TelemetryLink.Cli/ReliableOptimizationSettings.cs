using System.Globalization;
namespace TelemetryLink.Cli;

public sealed record ReliableOptimizationSettings(string[] Scenarios, int[] SearchSeeds, int[] ValidationSeeds, int Repeats)
{
    public int SearchRunsPerCandidate => checked(Scenarios.Length * SearchSeeds.Length * Repeats);
    public int ValidationRunsPerCandidate => checked(Scenarios.Length * ValidationSeeds.Length * Repeats);
    public static ReliableOptimizationSettings Parse(CommandLineOptions options)
    {
        if (options.Has("channel-seed") || options.Has("seeds"))
            throw new ArgumentException("Для reliable задайте --search-seeds та --validation-seeds.");
        string[] List(string key, string fallback)
        {
            string[] values = (options.Has(key) ? options.Require(key) : fallback).Split(',').Select(x => x.Trim().ToLowerInvariant()).ToArray();
            if (values.Length == 0 || values.Length > 100 || values.Any(string.IsNullOrEmpty) || values.Distinct().Count() != values.Length)
                throw new ArgumentException($"--{key}: потрібні різні непорожні значення (до 100).");
            return values;
        }
        int[] Seeds(string key, string fallback)
        {
            var result = List(key, fallback).Select(x => int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed)
                ? seed : throw new ArgumentException($"--{key}: некоректний seed.")).ToArray();
            if (result.Distinct().Count() != result.Length) throw new ArgumentException($"--{key}: повторні seed.");
            return result;
        }
        var search = Seeds("search-seeds", "4001,4002,4003");
        var validation = Seeds("validation-seeds", "5001,5002,5003,5004,5005");
        if (search.Intersect(validation).Any()) throw new ArgumentException("Seed пошуку та перевірки не можуть перетинатися.");
        int repeats = options.Value("fitness-repeats", 1);
        if (repeats is < 1 or > 10) throw new ArgumentException("--fitness-repeats має бути 1..10.");
        return new(List("scenario", "calm,working,emergency"), search, validation, repeats);
    }
}

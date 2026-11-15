using System.Globalization;

namespace TelemetryLink.Cli;

public sealed record ComparisonSettings(string Dimension, string[] Variants, string[] Scenarios,
    int[] Seeds, int Repeats, int Warmup, bool Ablation)
{
    public static ComparisonSettings Parse(CommandLineOptions options, int defaultSeed)
    {
        bool ablation = options.Has("ablation");
        if (ablation && options.Has("vary")) throw new ArgumentException("Оберіть --vary або --ablation, а не обидва.");
        string dimension = "ablation";
        string[] variants = [];
        if (!ablation)
        {
            string text = options.Has("vary") ? options.Require("vary") : "packer=sequential,greedy,dp";
            string[] parts = text.Split('=');
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
                throw new ArgumentException("Очікується --vary packer=sequential,greedy,dp.");
            dimension = parts[0].Trim().ToLowerInvariant();
            if (dimension is not ("codec" or "packer" or "scheduler" or "classifier" or "delivery"))
                throw new ArgumentException("Вимір має бути codec, packer, scheduler, classifier або delivery.");
            variants = List(parts[1], "--vary").Select(s => s.ToLowerInvariant()).ToArray();
        }
        string[] scenarios = options.Has("scenario") ? List(options.Require("scenario"), "--scenario")
            .Select(s => s.ToLowerInvariant()).ToArray() : ["calm", "working", "emergency"];
        if (options.Has("seeds") && options.Has("channel-seed"))
            throw new ArgumentException("Задайте --seeds або --channel-seed, а не обидва.");
        int[] seeds = options.Has("seeds") ? List(options.Require("seeds"), "--seeds").Select(ParseSeed).ToArray()
            : [options.Value("channel-seed", defaultSeed)];
        if (seeds.Distinct().Count() != seeds.Length) throw new ArgumentException("Seed не повинні повторюватися.");
        int repeats = options.Value("repeats", 3), warmup = options.Value("warmup", 1);
        if (repeats is < 1 or > 1000 || warmup is < 0 or > 100)
            throw new ArgumentException("--repeats має бути 1..1000, --warmup — 0..100.");
        return new ComparisonSettings(dimension, variants, scenarios, seeds, repeats, warmup, ablation);
    }

    private static string[] List(string value, string option)
    {
        string[] items = value.Split(',').Select(s => s.Trim()).ToArray();
        if (items.Any(string.IsNullOrEmpty) || items.Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Length)
            throw new ArgumentException($"{option}: порожні або повторні значення недопустимі.");
        return items;
    }

    private static int ParseSeed(string value) => int.TryParse(value, NumberStyles.Integer,
        CultureInfo.InvariantCulture, out int seed) ? seed : throw new ArgumentException($"Некоректний seed '{value}'.");
}

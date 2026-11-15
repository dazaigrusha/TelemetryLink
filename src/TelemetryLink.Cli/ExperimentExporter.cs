using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Metrics;

namespace TelemetryLink.Cli;

public static class ExperimentExporter
{
    public static string CsvRow(params object?[] values) => string.Join(',', values.Select(value =>
    {
        string text = value switch
        {
            null => "",
            bool flag => flag ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };
        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }));

    public static string[] OutputPaths(string path)
    {
        if (!path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("--out має вказувати CSV-файл.");
        string full = Path.GetFullPath(path);
        return [full, full[..^4] + ".summary.csv", full[..^4] + ".manifest.json"];
    }

    public static void ValidateDestinations(string path, params string?[] inputs)
    {
        var sources = inputs.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => Path.GetFullPath(p!)).ToArray();
        if (OutputPaths(path).Any(output => sources.Any(input => string.Equals(input, output,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))))
            throw new ArgumentException("Файли результатів не можуть перезаписувати вхідні дані, конфігурацію або модель.");
    }

    public static void Print(IReadOnlyList<ExperimentSummary> summaries, IReadOnlyList<ExperimentVariant> variants)
    {
        Console.WriteLine("\nПорівняння: середні значення; ΔQ — парна різниця відносно першого варіанта.");
        Console.WriteLine($"{"сценарій",-12}{"варіант",-24}{"N",5}{"R",9}{"Q mean",11}{"Q sd",11}{"ΔQ",11}{"час,мс",11}");
        foreach (var s in summaries)
            Console.WriteLine(FormattableString.Invariant($"{s.Scenario,-12}{s.Variant,-24}{s.Samples,5}{s.MeanR,9:F4}{s.MeanQ,11:F5}{s.StdDevQ,11:F5}{s.MeanDeltaQ,11:F5}{s.MeanElapsedMs,11:F1}"));
        Console.WriteLine("\nСереднє Q (шкала 0..1):");
        foreach (var s in summaries)
            Console.WriteLine($"  {s.Scenario,-12}{s.Variant,-24}{new string('█', (int)Math.Round(Math.Clamp(s.MeanQ, 0, 1) * 35))}");
        foreach (var variant in variants.Where(v => v.Note is not null))
            Console.WriteLine($"  {variant.Name}: {variant.Note}");
        Console.WriteLine("  SD — описовий розкид, не доказ переваги. При N=1 SD=0 за домовленістю.\n");
    }

    public static void Write(string path, ComparisonSettings settings, IReadOnlyList<ExperimentVariant> variants,
        IReadOnlyList<ExperimentRun> runs, IReadOnlyList<ExperimentSummary> summaries,
        TeamConfig config, CommandLineOptions options)
    {
        string sessionPath = options.Value("session", Path.Combine(CommandContext.DataDirectory, "session.csv"));
        string configPath = options.Value("config", Path.Combine(CommandContext.DataDirectory, "team_config.json"));
        string? modelPath = options.Value("model");
        ValidateDestinations(path, sessionPath, configPath, modelPath);
        string[] paths = OutputPaths(path);
        var raw = new StringBuilder();
        raw.AppendLine("scenario,variant,channel_seed,repeat,reused_baseline,codec,packer,scheduler,classifier,window,compression_threshold,critical_weight,frames,critical,recovered_frames,recovered_critical,reference_bits,sent_bits,built_bits,elapsed_ms,packets_built,packets_sent,packets_lost,packets_over_budget,packets_accepted,value,delivered_value,compression_ratio,S,R,T,Q,critical_loss,critical_loss_limit,within_threshold,exact_restore,receiver_crashed,channel_time_s," + DeliveryDiagnosticColumns.Header);
        foreach (var run in runs)
        {
            var r = run.Report;
            raw.AppendLine(CsvRow(new object?[] { r.ScenarioName, run.Variant, run.ChannelSeed, run.Repeat, run.ReusedBaseline,
                r.CodecName, r.PackerName, r.SchedulerName, r.ClassifierName, r.Tuning.WindowFrames,
                r.Tuning.CompressionThreshold, r.Tuning.CriticalWeight, r.FrameCount, r.CriticalFrameCount,
                r.RecoveredFrameCount, r.RecoveredCriticalFrameCount, r.ReferenceBits, r.SentBits, r.BuiltBits,
                r.ElapsedSeconds * 1000, r.PacketsBuilt, r.Channel.Sent, r.Channel.Lost, r.Channel.OverBudget,
                r.PacketsAccepted, r.SelectedValue, r.DeliveredValue, r.CompressionRatio, r.S, r.R, r.T, r.Q,
                r.CriticalLossRatio, config.CriticalLossThreshold(r.ScenarioName),
                r.CriticalLossRatio <= config.CriticalLossThreshold(r.ScenarioName) + 1e-9,
                r.ExactRestore, r.ReceiverCrashed, r.Channel.ChannelTimeSeconds }.Concat(DeliveryDiagnosticColumns.Values(r)).ToArray()));
        }
        var summary = new StringBuilder("scenario,variant,samples,measured_runs,mean_S,mean_R,mean_T,mean_Q,sd_Q,mean_delta_Q,mean_elapsed_ms,mean_sent_bits,mean_delivered_value,mean_critical_loss,exact_restore\n");
        foreach (var s in summaries)
            summary.AppendLine(CsvRow(s.Scenario, s.Variant, s.Samples, s.MeasuredRuns, s.MeanS, s.MeanR,
                s.MeanT, s.MeanQ, s.StdDevQ, s.MeanDeltaQ, s.MeanElapsedMs, s.MeanSentBits,
                s.MeanDeliveredValue, s.MeanCriticalLoss, s.ExactRestore));
        var manifest = new
        {
            SchemaVersion = 1,
            CreatedUtc = DateTimeOffset.UtcNow,
            Runtime = RuntimeInformation.FrameworkDescription,
            OS = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            Inputs = new
            {
                Session = Fingerprint(sessionPath),
                Config = Fingerprint(configPath),
                Model = modelPath is null ? null : Fingerprint(modelPath)
            },
            Protocol = new
            {
                settings.Dimension,
                settings.Ablation,
                settings.Repeats,
                settings.Warmup,
                ChannelSeeds = settings.Seeds,
                Baseline = variants[0].Name,
                Order = "rotation by repetition",
                WarmupIncludedInStatistics = false,
                FixedSeedRepeatsAreIndependentChannelSamples = false,
                StdDev = "sample SD (n-1); zero by convention for n=1",
                Delta = "paired by scenario, channel_seed and repeat"
            },
            Variants = variants.Select(v => new {
                v.Name,
                v.ReuseBaseline,
                v.Note,
                Codec = v.Options.Codec.Name,
                Packer = v.Options.Packer.Name,
                Scheduler = v.Options.Scheduler.Name,
                Classifier = v.Options.Classifier.Name,
                Delivery = Core.Pipeline.DeliveryPolicies.Name(v.Options.Delivery),
                v.Options.Tuning,
                v.Options.DeviceId
            }),
            Scenarios = settings.Scenarios.Select(config.Scenario),
            Metric = new { config.Alpha, config.Beta, config.Gamma, config.ReferenceTimeSeconds },
            Rows = runs.Count,
            MeasuredRuns = runs.Count(r => !r.ReusedBaseline),
            Files = paths.Select(Path.GetFileName)
        };
        string metadata = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        });
        ReportRenderer.EnsureDirectory(paths[0]);
        File.WriteAllText(paths[0], raw.ToString());
        File.WriteAllText(paths[1], summary.ToString());
        File.WriteAllText(paths[2], metadata);
        foreach (string output in paths) Console.WriteLine($"записано: {output}");
    }

    private sealed record InputFingerprint(string FileName, string Sha256);
    private static InputFingerprint Fingerprint(string path)
        => new(Path.GetFileName(path), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
}

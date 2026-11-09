using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Metrics;
using TelemetryLink.Core.Prediction;

namespace TelemetryLink.Cli;

public sealed record ClassifierEvaluation(int Samples, double MacroF1, double Accuracy,
    int[][] Confusion, double[] F1, double MajorityMacroF1)
{
    public static ClassifierEvaluation From(ClassificationReport report, ClassificationReport majority)
        => new(report.SampleCount, report.MacroF1, report.Accuracy,
            Enumerable.Range(0, report.ClassCount).Select(a => Enumerable.Range(0, report.ClassCount)
                .Select(p => report.Confusion(a, p)).ToArray()).ToArray(),
            Enumerable.Range(0, report.ClassCount).Select(report.F1).ToArray(), majority.MacroF1);
}

public static class TrainingArtifacts
{
    public static string[] OutputPaths(string path)
    {
        if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("--out моделі має закінчуватися на .json.");
        string full = Path.GetFullPath(path);
        return [full, full[..^5] + ".training.json", full[..^5] + ".history.csv"];
    }
    public static void ValidateDestinations(string path, params string[] inputs)
    {
        var sources = inputs.Select(Path.GetFullPath).ToArray();
        if (OutputPaths(path).Any(output => sources.Any(input => string.Equals(output, input,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))))
            throw new ArgumentException("Модель та її звіти не можуть перезаписувати вхідні дані або конфігурацію.");
    }

    public static void Write(string output, ClassifierModel model, PerceptronTrainer trainer, TrainingOptions options,
        string trainPath, string configPath, string? testPath, ClassifierEvaluation? evaluation, TeamConfig config)
    {
        string[] paths = OutputPaths(output);
        model.Save(paths[0]);
        var report = new
        {
            SchemaVersion = 1, CreatedUtc = DateTimeOffset.UtcNow, Runtime = RuntimeInformation.FrameworkDescription,
            OS = RuntimeInformation.OSDescription, Training = options,
            Protocol = new { Split = "chronological 80/20", model.TrainingCount, model.ValidationCount,
                ScalerFit = "training prefix only", Features = "causal: current and previous frames only",
                Balancing = "equal samples per present class with replacement; training prefix only",
                LearningRate = "initial / (1 + 0.15 * (epoch - 1))",
                Selection = "best internal-validation macro-F1 checkpoint per seed, then best seed; ties keep earlier",
                TestEvaluatedAfterSelection = evaluation is not null, TestUsedForSelection = false },
            FeatureSchema = FeatureExtractor.Schema, FeatureNames = FeatureExtractor.FeatureNames,
            Inputs = new { Train = Fingerprint(trainPath), Config = Fingerprint(configPath),
                OpenTest = testPath is null ? null : Fingerprint(testPath) },
            Model = new { FileName = Path.GetFileName(paths[0]), Sha256 = Hash(paths[0]), model.Seed,
                model.ValidationMacroF1, model.SelectedEpoch,
                Layers = MultilayerPerceptron.FromJson(model.NetworkJson).LayerSizes },
            Attempts = trainer.Attempts, History = trainer.History, OpenTest = evaluation,
            MinimumMacroF1 = config.MinimumMacroF1,
            MeetsOpenTestMinimum = evaluation is null ? (bool?)null : evaluation.MacroF1 >= config.MinimumMacroF1
        };
        File.WriteAllText(paths[1], JsonSerializer.Serialize(report, new JsonSerializerOptions
            { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var lines = new List<string> { "seed,epoch,mean_balanced_training_loss,internal_validation_macro_F1" };
        lines.AddRange(trainer.History.Select(h => ExperimentExporter.CsvRow(h.Seed, h.Epoch,
            h.MeanBalancedTrainingLoss, h.ValidationMacroF1)));
        File.WriteAllLines(paths[2], lines);
        foreach (string path in paths) Console.WriteLine($"записано: {path}");
    }
    private static object Fingerprint(string path) => new { FileName = Path.GetFileName(path), Sha256 = Hash(path) };
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}

using System.Security.Cryptography;
using System.Text.Json;
using TelemetryLink.Core;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Optimization;
using TelemetryLink.Core.Pipeline;
namespace TelemetryLink.Cli.Commands;

public static class OptimizeCommand
{
    public static int Execute(CommandLineOptions options)
    {
        var context = new CommandContext(options);
        int budget = options.Value("budget", 60), seed = options.Value("seed", 1);
        if (budget < 1) throw new ArgumentException("--budget має бути додатним.");
        string[] names = options.List("optimizer", "ga", "random").Select(x => x.ToLowerInvariant()).ToArray();
        if (names.Length == 0 || names.Distinct().Count() != names.Length) throw new ArgumentException("Оптимізатори не можуть повторюватися.");
        var gaOptions = new GeneticOptions(options.Value("population", 12), options.Value("tournament", 3),
            options.Value("mutation", .2), options.Value("mutation-scale", .15), options.Value("elite", 1));
        _ = new GeneticOptimizer(gaOptions);
        var optimizers = names.Select(name => name == "ga" ? new GeneticOptimizer(gaOptions) : AlgorithmCatalog.Optimizer(name)).ToArray();
        string output = options.Value("out", "results/optimization.csv");
        if (!output.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("--out має закінчуватися на .csv.");
        string stem = Path.GetFullPath(output)[..^4];
        string configPath = options.Value("config", Path.Combine(CommandContext.DataDirectory, "team_config.json"));
        string sessionPath = options.Value("session", Path.Combine(CommandContext.DataDirectory, "session.csv"));
        string? modelPath = options.Value("model"), tuningPath = options.Value("tuning");
        string[] inputs = new[] { configPath, sessionPath, modelPath, tuningPath }.Where(x => x is not null).Select(x => Path.GetFullPath(x!)).ToArray();
        string[] outputs = new[] { stem + ".csv", stem + ".summary.json" }.Concat(names.Select(n => stem + "." + n + ".best.json")).ToArray();
        if (outputs.Any(x => inputs.Any(y => string.Equals(x, y, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))))
            throw new ArgumentException("Результати не можуть перезаписати вхідні файли.");
        var session = context.LoadSession();
        var scenario = context.Config.Scenario(options.Value("scenario", "emergency"));
        var pipeline = context.BuildPipeline();
        Console.WriteLine($"Сценарій {scenario.Name}; бюджет {budget} на кожен оптимізатор; channel-seed={context.ChannelSeed}.");
        Console.WriteLine("Fitness = Q повного прогону. Компонента T залежить від виміряного часу; однаковий seed не усуває часовий шум.");
        var results = new List<(string Name, OptimizationResult Result)>();
        foreach (var optimizer in optimizers)
        {
            int calls = 0;
            double Fitness(Tuning tuning)
            {
                var report = new SessionRunner(context.Config, pipeline with { Tuning = tuning }).Run(session, scenario, context.ChannelSeed);
                calls++;
                if (calls == 1 || calls % 20 == 0 || calls == budget) Console.WriteLine($"{optimizer.Name}: {calls}/{budget}, Q={report.Q:F5}");
                return report.Q;
            }
            var result = optimizer.Optimize(Fitness, budget, seed);
            if (calls != budget || result.Evaluations != budget) throw new InvalidOperationException("Порушено бюджет оцінок.");
            results.Add((optimizer.Name, result));
            Console.WriteLine($"{optimizer.Name}: best Q={result.BestFitness:F6}; {result.Best}; оцінок={calls}");
        }
        ReportRenderer.EnsureDirectory(output);
        var rows = new List<string> { "optimizer,generation,evaluations,best_fitness,mean_fitness" };
        foreach (var (name, result) in results)
        {
            rows.AddRange(result.History.Select(h => ExperimentExporter.CsvRow(name, h.Generation, h.Evaluations, h.BestFitness, h.MeanFitness)));
            File.WriteAllText(stem + "." + name + ".best.json", JsonSerializer.Serialize(result.Best, new JsonSerializerOptions { WriteIndented = true }));
        }
        File.WriteAllLines(output, rows);
        object Fingerprint(string path) => new { FileName = Path.GetFileName(path), Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() };
        var summary = new { SchemaVersion = 1, CreatedUtc = DateTimeOffset.UtcNow, BudgetPerOptimizer = budget, Seed = seed,
            context.ChannelSeed, Scenario = scenario.Name, Pipeline = pipeline.Describe(), GeneticOptions = gaOptions,
            Bounds = Tuning.Bounds.Select((b, i) => new { Gene = Tuning.GeneNames[i], b.Low, b.High }),
            Protocol = new { Fitness = "full-session Q", FixedChannelSeed = true, FitnessIsDeterministic = false,
                Timing = "Q includes measured elapsed time; no guarantee of GA superiority", EliteReevaluation = false,
                RandomMean = "all samples so far", GeneticMean = "current population, including cached elites",
                PartialGeneration = "stop exactly at budget; retain elites", PopulationOne = "elitist (1+1) strategy" },
            Inputs = new { Config = Fingerprint(configPath), Session = Fingerprint(sessionPath), Model = modelPath is null ? null : Fingerprint(modelPath) },
            Results = results.Select(x => new { Optimizer = x.Name, x.Result.Best, x.Result.BestFitness, x.Result.Evaluations, x.Result.History }) };
        File.WriteAllText(stem + ".summary.json", JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
        foreach (string path in outputs) Console.WriteLine($"записано: {path}");
        return 0;
    }
}

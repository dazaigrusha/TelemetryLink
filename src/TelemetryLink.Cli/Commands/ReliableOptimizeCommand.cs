using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TelemetryLink.Core;
using TelemetryLink.Core.Optimization;
using TelemetryLink.Core.Pipeline;
namespace TelemetryLink.Cli.Commands;

public static class ReliableOptimizeCommand
{
    public static int Execute(CommandLineOptions options)
    {
        var settings = ReliableOptimizationSettings.Parse(options);
        int budget = options.Value("budget", 60), seed = options.Value("seed", 1);
        if (budget < 1) throw new ArgumentException("--budget має бути додатним.");
        var gaOptions = new GeneticOptions(options.Value("population", 12), options.Value("tournament", 3),
            options.Value("mutation", .2), options.Value("mutation-scale", .15), options.Value("elite", 1));
        _ = new GeneticOptimizer(gaOptions);
        string[] names = options.List("optimizer", "ga", "random").Select(x => x.ToLowerInvariant()).ToArray();
        if (names.Length == 0 || names.Distinct().Count() != names.Length) throw new ArgumentException("Потрібні різні оптимізатори.");
        IParameterOptimizer[] optimizers = names.Select(n => n == "ga" ? new GeneticOptimizer(gaOptions) : AlgorithmCatalog.Optimizer(n)).ToArray();
        string output = options.Has("out") ? options.Require("out") : "results/reliable.csv";
        if (!output.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("--out має закінчуватися на .csv.");
        string stem = Path.GetFullPath(output)[..^4];
        string configPath = options.Value("config", Path.Combine(CommandContext.DataDirectory, "team_config.json"));
        string sessionPath = options.Value("session", Path.Combine(CommandContext.DataDirectory, "session.csv"));
        string? modelPath = options.Value("model"), tuningPath = options.Value("tuning");
        string[] outputs = new[] { stem + ".csv", stem + ".candidates.csv", stem + ".runs.csv", stem + ".summary.json" }
            .Concat(names.Select(n => stem + "." + n + ".best.json")).ToArray();
        string[] inputs = new[] { configPath, sessionPath, modelPath, tuningPath }.Where(p => p is not null).Select(p => Path.GetFullPath(p!)).ToArray();
        if (outputs.Any(x => inputs.Any(y => string.Equals(x, y, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))))
            throw new ArgumentException("Результати не можуть перезаписувати вхідні файли.");
        var context = new CommandContext(options);
        var scenarios = settings.Scenarios.Select(context.Config.Scenario).ToArray();
        var session = context.LoadSession(); var pipeline = context.BuildPipeline();
        _ = TransmissionPlan.Create([], [], pipeline); // Validate copy/gap ranges before expensive evaluation.
        Console.WriteLine($"Reliable: {budget} кандидатів на алгоритм, {settings.SearchRunsPerCandidate} повних прогонів на кандидата.");
        Console.WriteLine($"Пошук: {string.Join(',', settings.SearchSeeds)}; перевірка: {string.Join(',', settings.ValidationSeeds)}.");
        Console.WriteLine("Усі пороги й цілісність мають виконуватися у кожному прогоні. Q порівнюється серед допустимих кандидатів.");
        ReliabilityEvaluation Evaluate(Core.Configuration.Tuning tuning, int[] seeds)
            => ReliabilityFitness.Evaluate(tuning, scenarios, seeds, settings.Repeats, context.Config.CriticalLossThreshold,
                (candidate, scenario, channelSeed) => new SessionRunner(context.Config, pipeline with { Tuning = candidate }).Run(session, scenario, channelSeed));
        var result = ReliableOptimizationExperiment.Run(optimizers, budget, seed, pipeline.Tuning,
            t => Evaluate(t, settings.SearchSeeds), t => Evaluate(t, settings.ValidationSeeds),
            trial => Console.WriteLine($"{trial.Optimizer} {trial.Evaluation}/{budget}: feasible={trial.Result.Assessment.Feasible}, " +
                $"failed={trial.Result.Assessment.ThresholdFailures}, mean Q={trial.Result.Assessment.MeanQ:F5}, score={trial.Result.Assessment.Score:F5}"));
        var json = new JsonSerializerOptions { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
        ReportRenderer.EnsureDirectory(output);
        var history = new List<string> { "optimizer,generation,evaluations,best_score,mean_score" };
        foreach (var selected in result.Results)
        {
            history.AddRange(selected.Search.History.Select(h => ExperimentExporter.CsvRow(selected.Optimizer, h.Generation, h.Evaluations, h.BestFitness, h.MeanFitness)));
            File.WriteAllText(stem + "." + selected.Optimizer + ".best.json", JsonSerializer.Serialize(selected.Search.Best, json));
            Console.WriteLine($"{selected.Optimizer}: SEARCH feasible={selected.Training.Assessment.Feasible}, Q={selected.Training.Assessment.MeanQ:F5}; " +
                $"VALIDATION feasible={selected.Validation.Assessment.Feasible}, Q={selected.Validation.Assessment.MeanQ:F5}");
        }
        File.WriteAllLines(stem + ".csv", history);
        var candidates = new List<string> { "phase,optimizer,evaluation,window,threshold,critical_weight,feasible,score,mean_Q,integrity_failures,threshold_failures,cells,mean_excess,worst_excess" };
        var runs = new List<string> { "phase,optimizer,evaluation,window,threshold,critical_weight,scenario,channel_seed,repeat,Q,critical_loss,limit,within_threshold,exact_restore,receiver_crashed,critical,recovered_critical,sent_bits,elapsed_ms" };
        void Export(string phase, string optimizer, int index, ReliabilityEvaluation e)
        {
            var t = e.Tuning; var a = e.Assessment;
            candidates.Add(ExperimentExporter.CsvRow(phase, optimizer, index, t.WindowFrames, t.CompressionThreshold, t.CriticalWeight,
                a.Feasible, a.Score, a.MeanQ, a.IntegrityFailures, a.ThresholdFailures, a.Cells, a.MeanExcess, a.WorstExcess));
            runs.AddRange(e.Cells.Select(c => ExperimentExporter.CsvRow(phase, optimizer, index, t.WindowFrames, t.CompressionThreshold,
                t.CriticalWeight, c.Scenario, c.ChannelSeed, c.Repeat, c.Q, c.CriticalLoss, c.Limit,
                c.CriticalLoss <= c.Limit + 1e-9, c.ExactRestore, c.ReceiverCrashed, c.CriticalFrames, c.RecoveredCritical, c.SentBits, c.ElapsedMs)));
        }
        Export("baseline-search", "default", 0, result.BaselineTraining);
        foreach (var trial in result.Trials) Export("search", trial.Optimizer, trial.Evaluation, trial.Result);
        Export("validation", "default", 0, result.BaselineValidation);
        foreach (var winner in result.Results) Export("validation", winner.Optimizer, 0, winner.Validation);
        File.WriteAllLines(stem + ".candidates.csv", candidates); File.WriteAllLines(stem + ".runs.csv", runs);
        object Fingerprint(string path) => new { FileName = Path.GetFileName(path), Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() };
        var summary = new
        {
            SchemaVersion = 1, Objective = "reliable", CreatedUtc = DateTimeOffset.UtcNow,
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            BudgetPerOptimizer = budget, Seed = seed, Settings = settings, GeneticOptions = gaOptions,
            Pipeline = new { Codec = pipeline.Codec.Name, Packer = pipeline.Packer.Name, Scheduler = pipeline.Scheduler.Name,
                Classifier = pipeline.Classifier.Name, Delivery = DeliveryPolicies.Name(pipeline.Delivery), pipeline.CriticalCopies, pipeline.RepeatGapPackets, pipeline.DeviceId },
            Protocol = new { Selection = "search grid only; all winners frozen before validation", ValidationUsedForSelection = false,
                ThresholdRule = "every scenario/seed/repeat; loss <= limit + 1e-9; exact restore; no receiver crash",
                Score = "healthy feasible: 1+meanQ; healthy infeasible: -failed/N-meanExcess/(N+1); integrity failure: -3-bad/N",
                InfeasibleRanking = "fewer failed cells first, then smaller mean excess; Q does not rescue violations",
                FitnessRunsPerOptimizer = checked((long)budget * settings.SearchRunsPerCandidate),
                BaselineSearchRuns = settings.SearchRunsPerCandidate,
                ValidationRuns = checked((long)(result.Results.Count + 1) * settings.ValidationRunsPerCandidate),
                Cache = "none; every fitness callback runs full grid, even for duplicate candidates or failed thresholds",
                EliteReevaluation = false, FitnessIsDeterministic = false, Timing = "wall-clock Q is noisy; baseline search runs first, no additional warmup" },
            Scenarios = scenarios.Select(s => new { s.Name, CriticalLossLimit = context.Config.CriticalLossThreshold(s.Name) }),
            Inputs = new { Config = Fingerprint(configPath), Session = Fingerprint(sessionPath), Model = modelPath is null ? null : Fingerprint(modelPath),
                InitialTuning = tuningPath is null ? null : Fingerprint(tuningPath) },
            Baseline = new { Training = result.BaselineTraining.Assessment, Validation = result.BaselineValidation.Assessment, Tuning = result.BaselineTraining.Tuning },
            Results = result.Results.Select(x => new { x.Optimizer, x.Search.Best, x.Search.BestFitness, x.Search.Evaluations,
                x.Search.History, Training = x.Training.Assessment, Validation = x.Validation.Assessment,
                FoundFeasibleOnSearch = x.Training.Assessment.Feasible, PassedValidation = x.Validation.Assessment.Feasible })
        };
        File.WriteAllText(stem + ".summary.json", JsonSerializer.Serialize(summary, json));
        foreach (string path in outputs) Console.WriteLine($"записано: {path}");
        bool success = result.Results.All(x => x.Training.Assessment.Feasible && x.Validation.Assessment.Feasible);
        if (!success) Console.WriteLine("УВАГА: не всі переможці виконали пороги. best.json означає найкращий знайдений кандидат, а не гарантію допустимості.");
        return options.Has("strict") && !success ? 1 : 0;
    }
}

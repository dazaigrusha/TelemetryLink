using System.Globalization;
using System.Text;
using TelemetryLink.Core;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Optimization;
using TelemetryLink.Core.Pipeline;

namespace TelemetryLink.Cli.Commands;

/// <summary>
/// Підбір параметрів тракту. Функція пристосованості — метрика Q повного
/// прогону, тобто рівно та сама метрика, за якою порівнюються команди.
///
/// Кілька оптимізаторів через кому запускаються з ОДНАКОВИМ бюджетом
/// обчислень: без цього порівняння ГА з випадковим пошуком нічого не означає.
/// </summary>
public static class OptimizeCommand
{
    public static int Execute(CommandLineOptions options)
    {
        var context = new CommandContext(options);
        var session = context.LoadSession();
        var scenario = context.Config.Scenario(options.Value("scenario", "emergency"));

        int budget = options.Value("budget", 60);
        int seed = options.Value("seed", 1);
        string[] names = options.List("optimizer", "ga", "random");

        Console.WriteLine();
        Console.WriteLine($"оптимізація на сценарії «{scenario.Name}», бюджет {budget} обчислень пристосованості");
        Console.WriteLine();

        var results = new List<(string Name, OptimizationResult Result)>();

        foreach (string name in names)
        {
            var optimizer = AlgorithmCatalog.Optimizer(name);
            int evaluations = 0;

            double Fitness(Tuning tuning)
            {
                evaluations++;
                var pipeline = context.BuildPipeline(tuning: tuning);
                return new SessionRunner(context.Config, pipeline)
                    .Run(session, scenario, context.ChannelSeed).Q;
            }

            var result = optimizer.Optimize(Fitness, budget, seed);
            results.Add((optimizer.Name, result));

            Console.WriteLine($"{optimizer.Name,-8} Q = {result.BestFitness:F4}   {result.Best}   " +
                              $"({evaluations} обчислень)");
        }

        // Перевірка елітизму: найкращий результат останнього покоління не
        // може бути гіршим за найкращий результат покоління 0.
        foreach (var (name, result) in results.Where(r => r.Result.History.Count > 1))
        {
            var first = result.History[0];
            var last = result.History[^1];
            Console.WriteLine($"{name,-8} покоління 0: краще {first.BestFitness:F4}, " +
                              $"останнє: краще {last.BestFitness:F4}   " +
                              $"{(last.BestFitness >= first.BestFitness - 1e-12 ? "OK" : "ПОРУШЕНО ЕЛІТИЗМ")}");
        }

        WriteHistory(options.Value("out", "results/optimization.csv"), results);
        Console.WriteLine();
        return 0;
    }

    private static void WriteHistory(string path, IReadOnlyList<(string Name, OptimizationResult Result)> results)
    {
        var builder = new StringBuilder("optimizer,generation,best_fitness,mean_fitness\n");

        foreach (var (name, result) in results)
            foreach (var record in result.History)
                builder.AppendLine(string.Join(',', name, record.Generation,
                    record.BestFitness.ToString("F6", CultureInfo.InvariantCulture),
                    record.MeanFitness.ToString("F6", CultureInfo.InvariantCulture)));

        ReportRenderer.EnsureDirectory(path);
        File.WriteAllText(path, builder.ToString());
        Console.WriteLine($"журнал еволюції: {path}");
    }
}

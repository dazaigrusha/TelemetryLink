using TelemetryLink.Core.Metrics;
namespace TelemetryLink.Cli.Commands;
public static class ExperimentCommand
{
    public static int Execute(CommandLineOptions options)
    {
        string name = options.Value("name", "order");
        if (name != "order") throw new ArgumentException($"Невідомий експеримент '{name}'. Доступні: order.");
        var context = new CommandContext(options);
        string? output = options.Has("out") ? options.Require("out") : null;
        if (output is not null)
        {
            if (!output.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("--out має закінчуватися на .csv.");
            var inputs = new[] { options.Value("session",Path.Combine(CommandContext.DataDirectory,"session.csv")),
                options.Value("config",Path.Combine(CommandContext.DataDirectory,"team_config.json")) };
            if (inputs.Any(x => string.Equals(Path.GetFullPath(x),Path.GetFullPath(output),OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)))
                throw new ArgumentException("Результат не може перезаписати вхідний файл.");
        }
        var measurements = OrderOfOperationsExperiment.Run(context.LoadSession().Frames, context.Config.SessionPassword);
        Console.WriteLine("Порядок операцій: потік із доповненням + таблиця (2 байти на символ).");
        foreach(var m in measurements) Console.WriteLine($"{m.Order}: raw={m.RawBits}, table={m.TableBits}, stream={m.BitstreamBits}, result={m.ResultBits}, ratio={m.Ratio:F4}, exact={m.ExactRestore}");
        Console.WriteLine("Без каналу й спільних витрат заголовка, salt, nonce та HMAC. Це не пакет для передачі.");
        Console.WriteLine("Випадкові salt/nonce оновлюються на запуск; обсяг encrypt-first може трохи змінюватися.");
        if(output is not null)
        {
            ReportRenderer.EnsureDirectory(output);
            File.WriteAllLines(output,new[]{"order,raw_bits,table_bits,bitstream_bits,result_bits,ratio,exact_restore"}.Concat(
                measurements.Select(m=>ExperimentExporter.CsvRow(m.Order,m.RawBits,m.TableBits,m.BitstreamBits,m.ResultBits,m.Ratio,m.ExactRestore))));
            Console.WriteLine($"записано: {output}");
        }
        return measurements.All(m=>m.ExactRestore)?0:1;
    }
}

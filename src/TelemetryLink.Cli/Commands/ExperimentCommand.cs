using TelemetryLink.Core.Metrics;

namespace TelemetryLink.Cli.Commands;

/// <summary>Окремі експерименти звіту, які не зводяться до прогону тракту.</summary>
public static class ExperimentCommand
{
    public static int Execute(CommandLineOptions options)
    {
        var context = new CommandContext(options);
        string name = options.Value("name", "order");

        return name switch
        {
            "order" => OrderOfOperations(context),
            _ => Unknown(name)
        };
    }

    private static int OrderOfOperations(CommandContext context)
    {
        var session = context.LoadSession();
        var measurements = OrderOfOperationsExperiment.Run(session.Frames, context.Config.SessionPassword);

        Console.WriteLine();
        Console.WriteLine("порядок операцій: коефіцієнт стиснення (таблиця 3 звіту)");
        Console.WriteLine();
        Console.WriteLine($"{"схема",-20}{"сирих біт",12}{"після обробки",16}{"коефіцієнт",12}");
        Console.WriteLine(new string('-', 60));

        foreach (var measurement in measurements)
            Console.WriteLine($"{measurement.Order,-20}{measurement.RawBits,12}{measurement.ResultBits,16}" +
                              $"{measurement.Ratio,12:F3}");

        Console.WriteLine();
        Console.WriteLine("Шифротекст статистично рівномірний, тому Гаффман на ньому виграшу не дає");
        Console.WriteLine("і ще витрачає біти на таблицю. Тому стиснення йде ПЕРЕД шифруванням.");
        Console.WriteLine();
        return 0;
    }

    private static int Unknown(string name)
    {
        Console.Error.WriteLine($"Невідомий експеримент '{name}'. Доступні: order.");
        return 2;
    }
}

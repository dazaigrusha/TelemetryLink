using TelemetryLink.Core.Pipeline;

namespace TelemetryLink.Cli.Commands;

/// <summary>Один прогін тракту з друком метрики Q.</summary>
public static class RunCommand
{
    public static int Execute(CommandLineOptions options)
    {
        var context = new CommandContext(options);
        var session = context.LoadSession();
        var pipeline = context.BuildPipeline();

        string scenarioName = options.Value("scenario", "working");
        var scenario = context.Config.Scenario(scenarioName);

        Console.WriteLine($"TelemetryLink · {context.Config.Describe()} · seed каналу {context.ChannelSeed}");

        var report = new SessionRunner(context.Config, pipeline).Run(session, scenario, context.ChannelSeed);
        ReportRenderer.PrintRun(report, context.Config);

        if (options.Has("out")) ReportRenderer.WriteCsv(options.Require("out"), [report]);

        // Ненульовий код повернення потрібен CI. Порушення побайтового
        // відновлення — помилка завжди. Поріг втрати критичних кадрів валить
        // збірку лише з --strict: в аварійному сценарії базова система в нього
        // не вкладається, і це очікуваний вимірюваний результат, а не помилка
        // (кейс C1 №5 вимагає лише відсутності аварійного завершення).
        double threshold = context.Config.CriticalLossThreshold(scenarioName);
        bool withinThreshold = report.CriticalLossRatio <= threshold + 1e-9;

        if (!report.ExactRestore) return 1;
        return options.Has("strict") && !withinThreshold ? 1 : 0;
    }
}

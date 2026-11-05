using System.Globalization;
using System.Text;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Metrics;

namespace TelemetryLink.Cli;

/// <summary>
/// Друк результатів у термінал і експорт у CSV.
///
/// Увесь вивід зосереджений тут: у Core немає жодного Console.WriteLine.
/// Саме це дозволяє тестам викликати ті самі методи, що й CLI.
/// </summary>
public static class ReportRenderer
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static void PrintRun(SessionReport report, TeamConfig config)
    {
        Console.WriteLine();
        Console.WriteLine($"сценарій: {report.ScenarioName}   {report.CodecName} · {report.PackerName} · " +
                          $"{report.SchedulerName} · {report.ClassifierName}");
        Console.WriteLine($"параметри: {report.Tuning}");
        Console.WriteLine();

        var channel = report.Channel;
        PrintPair("кадрів на вході", report.FrameCount, "пакетів сформовано", report.PacketsBuilt);
        PrintPair("сирих біт (B_ref)", report.ReferenceBits, "надіслано в канал", channel.Sent);
        PrintPair("передано біт (B_sent)", report.SentBits, "втрачено в каналі", channel.Lost);
        PrintPair("критичних кадрів", report.CriticalFrameCount, "поза бюджетом", channel.OverBudget);
        PrintPair("відновлено критичних", report.RecoveredCriticalFrameCount, "пакетів прийнято", report.PacketsAccepted);
        PrintPair("відновлено кадрів", report.RecoveredFrameCount, "коефіцієнт стиснення",
                  report.CompressionRatio.ToString("F3", Culture));

        Console.WriteLine();
        Console.WriteLine($"  S = {report.S:F4}    R = {report.R:F4}    T = {report.T:F4}");
        Console.WriteLine($"  Q = {config.Alpha}·{report.S:F4} + {config.Beta}·{report.R:F4} + " +
                          $"{config.Gamma}·{report.T:F4} = {report.Q:F4}");
        Console.WriteLine();

        double threshold = config.CriticalLossThreshold(report.ScenarioName);
        bool withinThreshold = report.CriticalLossRatio <= threshold + 1e-9;
        Console.WriteLine($"  {(withinThreshold ? "OK  " : "УВАГА")} поріг втрати критичних кадрів {threshold:P1}" +
                          $"   фактично {report.CriticalLossRatio:P1}");

        Console.WriteLine($"  {(report.ExactRestore ? "OK  " : "УВАГА")} побайтове відновлення доставлених пакетів");

        if (report.Rejections.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  відмови приймача: " +
                              string.Join(", ", report.Rejections.Select(pair => $"{pair.Key} × {pair.Value}")));
        }

        Console.WriteLine();
    }

    private static void PrintPair(string leftLabel, object leftValue, string rightLabel, object rightValue) =>
        Console.WriteLine($"  {leftLabel,-24}{leftValue,10}        {rightLabel,-24}{rightValue,8}");

    /// <summary>Таблиця порівняння з колонкою Δ відносно першого варіанта в групі.</summary>
    /// <param name="dimension">Що саме змінюється: codec, packer, scheduler, classifier.</param>
    /// <param name="caption">Людський опис для заголовка.</param>
    public static void PrintComparison(string dimension, string caption, IReadOnlyList<SessionReport> reports)
    {
        Console.WriteLine();
        Console.WriteLine($"порівняння: {caption};  решта тракту незмінна");
        Console.WriteLine();
        Console.WriteLine($"{"сценарій",-12}{"варіант",-12}{"дійшло",10}{"біт",11}{"час,мс",9}" +
                          $"{"S",9}{"R",9}{"Q",9}{"Δ",10}");
        Console.WriteLine(new string('-', 91));

        double? baseline = null;
        string? currentScenario = null;

        foreach (var report in reports)
        {
            if (report.ScenarioName != currentScenario)
            {
                currentScenario = report.ScenarioName;
                baseline = null;
            }

            string delta = baseline is null ? "—" : (report.Q - baseline.Value).ToString("+0.0000;-0.0000;0.0000", Culture);
            baseline ??= report.Q;

            Console.WriteLine($"{report.ScenarioName,-12}{VariantOf(dimension, report),-12}" +
                              $"{report.DeliveredValue,10}{report.SentBits,11}{report.ElapsedSeconds * 1000,9:F0}" +
                              $"{report.S,9:F4}{report.R,9:F4}{report.Q,9:F4}{delta,10}");
        }

        PrintBars(dimension, reports);
    }

    private static void PrintBars(string dimension, IReadOnlyList<SessionReport> reports)
    {
        Console.WriteLine();
        Console.WriteLine("Q за сценаріями:");

        double maximum = Math.Max(reports.Max(r => r.Q), 1e-9);

        foreach (var group in reports.GroupBy(r => r.ScenarioName))
        {
            bool first = true;
            foreach (var report in group)
            {
                string scenario = first ? group.Key : "";
                first = false;
                int width = (int)Math.Round(report.Q / maximum * 40);
                Console.WriteLine($"  {scenario,-12}{VariantOf(dimension, report),-12}" +
                                  $"{new string('#', width)}  {report.Q:F4}");
            }
        }

        Console.WriteLine();
    }

    private static string VariantOf(string dimension, SessionReport report) => dimension switch
    {
        "codec" => report.CodecName,
        "packer" => report.PackerName,
        "scheduler" => report.SchedulerName,
        "classifier" => report.ClassifierName,
        _ => report.PackerName
    };

    public static void WriteCsv(string path, IReadOnlyList<SessionReport> reports)
    {
        var builder = new StringBuilder();
        builder.AppendLine("scenario,codec,packer,scheduler,classifier,frames,critical,recovered_critical," +
                           "reference_bits,sent_bits,elapsed_ms,packets_built,packets_accepted,value,delivered_value,compression_ratio,S,R,T,Q");

        foreach (var r in reports)
            builder.AppendLine(string.Join(',',
                r.ScenarioName, r.CodecName, r.PackerName, r.SchedulerName, r.ClassifierName,
                r.FrameCount, r.CriticalFrameCount, r.RecoveredCriticalFrameCount,
                r.ReferenceBits, r.SentBits,
                (r.ElapsedSeconds * 1000).ToString("F1", Culture),
                r.PacketsBuilt, r.PacketsAccepted, r.SelectedValue, r.DeliveredValue,
                r.CompressionRatio.ToString("F4", Culture),
                r.S.ToString("F6", Culture), r.R.ToString("F6", Culture),
                r.T.ToString("F6", Culture), r.Q.ToString("F6", Culture)));

        EnsureDirectory(path);
        File.WriteAllText(path, builder.ToString());
        Console.WriteLine($"записано: {path}");
    }

    public static void EnsureDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    }
}

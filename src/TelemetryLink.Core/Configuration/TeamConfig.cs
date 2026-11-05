using System.Globalization;
using System.Text.Json;
using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Configuration;

/// <summary>
/// Конфігурація команди з team_config.json.
///
/// Ключі у файлі українські, тому читання зроблено явним розбором
/// JsonDocument, а не атрибутами серіалізації: так видно, звідки береться
/// кожне число, і помилка в імені ключа знаходиться одразу.
///
/// Усі значення тут — умова задачі. Змінювати їх не можна: пріоритети кадрів
/// і коефіцієнти Q однакові для всіх команд, інакше розв'язки непорівнянні.
/// </summary>
public sealed class TeamConfig
{
    public required string Team { get; init; }
    public required int Seed { get; init; }
    public required string SessionPassword { get; init; }

    // --- метрика Q ---
    public required double Alpha { get; init; }
    public required double Beta { get; init; }
    public required double Gamma { get; init; }
    public required double ReferenceTimeSeconds { get; init; }

    // --- пакування ---
    public required int MaxPacketBytes { get; init; }
    public required int MaxPayloadBytes { get; init; }
    public required int CandidateWindowFrames { get; init; }

    // --- цінність кадру ---
    public required int PriorityNormal { get; init; }
    public required int PriorityTransition { get; init; }
    public required int PriorityAnomaly { get; init; }
    public required int CriticalBonus { get; init; }

    // --- канал ---
    public required int ChannelSeed { get; init; }
    public required IReadOnlyDictionary<string, ScenarioConfig> Scenarios { get; init; }
    public required IReadOnlyDictionary<string, double> CriticalLossThresholds { get; init; }

    // --- інше ---
    public required double FullRunTimeLimitSeconds { get; init; }
    public required double MinimumMacroF1 { get; init; }

    /// <summary>
    /// v_i = пріоритет(клас_i) + critical_бонус × [critical_i == 1].
    /// Це і є цінність кадру для задачі про рюкзак.
    /// </summary>
    public int ValueOf(FrameClass frameClass, bool isCritical)
    {
        int priority = frameClass switch
        {
            FrameClass.Normal => PriorityNormal,
            FrameClass.Anomaly => PriorityAnomaly,
            FrameClass.Transition => PriorityTransition,
            _ => PriorityNormal
        };
        return priority + (isCritical ? CriticalBonus : 0);
    }

    public ScenarioConfig Scenario(string name) =>
        Scenarios.TryGetValue(name, out var scenario)
            ? scenario
            : throw new ArgumentException(
                $"Невідомий сценарій '{name}'. Доступні: {string.Join(", ", Scenarios.Keys)}.", nameof(name));

    public double CriticalLossThreshold(string scenarioName) =>
        CriticalLossThresholds.TryGetValue(scenarioName, out double threshold) ? threshold : 1.0;

    // Англійські імена сценаріїв для CLI; у файлі вони українські.
    private static readonly (string English, string Ukrainian)[] ScenarioNames =
    [
        ("calm", "спокійний"),
        ("working", "робочий"),
        ("emergency", "аварійний")
    ];

    public static TeamConfig Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        var metric = root.GetProperty("метрика_Q");
        var packing = root.GetProperty("пакування");
        var priorities = root.GetProperty("пріоритети_кадрів");
        var channel = root.GetProperty("канал");
        var scenariosNode = channel.GetProperty("сценарії");
        var thresholds = root.GetProperty("пороги_втрати_критичних_кадрів");
        var perceptron = root.GetProperty("перцептрон");

        var scenarios = new Dictionary<string, ScenarioConfig>(StringComparer.OrdinalIgnoreCase);
        var lossThresholds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (english, ukrainian) in ScenarioNames)
        {
            if (!scenariosNode.TryGetProperty(ukrainian, out var node)) continue;
            scenarios[english] = ReadScenario(english, ukrainian, node);
            if (thresholds.TryGetProperty(ukrainian, out var threshold))
                lossThresholds[english] = threshold.GetDouble();
        }

        return new TeamConfig
        {
            Team = root.GetProperty("команда").GetString() ?? "?",
            Seed = root.GetProperty("seed").GetInt32(),
            SessionPassword = root.GetProperty("пароль_сеансу").GetString() ?? "",

            Alpha = metric.GetProperty("alpha").GetDouble(),
            Beta = metric.GetProperty("beta").GetDouble(),
            Gamma = metric.GetProperty("gamma").GetDouble(),
            ReferenceTimeSeconds = metric.GetProperty("t_ref_секунд").GetDouble(),

            MaxPacketBytes = packing.GetProperty("max_packet_bytes").GetInt32(),
            MaxPayloadBytes = packing.GetProperty("max_payload_bytes").GetInt32(),
            CandidateWindowFrames = packing.GetProperty("candidate_window_frames").GetInt32(),

            PriorityNormal = priorities.GetProperty("норма").GetInt32(),
            PriorityTransition = priorities.GetProperty("перехідний_режим").GetInt32(),
            PriorityAnomaly = priorities.GetProperty("аномалія").GetInt32(),
            CriticalBonus = priorities.GetProperty("critical_бонус").GetInt32(),

            ChannelSeed = channel.GetProperty("seed_моделі_каналу").GetInt32(),
            Scenarios = scenarios,
            CriticalLossThresholds = lossThresholds,

            FullRunTimeLimitSeconds = root.GetProperty("ліміт_часу_повного_прогону_секунд").GetDouble(),
            MinimumMacroF1 = perceptron.GetProperty("F1_min").GetDouble()
        };
    }

    private static ScenarioConfig ReadScenario(string english, string ukrainian, JsonElement node)
    {
        var delay = node.GetProperty("delay_ms");
        string kind = delay.GetProperty("тип").GetString() ?? "стала";

        double burst = 1.0;
        if (node.TryGetProperty("втрати_пачками", out var burstNode) &&
            burstNode.TryGetProperty("середня_довжина_пачки", out var burstLength))
            burst = burstLength.GetDouble();

        return new ScenarioConfig(
            Name: english,
            UkrainianName: ukrainian,
            LossProbability: node.GetProperty("loss_probability").GetDouble(),
            BurstMeanLength: burst,
            DelayKind: kind == "стала" ? DelayKind.Constant : DelayKind.Uniform,
            DelayConstantMs: delay.TryGetProperty("значення", out var v) ? v.GetDouble() : 0,
            DelayLowMs: delay.TryGetProperty("від", out var lo) ? lo.GetDouble() : 0,
            DelayHighMs: delay.TryGetProperty("до", out var hi) ? hi.GetDouble() : 0,
            BandwidthBitsPerSecond: node.GetProperty("bandwidth_bps").GetDouble(),
            TransmissionBudgetSeconds: node.TryGetProperty("transmission_budget_s", out var budget)
                ? budget.GetDouble()
                : double.PositiveInfinity);
    }

    public string Describe() =>
        string.Create(CultureInfo.InvariantCulture,
            $"команда {Team}, seed {Seed}, Q = {Alpha}·S + {Beta}·R + {Gamma}·T, t_ref = {ReferenceTimeSeconds} c");
}

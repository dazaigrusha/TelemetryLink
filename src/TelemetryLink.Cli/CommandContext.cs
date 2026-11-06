using TelemetryLink.Core;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.InputOutput;
using TelemetryLink.Core.Pipeline;
using TelemetryLink.Core.Prediction;

namespace TelemetryLink.Cli;

/// <summary>
/// Спільна для всіх команд частина: звідки взяти конфігурацію, дані і модель,
/// і як зібрати тракт із імен алгоритмів.
///
/// Тут і тільки тут імена з командного рядка перетворюються на об'єкти.
/// </summary>
public sealed class CommandContext
{
    private readonly CommandLineOptions _options;
    private ClassifierModel? _model;

    public CommandContext(CommandLineOptions options)
    {
        _options = options;
        string configPath = options.Value("config", Path.Combine(DataDirectory, "team_config.json"));
        Config = TeamConfig.Load(configPath);
    }

    public TeamConfig Config { get; }

    /// <summary>Каталог із комплектом викладача. Типово data/ поруч із репозиторієм.</summary>
    public static string DataDirectory => Environment.GetEnvironmentVariable("TLM_DATA") ?? "data";

    public TelemetrySession LoadSession()
    {
        string path = _options.Value("session", Path.Combine(DataDirectory, "session.csv"));
        return path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)
            ? TelemetryBinaryReader.Read(path)
            : TelemetryCsvReader.Read(path);
    }

    public ClassifierModel? LoadModel()
    {
        if (_model is not null) return _model;

        string? path = _options.Value("model");
        if (path is null) return null;

        return _model = ClassifierModel.Load(path);
    }

    /// <summary>
    /// Збирає тракт. Перевизначення дозволяє команді compare міняти рівно
    /// один компонент, лишаючи решту незмінною.
    /// </summary>
    public PipelineOptions BuildPipeline(
        string? codec = null, string? packer = null, string? scheduler = null, string? classifier = null,
        Tuning? tuning = null) => new()
        {
            Codec = AlgorithmCatalog.Codec(codec ?? _options.Value("codec", "huffman")),
            Packer = AlgorithmCatalog.Packer(packer ?? _options.Value("packer", "dp")),
            Scheduler = AlgorithmCatalog.Scheduler(scheduler ?? _options.Value("scheduler", "sequential")),
            Classifier = AlgorithmCatalog.Classifier(
                classifier ?? _options.Value("classifier", "majority"), LoadModel()),
            Tuning = tuning ?? CurrentTuning
        };

    public Tuning CurrentTuning => new(
        _options.Value("window", Config.CandidateWindowFrames),
        _options.Value("threshold", Tuning.Default.CompressionThreshold),
        _options.Value("critical-weight", Tuning.Default.CriticalWeight));

    public int ChannelSeed => _options.Value("channel-seed", Config.ChannelSeed);

    public string ReferenceDirectory => _options.Value("reference", Path.Combine(DataDirectory, "reference"));
}

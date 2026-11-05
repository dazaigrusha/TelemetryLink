using TelemetryLink.Core.Compression;
using TelemetryLink.Core.Optimization;
using TelemetryLink.Core.Packing;
using TelemetryLink.Core.Prediction;
using TelemetryLink.Core.Scheduling;

namespace TelemetryLink.Core;

/// <summary>
/// Реєстр алгоритмів: «ім'я з командного рядка» → реалізація.
///
/// Це єдине місце, де система знає перелік конкретних класів. Додати новий
/// алгоритм — це новий файл у відповідному каталозі плюс ОДИН рядок тут.
/// Тракт, формат, криптографія і CLI при цьому не змінюються.
///
/// Саме завдяки реєстру працює головна вимога до скелета: запускати систему
/// з різними алгоритмами, вказуючи їх параметрами запуску.
/// </summary>
public static class AlgorithmCatalog
{
    private static readonly Dictionary<string, Func<ISymbolCodec>> Codecs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["huffman"] = () => new HuffmanCodec(),
        ["none"] = () => new NoCompressionCodec()
        // ["fgk"]        = () => new AdaptiveHuffmanCodec(),   // просунутий рівень
        // ["arithmetic"] = () => new ArithmeticCodec(),        // експертний рівень
    };

    private static readonly Dictionary<string, Func<IFramePacker>> Packers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dp"] = () => new DynamicProgrammingPacker(),
        ["greedy"] = () => new GreedyRatioPacker(),
        ["sequential"] = () => new SequentialPacker(),
        ["brute"] = () => new BruteForcePacker()
    };

    private static readonly Dictionary<string, Func<ISendScheduler>> Schedulers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["greedy"] = () => new ValueDensityScheduler(),
        ["sequential"] = () => new SequentialScheduler()
    };

    private static readonly Dictionary<string, Func<IParameterOptimizer>> Optimizers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ga"] = () => new GeneticOptimizer(),
        ["random"] = () => new RandomSearchOptimizer()
    };

    public static IEnumerable<string> CodecNames => Codecs.Keys;
    public static IEnumerable<string> PackerNames => Packers.Keys;
    public static IEnumerable<string> SchedulerNames => Schedulers.Keys;
    public static IEnumerable<string> ClassifierNames => ["majority", "mlp"];
    public static IEnumerable<string> OptimizerNames => Optimizers.Keys;

    public static ISymbolCodec Codec(string name) => Create(Codecs, name, "кодек");
    public static IFramePacker Packer(string name) => Create(Packers, name, "пакувальник");
    public static ISendScheduler Scheduler(string name) => Create(Schedulers, name, "планувальник");
    public static IParameterOptimizer Optimizer(string name) => Create(Optimizers, name, "оптимізатор");

    /// <summary>
    /// Класифікатор mlp потребує навченої моделі. Без неї запуск має
    /// зупинитися зрозумілим повідомленням, а не тихо підставити базову лінію.
    /// </summary>
    public static IFrameClassifier Classifier(string name, ClassifierModel? model = null) => name.ToLowerInvariant() switch
    {
        "majority" => new MajorityClassifier(),
        "mlp" => model is not null
            ? new PerceptronClassifier(model)
            : throw new ArgumentException("Класифікатор mlp потребує --model. Спочатку виконайте: tlm train"),
        _ => throw new ArgumentException(
            $"Невідомий класифікатор '{name}'. Доступні: {string.Join(", ", ClassifierNames)}.")
    };

    private static T Create<T>(Dictionary<string, Func<T>> registry, string name, string kind) =>
        registry.TryGetValue(name, out var factory)
            ? factory()
            : throw new ArgumentException(
                $"Невідомий {kind} '{name}'. Доступні: {string.Join(", ", registry.Keys)}.");
}

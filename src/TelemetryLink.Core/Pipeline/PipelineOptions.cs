using TelemetryLink.Core.Compression;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Packing;
using TelemetryLink.Core.Prediction;
using TelemetryLink.Core.Scheduling;

namespace TelemetryLink.Core.Pipeline;

/// <summary>
/// Склад тракту на один прогін: які саме реалізації стоять у п'яти точках
/// підключення і з якими параметрами.
///
/// Це єдине місце, де «збирається» система. Усе інше працює через інтерфейси
/// і не знає, який саме алгоритм підставлено.
/// </summary>
public sealed record PipelineOptions
{
    public required ISymbolCodec Codec { get; init; }
    public required IFramePacker Packer { get; init; }
    public required ISendScheduler Scheduler { get; init; }
    public required IFrameClassifier Classifier { get; init; }
    public Tuning Tuning { get; init; } = Tuning.Default;

    /// <summary>
    /// Ідентифікатор апарата в заголовку пакета. На результат не впливає,
    /// потрібен для відповідності формату.
    /// </summary>
    public ushort DeviceId { get; init; } = 1;

    public string Describe() =>
        $"codec={Codec.Name}  packer={Packer.Name}  scheduler={Scheduler.Name}  classifier={Classifier.Name}";
}

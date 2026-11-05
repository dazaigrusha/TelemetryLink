using TelemetryLink.Core.Channel;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Format;

namespace TelemetryLink.Core.Metrics;

/// <summary>
/// Єдина метрика якості (умова, розділ 4).
///
///     S = B_ref / (B_ref + B_sent)      ефективність передавання
///     R = частка відновлених критичних кадрів
///     T = t_ref / (t_ref + t)           швидкодія
///     Q = α·S + β·R + γ·T,   α + β + γ = 1
///
/// Усі компоненти нормовані в [0,1], тому жодна не може домінувати над іншими.
/// Якщо приймач аварійно завершується або не відновлює жодного критичного
/// кадру, Q = 0.
/// </summary>
public static class QualityMetric
{
    // TODO(S6). Три однорядкові формули і одна згортка. Пастки — у крайніх
    // випадках, і саме вони перевіряються тестами:
    //   • B_ref + B_sent == 0        не ділити на нуль
    //   • критичних кадрів у сеансі немає  → R = 1, а не NaN
    //   • миттєвий прогін (t = 0)    → T = 1

    public static double TransmissionEfficiency(long referenceBits, long sentBits)
        => throw new NotImplementedException("QualityMetric.TransmissionEfficiency");

    public static double CriticalRecovery(int recoveredCritical, int totalCritical)
        => throw new NotImplementedException("QualityMetric.CriticalRecovery");

    public static double Speed(double referenceSeconds, double elapsedSeconds)
        => throw new NotImplementedException("QualityMetric.Speed");

    public static double Combine(TeamConfig config, double s, double r, double t)
        => throw new NotImplementedException("QualityMetric.Combine");
}

/// <summary>Підсумок одного повного прогону. Усе, що потрапляє у звіт і в CSV.</summary>
public sealed record SessionReport
{
    public required string ScenarioName { get; init; }
    public required string CodecName { get; init; }
    public required string PackerName { get; init; }
    public required string SchedulerName { get; init; }
    public required string ClassifierName { get; init; }
    public required Tuning Tuning { get; init; }

    public required int FrameCount { get; init; }
    public required int CriticalFrameCount { get; init; }
    public required int RecoveredFrameCount { get; init; }
    public required int RecoveredCriticalFrameCount { get; init; }

    public required long ReferenceBits { get; init; }
    public required long SentBits { get; init; }

    /// <summary>Сумарний розмір УСІХ сформованих пакетів — база для коефіцієнта стиснення.</summary>
    public required long BuiltBits { get; init; }

    /// <summary>Скільки кадрів узагалі потрапило в пакети.</summary>
    public required int PackedFrameCount { get; init; }

    public required double ElapsedSeconds { get; init; }

    public required int PacketsBuilt { get; init; }
    public required int PacketsAccepted { get; init; }
    /// <summary>Сумарна цінність кадрів у ВСІХ сформованих пакетах.</summary>
    public required int SelectedValue { get; init; }

    /// <summary>
    /// Цінність, яка реально дійшла до приймача. Саме це число порівнює
    /// пакувальники: коли бюджет обмежує канал, виграє той, хто вклав у ранні
    /// пакети більше цінного.
    /// </summary>
    public required int DeliveredValue { get; init; }

    /// <summary>Усі доставлені пакети відновилися ПОБАЙТОВО точно.</summary>
    public required bool ExactRestore { get; init; }

    public required ChannelStatistics Channel { get; init; }
    public required IReadOnlyDictionary<RejectionCode, int> Rejections { get; init; }

    public required double S { get; init; }
    public required double R { get; init; }
    public required double T { get; init; }
    public required double Q { get; init; }

    /// <summary>
    /// Коефіцієнт стиснення: сирі байти запакованих кадрів до розміру пакетів,
    /// що їх несуть. Рахується за СФОРМОВАНИМИ пакетами, а не за надісланими:
    /// інакше число залежало б від втрат у каналі й нічого не говорило б про
    /// якість стиснення. Значення &lt; 1 означає, що накладні витрати (43 байти
    /// заголовка, 32 байти HMAC, таблиця кодів) перевищують виграш.
    /// </summary>
    public double CompressionRatio =>
        BuiltBits == 0 ? 0.0 : (double)PackedFrameCount * Domain.TelemetryFrame.SizeInBytes * 8 / BuiltBits;

    /// <summary>Частка ВТРАЧЕНИХ критичних кадрів — її порівнюють із порогом викладача.</summary>
    public double CriticalLossRatio =>
        CriticalFrameCount == 0 ? 0.0 : 1.0 - (double)RecoveredCriticalFrameCount / CriticalFrameCount;
}

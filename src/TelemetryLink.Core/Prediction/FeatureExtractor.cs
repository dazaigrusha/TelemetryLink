using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Prediction;

/// <summary>
/// Обчислення ознак кадру.
///
/// Ознаки ОБОВ'ЯЗКОВО включають вікно історії. Клас «аномалія» розпізнається
/// переважно за миттєвою вібрацією, але клас «перехідний режим» — ТІЛЬКИ за
/// часовими ознаками: нестабільність mode, дрейф висоти й температури.
/// Класифікатор на полях одного кадру порогу F1_min не досягає, і це не
/// прихована умова, а частина базового завдання.
///
/// Поле critical подавати на вхід МОЖНА: воно задається правилами доставки
/// (кожен 10-й кадр опорний плюс висота нижче 300 дм) і з класом не корелює —
/// витоку мітки це не дає.
///
/// КРОК S7.
/// </summary>
public sealed class FeatureExtractor
{
    /// <summary>Глибина вікна історії. Потенційний ген хромосоми ГА.</summary>
    public int WindowSize { get; }

    public FeatureExtractor(int windowSize = 8)
    {
        if (windowSize < 1) throw new ArgumentOutOfRangeException(nameof(windowSize));
        WindowSize = windowSize;
    }

    /// <summary>Скільки ознак повертає Extract. Має збігатися з довжиною рядка.</summary>
    public const int FeatureCount = 10;

    private static readonly string[] Names =
    [
        "Δaltitude", "Δtemp", "|vibration|", "vibration",
        "σ(vibration) по вікну", "σ(altitude) по вікну", "середнє |vibration| по вікну",
        "частота зміни mode", "дрейф altitude по вікну", "critical"
    ];

    public static IReadOnlyList<string> FeatureNames => Names;

    /// <summary>Матриця ознак: рядок на кадр.</summary>
    public double[][] Extract(IReadOnlyList<TelemetryFrame> frames)
    {
        // TODO(S7). Мінімальний набір (можна розширювати, але тоді змініть
        // FeatureCount і Names):
        //
        //   Δaltitude, Δtemp            різниці з попереднім кадром
        //   |vibration|, vibration      миттєва вібрація
        //   σ(vibration), σ(altitude)   ковзні відхилення по вікну
        //   середнє |vibration|         по вікну
        //   частота зміни mode          у вікні
        //   дрейф altitude              від початку вікна до поточного кадру
        //   critical                    як є
        //
        // ПАСТКИ:
        //   • на ПЕРШОМУ кадрі попереднього немає — різниці дорівнюють нулю,
        //     а не NaN і не значенню «з нізвідки»;
        //   • вікно може бути довшим за сеанс — беріть те, що є;
        //   • вікно з одного кадру: дисперсія дорівнює нулю, а не ділиться на нуль.
        throw new NotImplementedException("FeatureExtractor.Extract");
    }
}

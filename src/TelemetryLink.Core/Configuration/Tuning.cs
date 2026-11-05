namespace TelemetryLink.Core.Configuration;

/// <summary>
/// Параметри, які підбирає оптимізатор. Це і є хромосома генетичного алгоритму.
///
/// Базовий рівень вимагає рівно трьох генів. Вони обрані так, щоб кожен
/// впливав на свою компоненту метрики Q:
///   WindowFrames         → розмір таблиці Гаффмана і якість пакування → S
///   CompressionThreshold → чи стискати взагалі                        → S і T
///   CriticalWeight       → що потрапить у ранні пакети                → R
///
/// Просунутий рівень додає глибину вікна ознак, архітектуру перцептрона
/// і ваги планувальника — сюди ж, новими полями.
/// </summary>
/// <param name="WindowFrames">Розмір вікна кандидатів, кадрів.</param>
/// <param name="CompressionThreshold">
/// Мінімальний виграш, за якого варто стискати: у скільки разів більше даних
/// має вміститися в пакет із таблицею Гаффмана, ніж у сирому режимі.
/// Значення 1,0 означає «стискати, лише якщо це справді дає більше кадрів».
/// Див. <see cref="Pipeline.Transmitter"/>: таблиця коштує 2 байти на символ
/// і платиться в КОЖНОМУ пакеті, тому на малих пакетах Гаффман буває збитковим.
/// </param>
/// <param name="CriticalWeight">Множник бонусу за критичність у цінності кадру.</param>
public sealed record Tuning(int WindowFrames, double CompressionThreshold, double CriticalWeight)
{
    public static readonly Tuning Default = new(64, 0.5, 1.0);

    public static readonly (double Low, double High)[] Bounds =
    [
        (8, 64),      // WindowFrames
        (0.3, 1.6),   // CompressionThreshold
        (0.0, 3.0)    // CriticalWeight
    ];

    public static readonly string[] GeneNames = ["вікно кандидатів", "поріг стиснення", "вага критичності"];

    public double[] ToGenes() => [WindowFrames, CompressionThreshold, CriticalWeight];

    public static Tuning FromGenes(IReadOnlyList<double> genes) => new(
        (int)Math.Round(Clamp(genes[0], 0)),
        Clamp(genes[1], 1),
        Clamp(genes[2], 2));

    /// <summary>Значення гена завжди лишається в межах — інакше прогін просто впаде.</summary>
    public static double Clamp(double value, int geneIndex)
    {
        var (low, high) = Bounds[geneIndex];
        return Math.Clamp(value, low, high);
    }

    public override string ToString() =>
        $"вікно={WindowFrames}, поріг={CompressionThreshold:F2}, вага_critical={CriticalWeight:F2}";
}

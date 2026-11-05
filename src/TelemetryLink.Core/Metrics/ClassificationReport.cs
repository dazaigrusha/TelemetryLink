namespace TelemetryLink.Core.Metrics;

/// <summary>
/// Якість класифікатора: macro-F1 і матриця помилок.
///
/// Чому macro-F1, а не частка правильних відповідей. Класи розподілені
/// приблизно як 78 % / 10 % / 12 %. Класифікатор, який завжди відповідає
/// «норма», дає точність 78 % і виглядає добре. Macro-F1 усереднює F1
/// ПО КЛАСАХ із однаковою вагою, тому проігнорований клас «аномалія» тягне
/// метрику вниз — і це навчальний зміст цієї метрики.
///
/// КРОК S7.
/// </summary>
public sealed class ClassificationReport
{
    private readonly int[,] _confusion;     // [істинний, передбачений]

    public ClassificationReport(IReadOnlyList<int> actual, IReadOnlyList<int> predicted, int classCount = 3)
    {
        if (actual.Count != predicted.Count)
            throw new ArgumentException("Довжини істинних і передбачених міток різні.");

        ClassCount = classCount;
        SampleCount = actual.Count;
        _confusion = new int[classCount, classCount];

        // TODO(S7): заповнити матрицю помилок.
        throw new NotImplementedException("ClassificationReport.ctor");
    }

    public int ClassCount { get; }
    public int SampleCount { get; }

    public int Confusion(int actual, int predicted) => _confusion[actual, predicted];

    public double Accuracy => throw new NotImplementedException("ClassificationReport.Accuracy");

    /// <summary>
    /// F1 окремого класу.
    /// ПАСТКА: клас, відсутній і в істині, і в прогнозі, має дати 0, а не NaN.
    /// </summary>
    public double F1(int classIndex) => throw new NotImplementedException("ClassificationReport.F1");

    /// <summary>Середнє F1 по класах із ОДНАКОВОЮ вагою.</summary>
    public double MacroF1 => throw new NotImplementedException("ClassificationReport.MacroF1");
}

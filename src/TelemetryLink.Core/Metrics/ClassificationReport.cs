namespace TelemetryLink.Core.Metrics;

/// <summary>Матриця [істина, прогноз], accuracy та macro-F1 з рівною вагою класів.</summary>
public sealed class ClassificationReport
{
    private readonly int[,] _confusion;
    public int ClassCount { get; }
    public int SampleCount { get; }

    public ClassificationReport(IReadOnlyList<int> actual, IReadOnlyList<int> predicted, int classCount = 3)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(predicted);
        if (classCount < 1 || classCount > 1000) throw new ArgumentOutOfRangeException(nameof(classCount));
        if (actual.Count != predicted.Count) throw new ArgumentException("Довжини міток різні.");
        ClassCount = classCount; SampleCount = actual.Count;
        _confusion = new int[classCount, classCount];
        for (int i = 0; i < actual.Count; i++)
        {
            if (actual[i] < 0 || actual[i] >= classCount || predicted[i] < 0 || predicted[i] >= classCount)
                throw new ArgumentException("Мітка поза діапазоном класів.");
            _confusion[actual[i], predicted[i]]++;
        }
    }

    public int Confusion(int actual, int predicted) => _confusion[actual, predicted];
    public double Accuracy => SampleCount == 0 ? 0 : (double)Enumerable.Range(0, ClassCount).Sum(c => _confusion[c, c]) / SampleCount;
    public double F1(int classIndex)
    {
        if (classIndex < 0 || classIndex >= ClassCount) throw new ArgumentOutOfRangeException(nameof(classIndex));
        long actual = 0, predicted = 0;
        for (int c = 0; c < ClassCount; c++) { actual += _confusion[classIndex, c]; predicted += _confusion[c, classIndex]; }
        return actual + predicted == 0 ? 0 : 2.0 * _confusion[classIndex, classIndex] / (actual + predicted);
    }
    public double MacroF1 => Enumerable.Range(0, ClassCount).Average(F1);
}

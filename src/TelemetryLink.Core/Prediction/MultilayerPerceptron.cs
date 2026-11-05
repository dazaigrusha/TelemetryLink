namespace TelemetryLink.Core.Prediction;

/// <summary>
/// Багатошаровий перцептрон, написаний ВРУЧНУ: прямий прохід, зворотне
/// поширення похибки, SGD. Бібліотеки навчання не використовуються — це
/// пряма вимога умови.
///
/// Рекомендована схема: прихований шар ReLU, вихідний softmax, функція втрат —
/// перехресна ентропія. Для цієї пари похідна вихідного шару зводиться до
/// різниці (передбачення − мітка), тому зворотний прохід виходить коротким.
///
/// КРОК S7.
/// </summary>
public sealed class MultilayerPerceptron
{
    private readonly int[] _layerSizes;
    private readonly double[][,] _weights;   // [шар][вихід, вхід]
    private readonly double[][] _biases;     // [шар][вихід]

    public IReadOnlyList<int> LayerSizes => _layerSizes;
    public int InputSize => _layerSizes[0];
    public int OutputSize => _layerSizes[^1];

    public MultilayerPerceptron(int[] layerSizes, int seed)
    {
        if (layerSizes.Length < 2) throw new ArgumentException("Потрібно щонайменше два шари.");
        _layerSizes = layerSizes;
        _weights = new double[layerSizes.Length - 1][,];
        _biases = new double[layerSizes.Length - 1][];

        // TODO(S7): ініціалізація ваг. Нулі НЕ підходять — усі нейрони шару
        // навчатимуться однаково. Використайте нормальний розподіл із
        // масштабом sqrt(2 / кількість_входів) (ініціалізація He).
        throw new NotImplementedException("MultilayerPerceptron.ctor");
    }

    /// <summary>Прямий прохід. Повертає ймовірності класів (softmax, сума = 1).</summary>
    public double[] Forward(double[] input)
    {
        // TODO(S7)
        throw new NotImplementedException("MultilayerPerceptron.Forward");
    }

    /// <summary>Індекс класу з найбільшою ймовірністю.</summary>
    public int Predict(double[] input)
    {
        // TODO(S7)
        throw new NotImplementedException("MultilayerPerceptron.Predict");
    }

    /// <summary>
    /// Один крок навчання на одному прикладі. Повертає значення втрат
    /// (перехресна ентропія) ДО оновлення ваг.
    /// </summary>
    public double TrainStep(double[] input, int label, double learningRate)
    {
        // TODO(S7). Зворотний прохід:
        //
        //   delta вихідного шару = output - one_hot(label)
        //   для кожного шару з кінця:
        //       delta попереднього шару рахується ДО оновлення ваг цього шару
        //       (інакше градієнт буде порахований по вже зміненим вагам)
        //       похідна ReLU: 1, якщо активація > 0, інакше 0
        //       w -= learningRate * delta * активація_попереднього_шару
        //       b -= learningRate * delta
        //
        // Найважливіший тест цього методу — звірка з ЧИСЛОВИМ градієнтом.
        // Помилка в backprop не ламає програму: мережа просто гірше вчиться.
        throw new NotImplementedException("MultilayerPerceptron.TrainStep");
    }

    /// <summary>Серіалізація ваг. Потрібна, щоб модель можна було зберегти і відтворити.</summary>
    public string ToJson()
    {
        // TODO(S7)
        throw new NotImplementedException("MultilayerPerceptron.ToJson");
    }

    public static MultilayerPerceptron FromJson(string json)
    {
        // TODO(S7)
        throw new NotImplementedException("MultilayerPerceptron.FromJson");
    }
}

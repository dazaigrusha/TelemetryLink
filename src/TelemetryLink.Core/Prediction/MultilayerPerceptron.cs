using System.Text.Json;

namespace TelemetryLink.Core.Prediction;

/// <summary>Власні ReLU, softmax, cross-entropy, backprop та SGD без бібліотек ML.</summary>
public sealed class MultilayerPerceptron
{
    private readonly int[] _layerSizes;
    private readonly double[][,] _weights;
    private readonly double[][] _biases;
    public IReadOnlyList<int> LayerSizes { get; }
    public int InputSize => _layerSizes[0];
    public int OutputSize => _layerSizes[^1];
    public int ParameterCount { get; }

    public MultilayerPerceptron(int[] layerSizes, int seed)
    {
        ParameterCount = ValidateShape(layerSizes);
        _layerSizes = layerSizes.ToArray(); LayerSizes = Array.AsReadOnly(_layerSizes);
        _weights = new double[layerSizes.Length - 1][,]; _biases = new double[layerSizes.Length - 1][];
        var random = new Random(seed);
        for (int layer = 0; layer < _weights.Length; layer++)
        {
            int input = layerSizes[layer], output = layerSizes[layer + 1];
            _weights[layer] = new double[output, input]; _biases[layer] = new double[output];
            double scale = Math.Sqrt(2.0 / input);
            for (int o = 0; o < output; o++)
                for (int i = 0; i < input; i++)
                    _weights[layer][o, i] = scale * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble()))
                        * Math.Cos(2 * Math.PI * random.NextDouble());
        }
    }

    private static int ValidateShape(int[] sizes)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        if (sizes.Length < 2 || sizes.Any(s => s < 1)) throw new ArgumentException("Потрібні щонайменше два додатні розміри шарів.");
        long count = 0;
        for (int l = 1; l < sizes.Length; l++)
        {
            count += (long)sizes[l] * (sizes[l - 1] + 1L);
            if (count > 5_000_000) throw new ArgumentException("Навчальна мережа обмежена 5 мільйонами параметрів.");
        }
        return (int)count;
    }

    private double[][] Activations(double[] input, out double[] logits)
    {
        if (input.Length != InputSize || input.Any(v => !double.IsFinite(v))) throw new ArgumentException("Некоректний вхід мережі.");
        var activations = new double[_weights.Length + 1][]; activations[0] = input;
        logits = [];
        for (int layer = 0; layer < _weights.Length; layer++)
        {
            int output = _layerSizes[layer + 1], inputs = _layerSizes[layer];
            var next = new double[output]; var weights = _weights[layer]; var previous = activations[layer];
            for (int o = 0; o < output; o++)
            {
                double value = _biases[layer][o];
                for (int i = 0; i < inputs; i++) value += weights[o, i] * previous[i];
                if (!double.IsFinite(value)) throw new ArgumentException("Активації мережі не є скінченними.");
                next[o] = layer == _weights.Length - 1 ? value : Math.Max(0, value);
            }
            if (layer == _weights.Length - 1)
            {
                logits = next;
                double maximum = next.Max(), sum = 0;
                var probabilities = new double[output];
                for (int o = 0; o < output; o++) sum += probabilities[o] = Math.Exp(next[o] - maximum);
                for (int o = 0; o < output; o++) probabilities[o] /= sum;
                next = probabilities;
            }
            activations[layer + 1] = next;
        }
        return activations;
    }

    public double[] Forward(double[] input) => Activations(input, out _)[^1];
    public int Predict(double[] input)
    {
        double[] probabilities = Forward(input);
        int best = 0;
        for (int i = 1; i < probabilities.Length; i++) if (probabilities[i] > probabilities[best]) best = i;
        return best;
    }

    private double CrossEntropy(double[] logits, int label)
    {
        if (label < 0 || label >= OutputSize) throw new ArgumentOutOfRangeException(nameof(label));
        double maximum = logits.Max();
        return maximum + Math.Log(logits.Sum(v => Math.Exp(v - maximum))) - logits[label];
    }
    public double Loss(double[] input, int label)
    {
        Activations(input, out var logits);
        return CrossEntropy(logits, label);
    }

    private (double[][] Activations, double[][] Deltas, double Loss) Backprop(double[] input, int label)
    {
        var activations = Activations(input, out var logits);
        double loss = CrossEntropy(logits, label);
        var deltas = new double[_weights.Length][];
        deltas[^1] = activations[^1].ToArray(); deltas[^1][label] -= 1;
        // Усі delta обчислюються за СТАРИМИ вагами, до жодного оновлення.
        for (int layer = _weights.Length - 2; layer >= 0; layer--)
        {
            deltas[layer] = new double[_layerSizes[layer + 1]];
            for (int i = 0; i < deltas[layer].Length; i++)
            {
                double value = 0;
                for (int o = 0; o < deltas[layer + 1].Length; o++) value += _weights[layer + 1][o, i] * deltas[layer + 1][o];
                deltas[layer][i] = activations[layer + 1][i] > 0 ? value : 0;
            }
        }
        return (activations, deltas, loss);
    }

    public double TrainStep(double[] input, int label, double learningRate)
    {
        if (!double.IsFinite(learningRate) || learningRate < 0) throw new ArgumentOutOfRangeException(nameof(learningRate));
        var (activations, deltas, loss) = Backprop(input, label);
        for (int layer = 0; layer < _weights.Length; layer++)
            for (int o = 0; o < deltas[layer].Length; o++)
            {
                double delta = deltas[layer][o];
                for (int i = 0; i < activations[layer].Length; i++) _weights[layer][o, i] -= learningRate * delta * activations[layer][i];
                _biases[layer][o] -= learningRate * delta;
            }
        return loss;
    }

    /// <summary>Градієнт у тому самому порядку, що й GetParameters; потрібен для числової перевірки.</summary>
    public double[] Gradient(double[] input, int label)
    {
        var (activations, deltas, _) = Backprop(input, label);
        var result = new double[ParameterCount]; int position = 0;
        for (int layer = 0; layer < _weights.Length; layer++)
        {
            for (int o = 0; o < deltas[layer].Length; o++)
                for (int i = 0; i < activations[layer].Length; i++) result[position++] = deltas[layer][o] * activations[layer][i];
            foreach (double delta in deltas[layer]) result[position++] = delta;
        }
        return result;
    }

    public double[] GetParameters()
    {
        var result = new double[ParameterCount]; int position = 0;
        for (int layer = 0; layer < _weights.Length; layer++)
        {
            for (int o = 0; o < _layerSizes[layer + 1]; o++)
                for (int i = 0; i < _layerSizes[layer]; i++) result[position++] = _weights[layer][o, i];
            foreach (double bias in _biases[layer]) result[position++] = bias;
        }
        return result;
    }

    public void SetParameters(ReadOnlySpan<double> parameters)
    {
        if (parameters.Length != ParameterCount) throw new ArgumentException("Неправильна кількість параметрів.");
        foreach (double value in parameters) if (!double.IsFinite(value)) throw new ArgumentException("Ваги мають бути скінченними.");
        int position = 0;
        for (int layer = 0; layer < _weights.Length; layer++)
        {
            for (int o = 0; o < _layerSizes[layer + 1]; o++)
                for (int i = 0; i < _layerSizes[layer]; i++) _weights[layer][o, i] = parameters[position++];
            for (int o = 0; o < _biases[layer].Length; o++) _biases[layer][o] = parameters[position++];
        }
    }

    private sealed record NetworkState(int SchemaVersion, int[] LayerSizes, double[] Parameters);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public string ToJson() => JsonSerializer.Serialize(new NetworkState(1, _layerSizes, GetParameters()), JsonOptions);
    public static MultilayerPerceptron FromJson(string json)
    {
        try
        {
            var state = JsonSerializer.Deserialize<NetworkState>(json, JsonOptions) ?? throw new InvalidDataException("Порожні ваги мережі.");
            if (state.SchemaVersion != 1 || state.LayerSizes is null || state.Parameters is null)
                throw new InvalidDataException("Невідомий або неповний формат мережі.");
            if (ValidateShape(state.LayerSizes) != state.Parameters.Length) throw new InvalidDataException("Розміри шарів не відповідають вагам.");
            var network = new MultilayerPerceptron(state.LayerSizes, 0);
            network.SetParameters(state.Parameters);
            return network;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        { throw new InvalidDataException("Некоректний файл ваг мережі.", exception); }
    }
}

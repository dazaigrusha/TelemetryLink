using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Metrics;

namespace TelemetryLink.Core.Prediction;

public sealed record TrainingOptions(int WindowSize = 8, int[]? HiddenLayers = null, int Epochs = 12,
    double LearningRate = 0.02, int Initializations = 3, double ValidationShare = 0.2, int Seed = 1)
{
    public int[] Hidden => HiddenLayers ?? [32, 16];
}
public sealed record TrainingAttempt(int Seed, double ValidationMacroF1) { public int BestEpoch { get; init; } }
public sealed record TrainingEpoch(int Seed, int Epoch, double MeanBalancedTrainingLoss, double ValidationMacroF1);

/// <summary>Часовий TRAIN 80/20, scaler лише з 80%, три ініціалізації, вибір лише за внутрішньою валідацією.</summary>
public sealed class PerceptronTrainer
{
    public IReadOnlyList<TrainingAttempt> Attempts { get; private set; } = [];
    public IReadOnlyList<TrainingEpoch> History { get; private set; } = [];
    public int TrainingCount { get; private set; }
    public int ValidationCount { get; private set; }

    public ClassifierModel Train(IReadOnlyList<TelemetryFrame> frames, IReadOnlyList<int> labels,
        TrainingOptions options, Action<TrainingEpoch>? progress = null)
    {
        Attempts = []; History = [];
        if (frames.Count != labels.Count || frames.Count < 5 || labels.Any(label => label is < 0 or > 2))
            throw new ArgumentException("Потрібно від 5 кадрів із відповідними мітками 0..2.");
        if (options.WindowSize < 1 || options.Epochs < 1 || options.Initializations < 3
            || !double.IsFinite(options.LearningRate) || options.LearningRate <= 0
            || options.ValidationShare != 0.2 || options.Hidden.Any(size => size < 1)
            || (long)options.Seed + options.Initializations - 1 > int.MaxValue)
            throw new ArgumentException("Некоректні параметри; протокол вимагає 80/20 та щонайменше 3 ініціалізації.");
        var features = new FeatureExtractor(options.WindowSize).Extract(frames);
        TrainingCount = (int)Math.Floor(frames.Count * (1 - options.ValidationShare));
        ValidationCount = frames.Count - TrainingCount;
        // Ознаки причинні, тому внутрішня валідація може використовувати лише минулу історію TRAIN.
        var (means, deviations) = FeatureScaler.Fit(features.Take(TrainingCount).ToArray());
        foreach (var row in features) FeatureScaler.ApplyInPlace(row, means, deviations);
        var groups = Enumerable.Range(0, 3).Select(label => Enumerable.Range(0, TrainingCount)
            .Where(i => labels[i] == label).ToArray()).Where(group => group.Length > 0).ToArray();
        int maximum = groups.Max(group => group.Length);
        int[] shape = new[] { FeatureExtractor.FeatureCount }.Concat(options.Hidden).Append(3).ToArray();
        var attempts = new List<TrainingAttempt>(); var history = new List<TrainingEpoch>();
        ClassifierModel? selected = null;
        for (int initialization = 0; initialization < options.Initializations; initialization++)
        {
            int seed = options.Seed + initialization;
            var network = new MultilayerPerceptron(shape, seed); var random = new Random(seed ^ 0x5F3759DF);
            double bestF1 = -1; int bestEpoch = 0; string bestNetwork = "";
            for (int epoch = 1; epoch <= options.Epochs; epoch++)
            {
                // Балансування тільки навчальної частини: однакова кількість
                // вибірок кожного присутнього класу, з поверненням.
                var order = new int[maximum * groups.Length]; int position = 0;
                foreach (var group in groups)
                    for (int i = 0; i < maximum; i++) order[position++] = group[random.Next(group.Length)];
                for (int i = order.Length - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1); (order[i], order[j]) = (order[j], order[i]);
                }
                double loss = 0, learningRate = options.LearningRate / (1 + 0.15 * (epoch - 1));
                foreach (int index in order) loss += network.TrainStep(features[index], labels[index], learningRate);
                var predicted = features.Skip(TrainingCount).Select(network.Predict).ToArray();
                double f1 = new ClassificationReport(labels.Skip(TrainingCount).ToArray(), predicted).MacroF1;
                var result = new TrainingEpoch(seed, epoch, loss / order.Length, f1);
                history.Add(result); progress?.Invoke(result);
                if (f1 > bestF1 + 1e-12) { bestF1 = f1; bestEpoch = epoch; bestNetwork = network.ToJson(); }
            }
            attempts.Add(new TrainingAttempt(seed, bestF1) { BestEpoch = bestEpoch });
            if (selected is null || bestF1 > selected.ValidationMacroF1 + 1e-12)
                selected = new ClassifierModel(bestNetwork, options.WindowSize, means.ToArray(), deviations.ToArray(), bestF1, seed)
                { TrainingCount = TrainingCount, ValidationCount = ValidationCount, SelectedEpoch = bestEpoch };
        }
        Attempts = attempts.AsReadOnly(); History = history.AsReadOnly();
        return selected!;
    }
}

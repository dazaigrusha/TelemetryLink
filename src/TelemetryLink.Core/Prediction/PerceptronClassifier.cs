using System.Text.Json;
using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Prediction;

/// <summary>Ваги, схема ознак та нормалізація зберігаються разом.</summary>
public sealed record ClassifierModel(string NetworkJson, int WindowSize, double[] FeatureMeans,
    double[] FeatureDeviations, double ValidationMacroF1, int Seed)
{
    public int SchemaVersion { get; init; } = 1;
    public string FeatureSchema { get; init; } = FeatureExtractor.Schema;
    public int TrainingCount { get; init; }
    public int ValidationCount { get; init; }
    public int SelectedEpoch { get; init; }

    public void Validate()
    {
        if (SchemaVersion != 1 || FeatureSchema != FeatureExtractor.Schema || WindowSize < 1
            || FeatureMeans is null || FeatureDeviations is null || FeatureMeans.Length != FeatureExtractor.FeatureCount
            || FeatureDeviations.Length != FeatureExtractor.FeatureCount
            || FeatureMeans.Any(v => !double.IsFinite(v)) || FeatureDeviations.Any(v => !double.IsFinite(v) || v <= 0)
            || !double.IsFinite(ValidationMacroF1) || ValidationMacroF1 is < 0 or > 1 || string.IsNullOrWhiteSpace(NetworkJson))
            throw new InvalidDataException("Некоректні параметри моделі або схема ознак.");
        var network = MultilayerPerceptron.FromJson(NetworkJson);
        if (network.InputSize != FeatureExtractor.FeatureCount || network.OutputSize != 3)
            throw new InvalidDataException("Модель має приймати 10 ознак і повертати 3 класи.");
    }

    public string ToJson() { Validate(); return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }); }
    public static ClassifierModel FromJson(string json)
    {
        try
        {
            var model = JsonSerializer.Deserialize<ClassifierModel>(json) ?? throw new InvalidDataException("Порожній файл моделі.");
            model.Validate(); return model;
        }
        catch (JsonException exception) { throw new InvalidDataException("Некоректний JSON моделі.", exception); }
    }
    public static ClassifierModel Load(string path) => FromJson(File.ReadAllText(path));
    public void Save(string path)
    {
        string json = ToJson();
        string? parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (parent is not null) Directory.CreateDirectory(parent);
        File.WriteAllText(path, json);
    }
}

public sealed class PerceptronClassifier : IFrameClassifier
{
    private readonly MultilayerPerceptron _network;
    private readonly FeatureExtractor _extractor;
    private readonly double[] _means, _deviations;
    public PerceptronClassifier(ClassifierModel model)
    {
        ArgumentNullException.ThrowIfNull(model); model.Validate();
        _network = MultilayerPerceptron.FromJson(model.NetworkJson);
        _extractor = new FeatureExtractor(model.WindowSize);
        _means = model.FeatureMeans.ToArray(); _deviations = model.FeatureDeviations.ToArray();
    }
    public string Name => "mlp";
    public FrameClass[] Classify(IReadOnlyList<TelemetryFrame> frames)
    {
        var features = _extractor.Extract(frames); var result = new FrameClass[features.Length];
        for (int i = 0; i < features.Length; i++)
        {
            FeatureScaler.ApplyInPlace(features[i], _means, _deviations);
            result[i] = (FrameClass)_network.Predict(features[i]);
        }
        return result;
    }
}

public static class FeatureScaler
{
    public static (double[] Means, double[] Deviations) Fit(double[][] features)
    {
        if (features.Length == 0 || features[0] is null || features[0].Length == 0)
            throw new ArgumentException("Потрібна непорожня матриця ознак.");
        int columns = features[0].Length; var means = new double[columns]; var variances = new double[columns];
        for (int row = 0; row < features.Length; row++)
        {
            if (features[row] is null || features[row].Length != columns || features[row].Any(v => !double.IsFinite(v)))
                throw new ArgumentException("Некоректні рядки ознак.");
            for (int column = 0; column < columns; column++)
            {
                double delta = features[row][column] - means[column];
                means[column] += delta / (row + 1);
                variances[column] += delta * (features[row][column] - means[column]);
            }
        }
        var deviations = variances.Select(v => Math.Sqrt(Math.Max(0, v / features.Length))).Select(v => v < 1e-12 ? 1.0 : v).ToArray();
        return (means, deviations);
    }

    public static void ApplyInPlace(double[] features, double[] means, double[] deviations)
    {
        if (features.Length != means.Length || features.Length != deviations.Length)
            throw new ArgumentException("Розміри нормалізації не відповідають ознакам.");
        for (int i = 0; i < features.Length; i++)
        {
            if (!double.IsFinite(features[i]) || !double.IsFinite(means[i]) || !double.IsFinite(deviations[i]) || deviations[i] <= 0)
                throw new ArgumentException("Нормалізація потребує скінченних значень і додатних відхилень.");
            features[i] = (features[i] - means[i]) / deviations[i];
            if (!double.IsFinite(features[i])) throw new ArgumentException("Нормалізована ознака не є скінченною.");
        }
    }
}

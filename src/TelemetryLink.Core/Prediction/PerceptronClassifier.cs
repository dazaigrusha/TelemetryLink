using System.Text.Json;
using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Prediction;

/// <summary>
/// Навчена модель разом із усім, що потрібно для відтворюваного застосування:
/// розмір вікна ознак і параметри нормалізації.
///
/// Зберігати нормалізацію в одному файлі з вагами ОБОВ'ЯЗКОВО: модель,
/// застосована до ненормалізованих ознак, дає випадкові відповіді, а помилка
/// виявляється далеко від місця виникнення.
/// </summary>
public sealed record ClassifierModel(
    string NetworkJson,
    int WindowSize,
    double[] FeatureMeans,
    double[] FeatureDeviations,
    double ValidationMacroF1,
    int Seed)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static ClassifierModel FromJson(string json) =>
        JsonSerializer.Deserialize<ClassifierModel>(json)
        ?? throw new InvalidDataException("Не вдалося прочитати файл моделі.");

    public static ClassifierModel Load(string path) => FromJson(File.ReadAllText(path));
    public void Save(string path) => File.WriteAllText(path, ToJson());
}

/// <summary>Класифікатор на основі навченого перцептрона. КРОК S7.</summary>
public sealed class PerceptronClassifier : IFrameClassifier
{
    private readonly MultilayerPerceptron _network;
    private readonly FeatureExtractor _extractor;
    private readonly double[] _means;
    private readonly double[] _deviations;

    public PerceptronClassifier(ClassifierModel model)
    {
        _network = MultilayerPerceptron.FromJson(model.NetworkJson);
        _extractor = new FeatureExtractor(model.WindowSize);
        _means = model.FeatureMeans;
        _deviations = model.FeatureDeviations;
    }

    public string Name => "mlp";

    public FrameClass[] Classify(IReadOnlyList<TelemetryFrame> frames)
    {
        // TODO(S7): витягти ознаки, ЗАСТОСУВАТИ ТУ САМУ нормалізацію,
        // що й під час навчання, і передбачити клас кожного кадру.
        throw new NotImplementedException("PerceptronClassifier.Classify");
    }
}

/// <summary>Нормалізація ознак: віднімаємо середнє, ділимо на відхилення.</summary>
public static class FeatureScaler
{
    public static (double[] Means, double[] Deviations) Fit(double[][] features)
    {
        // TODO(S7). Пастка: стала ознака має нульове відхилення — ділення на
        // нуль дасть NaN у всій мережі. Замініть таке відхилення на 1.
        throw new NotImplementedException("FeatureScaler.Fit");
    }

    public static void ApplyInPlace(double[] features, double[] means, double[] deviations)
    {
        // TODO(S7)
        throw new NotImplementedException("FeatureScaler.ApplyInPlace");
    }
}

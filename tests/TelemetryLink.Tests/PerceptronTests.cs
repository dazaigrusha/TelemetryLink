using TelemetryLink.Core.Prediction;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Metrics;
namespace TelemetryLink.Tests;

public class PerceptronTests
{
    [Fact]
    public void Backpropagation_MatchesNumericalGradient()
    {
        var net = new MultilayerPerceptron([2, 3, 2, 2], 7);
        var parameters = net.GetParameters();
        // Positive values keep hidden ReLUs away from their nondifferentiable zero.
        for (int i = 0; i < parameters.Length; i++) parameters[i] = .1 + .01 * i;
        net.SetParameters(parameters);
        double[] input = [.4, .7];
        var gradient = net.Gradient(input, 1);
        for (int i = 0; i < parameters.Length; i++)
        {
            var plus = (double[])parameters.Clone(); plus[i] += 1e-6; net.SetParameters(plus); double a = net.Loss(input, 1);
            var minus = (double[])parameters.Clone(); minus[i] -= 1e-6; net.SetParameters(minus); double b = net.Loss(input, 1);
            Assert.InRange(Math.Abs(gradient[i] - (a - b) / 2e-6), 0, 1e-6);
        }
        net.SetParameters(parameters); double loss = net.Loss(input, 1);
        Assert.Equal(loss, net.TrainStep(input, 1, .01), 12);
        var updated = net.GetParameters();
        for (int i = 0; i < parameters.Length; i++) Assert.Equal(parameters[i] - .01 * gradient[i], updated[i], 12);
    }

    [Fact]
    public void Forward_ReturnsProbabilitiesThatSumToOne()
    {
        var net = new MultilayerPerceptron([2, 2], 1);
        net.SetParameters([0, 0, 0, 0, 1000 + Math.Log(2), 1000]);
        var p = net.Forward([1, 2]);
        Assert.Equal(1, p.Sum(), 12); Assert.Equal(2.0 / 3, p[0], 12);
        Assert.True(double.IsFinite(net.Loss([1, 2], 1)));
    }

    [Fact]
    public void Train_OnLinearlySeparableData_ReachesFullAccuracy()
    {
        double[][] x = [[-2, -1], [-1, -2], [1, 2], [2, 1]]; int[] y = [0, 0, 1, 1];
        var net = new MultilayerPerceptron([2, 2], 1);
        for (int e = 0; e < 150; e++) for (int i = 0; i < 4; i++) net.TrainStep(x[i], y[i], .05);
        Assert.Equal(y, x.Select(net.Predict).ToArray());
    }

    [Fact]
    public void Train_OnXor_RequiresHiddenLayer()
    {
        double[][] x = [[0, 0], [0, 1], [1, 0], [1, 1]]; int[] y = [0, 1, 1, 0];
        var hidden = new MultilayerPerceptron([2, 8, 2], 3);
        var linear = new MultilayerPerceptron([2, 2], 3);
        for (int e = 0; e < 2500; e++) for (int i = 0; i < 4; i++)
        { hidden.TrainStep(x[i], y[i], .05); linear.TrainStep(x[i], y[i], .05); }
        Assert.Equal(y, x.Select(hidden.Predict).ToArray());
        Assert.True(x.Where((row, i) => linear.Predict(row) == y[i]).Count() < 4);
    }

    [Fact]
    public void SameSeed_ProducesSameInitialWeights() => Assert.Equal(
        new MultilayerPerceptron([2, 3, 2], 7).GetParameters(), new MultilayerPerceptron([2, 3, 2], 7).GetParameters());

    [Fact]
    public void ToJson_ThenFromJson_PreservesPredictions()
    {
        var net = new MultilayerPerceptron([2, 3, 2], 7);
        var restored = MultilayerPerceptron.FromJson(net.ToJson());
        Assert.Equal(net.Forward([.3, -.2]), restored.Forward([.3, -.2]));
        Assert.Throws<InvalidDataException>(() => MultilayerPerceptron.FromJson("{}"));
        var original = net.GetParameters(); var bad = (double[])original.Clone(); bad[0] = double.NaN;
        Assert.Throws<ArgumentException>(() => net.SetParameters(bad)); Assert.Equal(original, net.GetParameters());
    }

    static TelemetryFrame Frame(int i) => new((uint)i, (short)(i * 2), (short)i, (short)(i % 3 * 10), (byte)(i % 2), 0);

    [Fact]
    public void Features_UseOnlyPastAndHandleShortSessions()
    {
        var frames = Enumerable.Range(0, 20).Select(Frame).ToArray();
        var extractor = new FeatureExtractor(50); var all = extractor.Extract(frames);
        var prefix = extractor.Extract(frames.Take(4).ToArray());
        for (int i = 0; i < 4; i++) Assert.Equal(all[i], prefix[i]);
        Assert.Equal(0, all[0][0]); Assert.Equal(0, all[0][1]);
        Assert.Equal(5, all[1][6]); Assert.Equal(5, all[1][4]); Assert.Equal(1, all[1][5]);
        Assert.All(all.SelectMany(x => x), v => Assert.True(double.IsFinite(v)));
        Assert.Empty(extractor.Extract([]));
        Assert.Equal(32768, extractor.Extract([Frame(0) with { Vibration = short.MinValue }])[0][2]);
        var single = new FeatureExtractor(1).Extract(frames); Assert.All(single, x => Assert.Equal(0, x[4]));
    }

    [Fact]
    public void Scaler_ConstantFeature_DoesNotDivideByZero()
    {
        double[][] rows = [[2, 1], [2, 3]];
        var (means, deviations) = FeatureScaler.Fit(rows);
        Assert.Equal(new double[] { 2, 2 }, means); Assert.Equal(new double[] { 1, 1 }, deviations);
        foreach (var row in rows) FeatureScaler.ApplyInPlace(row, means, deviations);
        Assert.Equal(new double[] { 0, -1 }, rows[0]); Assert.Equal(new double[] { 0, 1 }, rows[1]);
    }

    [Fact]
    public void Trainer_UsesPrefixScalerAndSelectsBestInternalCheckpoint()
    {
        var frames = Enumerable.Range(0, 30).Select(Frame).ToArray();
        frames[29] = frames[29] with { AltitudeDm = 30000 };
        var labels = Enumerable.Range(0, 30).Select(i => i % 3).ToArray();
        var options = new TrainingOptions(Epochs: 2, HiddenLayers: [4]);
        var trainer = new PerceptronTrainer(); var model = trainer.Train(frames, labels, options);
        Assert.Equal(24, model.TrainingCount); Assert.Equal(6, model.ValidationCount);
        var (means, _) = FeatureScaler.Fit(new FeatureExtractor().Extract(frames).Take(24).ToArray());
        Assert.Equal(means, model.FeatureMeans); Assert.Equal(3, trainer.Attempts.Count); Assert.Equal(6, trainer.History.Count);
        Assert.Equal(trainer.History.Max(h => h.ValidationMacroF1), model.ValidationMacroF1);
        var prediction = new PerceptronClassifier(model).Classify(frames).Skip(24).Select(x => (int)x).ToArray();
        Assert.Equal(model.ValidationMacroF1, new ClassificationReport(labels.Skip(24).ToArray(), prediction).MacroF1, 12);
        Assert.Equal(model.NetworkJson, new PerceptronTrainer().Train(frames, labels, options).NetworkJson);
        Assert.Equal(new PerceptronClassifier(model).Classify(frames), new PerceptronClassifier(ClassifierModel.FromJson(model.ToJson())).Classify(frames));
        Assert.Throws<ArgumentException>(() => trainer.Train(frames, labels, options with { Initializations = 2 }));
    }
}

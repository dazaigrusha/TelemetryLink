using System.Globalization;
using TelemetryLink.Core.InputOutput;
using TelemetryLink.Core.Metrics;
using TelemetryLink.Core.Prediction;

namespace TelemetryLink.Cli.Commands;

public static class TrainCommand
{
    public static int Execute(CommandLineOptions options)
    {
        var context = new CommandContext(options);
        bool noTest = options.Has("no-test");
        if (noTest && options.Has("valid")) throw new ArgumentException("Оберіть --no-test або --valid.");
        string trainPath = options.Has("train") ? options.Require("train") : Path.Combine(CommandContext.DataDirectory, "train.csv");
        string validPath = options.Has("valid") ? options.Require("valid") : Path.Combine(CommandContext.DataDirectory, "valid.csv");
        string configPath = options.Value("config", Path.Combine(CommandContext.DataDirectory, "team_config.json"));
        string output = options.Has("out") ? options.Require("out") : "model.json";
        TrainingArtifacts.ValidateDestinations(output, trainPath, validPath, configPath);
        if (!noTest && !File.Exists(validPath)) throw new FileNotFoundException("Не знайдено відкритий TEST; для навчання без його оцінки задайте --no-test.", validPath);
        int[]? hidden = null;
        if (options.Has("hidden"))
        {
            string text = options.Require("hidden");
            hidden = text.Equals("none", StringComparison.OrdinalIgnoreCase) ? [] : text.Split(',').Select(value =>
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) && count > 0
                    ? count : throw new ArgumentException("--hidden: додатні розміри через кому або none.")).ToArray();
        }
        var trainingOptions = new TrainingOptions(WindowSize: options.Value("window", 8), HiddenLayers: hidden,
            Epochs: options.Value("epochs", 12), LearningRate: options.Value("learning-rate", 0.02),
            Initializations: options.Value("inits", 3), Seed: options.Value("seed", 1));
        var train = TelemetryCsvReader.Read(trainPath);
        if (train.Labels is null) throw new InvalidDataException("TRAIN має містити колонку label.");
        Console.WriteLine($"TRAIN: {train.Frames.Count} кадрів, часовий поділ 80/20, {trainingOptions.Initializations} ініціалізації.");
        Console.WriteLine($"Вікно історії {trainingOptions.WindowSize}; ознаки: {string.Join(", ", FeatureExtractor.FeatureNames)}");
        var trainer = new PerceptronTrainer();
        var model = trainer.Train(train.Frames, train.Labels, trainingOptions, epoch => Console.WriteLine(
            FormattableString.Invariant($"seed={epoch.Seed}, epoch={epoch.Epoch}: loss={epoch.MeanBalancedTrainingLoss:F5}, internal macro-F1={epoch.ValidationMacroF1:F4}")));
        foreach (var attempt in trainer.Attempts)
            Console.WriteLine($"seed={attempt.Seed}: найкраща епоха {attempt.BestEpoch}, internal macro-F1={attempt.ValidationMacroF1:F4}");
        Console.WriteLine($"Обрано seed={model.Seed}, epoch={model.SelectedEpoch}; internal macro-F1={model.ValidationMacroF1:F4}.");
        ClassifierEvaluation? evaluation = null;
        if (!noTest)
        {
            // Відкритий TEST читається та оцінюється ЛИШЕ після остаточного вибору моделі.
            var test = TelemetryCsvReader.Read(validPath);
            if (test.Labels is null || test.Frames.Count == 0) throw new InvalidDataException("Відкритий TEST має містити кадри та label.");
            var predicted = new PerceptronClassifier(model).Classify(test.Frames).Select(c => (int)c).ToArray();
            var report = new ClassificationReport(test.Labels, predicted);
            var majority = new ClassificationReport(test.Labels, new int[test.Labels.Count]);
            evaluation = ClassifierEvaluation.From(report, majority);
            Console.WriteLine($"{Path.GetFileName(validPath)}: macro-F1={report.MacroF1:F4}; accuracy={report.Accuracy:P2}; majority macro-F1={majority.MacroF1:F4}");
            Console.WriteLine("Матриця: рядок — істина, стовпець — прогноз [норма, аномалія, перехідний].");
            for (int actual = 0; actual < 3; actual++)
                Console.WriteLine($"{actual}: {report.Confusion(actual, 0),6} {report.Confusion(actual, 1),6} {report.Confusion(actual, 2),6}; F1={report.F1(actual):F4}");
            Console.WriteLine($"{(report.MacroF1 >= context.Config.MinimumMacroF1 ? "OK" : "НЕ ДОСЯГНУТО")}: F1_min={context.Config.MinimumMacroF1:F2}");
        }
        else Console.WriteLine("Відкритий TEST не оцінювався (--no-test); це не підсумкова оцінка.");
        TrainingArtifacts.Write(output, model, trainer, trainingOptions, trainPath, configPath,
            noTest ? null : validPath, evaluation, context.Config);
        return evaluation is not null && evaluation.MacroF1 < context.Config.MinimumMacroF1 ? 1 : 0;
    }
}

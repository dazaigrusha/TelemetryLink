using TelemetryLink.Core.InputOutput;
using TelemetryLink.Core.Metrics;
using TelemetryLink.Core.Prediction;

namespace TelemetryLink.Cli.Commands;

/// <summary>
/// Навчання класифікатора за протоколом: 80/20, три ініціалізації, вибір
/// моделі за macro-F1 на валідації.
/// </summary>
public static class TrainCommand
{
    public static int Execute(CommandLineOptions options)
    {
        var context = new CommandContext(options);

        string trainPath = options.Value("train", Path.Combine(CommandContext.DataDirectory, "train.csv"));
        string validPath = options.Value("valid", Path.Combine(CommandContext.DataDirectory, "valid.csv"));
        string outputPath = options.Value("out", "model.json");

        var train = TelemetryCsvReader.Read(trainPath);
        if (train.Labels is null)
        {
            Console.Error.WriteLine($"У файлі {trainPath} немає колонки label.");
            return 2;
        }

        var trainingOptions = new TrainingOptions(
            WindowSize: options.Value("window", 8),
            Epochs: options.Value("epochs", 12),
            LearningRate: options.Value("learning-rate", 0.02),
            Initializations: options.Value("inits", 3),
            Seed: options.Value("seed", 1));

        Console.WriteLine();
        Console.WriteLine($"навчання: {train.Frames.Count} кадрів, вікно {trainingOptions.WindowSize}, " +
                          $"{trainingOptions.Initializations} ініціалізацій, {trainingOptions.Epochs} епох");
        Console.WriteLine($"ознаки: {string.Join(", ", FeatureExtractor.FeatureNames)}");
        Console.WriteLine();

        var trainer = new PerceptronTrainer();
        var model = trainer.Train(train.Frames, train.Labels, trainingOptions);

        foreach (var attempt in trainer.Attempts)
            Console.WriteLine($"  ініціалізація seed={attempt.Seed}   macro-F1 на валідації = {attempt.ValidationMacroF1:F4}");

        Console.WriteLine();
        Console.WriteLine($"обрано модель seed={model.Seed}, macro-F1 = {model.ValidationMacroF1:F4}");

        model.Save(outputPath);
        Console.WriteLine($"записано: {outputPath}");

        // Перевірка на відкритому наборі valid.csv. Прихованого holdout.csv
        // у команди немає і не буде — підсумковий macro-F1 рахує викладач.
        if (File.Exists(validPath)) EvaluateOn(validPath, model, context.Config.MinimumMacroF1);

        Console.WriteLine();
        return 0;
    }

    private static void EvaluateOn(string path, ClassifierModel model, double minimumMacroF1)
    {
        var validation = TelemetryCsvReader.Read(path);
        if (validation.Labels is null) return;

        var predicted = new PerceptronClassifier(model).Classify(validation.Frames)
            .Select(c => (int)c).ToArray();
        var report = new ClassificationReport(validation.Labels, predicted);

        Console.WriteLine();
        Console.WriteLine($"{Path.GetFileName(path)}: macro-F1 = {report.MacroF1:F4}, точність = {report.Accuracy:P1}");
        Console.WriteLine();
        Console.WriteLine("матриця помилок (рядок — істина, стовпець — прогноз)");
        Console.WriteLine($"{"",14}{"норма",10}{"аномалія",12}{"перехідний",12}{"F1",10}");

        string[] names = ["норма", "аномалія", "перехідний"];
        for (int actual = 0; actual < report.ClassCount; actual++)
        {
            Console.Write($"  {names[actual],-12}");
            for (int predictedClass = 0; predictedClass < report.ClassCount; predictedClass++)
                Console.Write($"{report.Confusion(actual, predictedClass),10}  ");
            Console.WriteLine($"{report.F1(actual),8:F4}");
        }

        Console.WriteLine();
        Console.WriteLine($"  {(report.MacroF1 >= minimumMacroF1 ? "OK  " : "УВАГА")} " +
                          $"поріг F1_min = {minimumMacroF1:F2}, отримано {report.MacroF1:F4}");
    }
}

using System.Text.Json;
using TelemetryLink.Cli;
using TelemetryLink.Cli.Commands;
using TelemetryLink.Core.Prediction;
namespace TelemetryLink.Tests;
public class TrainingCliTests
{
    [Fact]
    public void NoTest_ProducesReusableModelAndAuditableHistory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "tlm-training-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string train = Path.Combine(directory, "train.csv"), output = Path.Combine(directory, "model.json");
            File.WriteAllLines(train, File.ReadLines(DataKit.Path("train.csv")).Take(41));
            var args = new CommandLineOptions(["train", "--config", DataKit.Path("team_config.json"), "--train", train,
                "--no-test", "--epochs", "1", "--hidden", "4", "--out", output]);
            Assert.Equal(0, TrainCommand.Execute(args));
            var model = ClassifierModel.Load(output); Assert.Equal(32, model.TrainingCount); Assert.Equal(8, model.ValidationCount);
            using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "model.training.json")));
            Assert.Equal(JsonValueKind.Null, report.RootElement.GetProperty("openTest").ValueKind);
            Assert.False(report.RootElement.GetProperty("protocol").GetProperty("testUsedForSelection").GetBoolean());
            Assert.Equal(3, report.RootElement.GetProperty("attempts").GetArrayLength());
            Assert.Equal(4, File.ReadAllLines(Path.Combine(directory, "model.history.csv")).Length);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public void ArtifactPaths_CannotOverwriteInputs()
    {
        Assert.Throws<ArgumentException>(() => TrainingArtifacts.ValidateDestinations("model.json", "model.training.json"));
        Assert.Throws<ArgumentException>(() => TrainingArtifacts.OutputPaths("train.csv"));
        Assert.Throws<ArgumentException>(() => TrainCommand.Execute(new CommandLineOptions([
            "train", "--config", DataKit.Path("team_config.json"), "--no-test", "--valid", "missing.csv"])));
    }
}

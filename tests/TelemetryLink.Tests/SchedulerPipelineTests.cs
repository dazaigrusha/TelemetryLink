using TelemetryLink.Core;
using TelemetryLink.Core.Channel;
using TelemetryLink.Core.Pipeline;
using TelemetryLink.Core.Prediction;

namespace TelemetryLink.Tests;

public class SchedulerPipelineTests
{
    private static PipelineOptions Options(string scheduler, string packer = "dp", string codec = "huffman") => new()
    {
        Codec = AlgorithmCatalog.Codec(codec), Packer = AlgorithmCatalog.Packer(packer),
        Scheduler = AlgorithmCatalog.Scheduler(scheduler), Classifier = new MajorityClassifier()
    };

    [Theory]
    [InlineData("calm", "dp", "huffman")]
    [InlineData("working", "dp", "huffman")]
    [InlineData("emergency", "dp", "huffman")]
    [InlineData("calm", "greedy", "huffman")]
    [InlineData("working", "greedy", "huffman")]
    [InlineData("emergency", "greedy", "huffman")]
    [InlineData("calm", "dp", "none")]
    [InlineData("working", "dp", "none")]
    [InlineData("emergency", "dp", "none")]
    public void GreedySchedule_CompletesScenariosWithExactAcceptedData(string scenario, string packer, string codec)
    {
        var report = new SessionRunner(DataKit.Config, Options("greedy", packer, codec))
            .Run(DataKit.Session, DataKit.Config.Scenario(scenario), 425243);
        Assert.True(report.ExactRestore);
        Assert.Empty(report.Rejections);
        Assert.Equal(3000, report.PackedFrameCount);
        Assert.Equal(report.PacketsBuilt, report.Channel.Sent + report.Channel.OverBudget);
        Assert.Equal(report.Channel.Sent - report.Channel.Lost, report.PacketsAccepted);
        Assert.InRange(report.Q, 0.0, 1.0);
        if (scenario == "calm") Assert.Equal(3000, report.RecoveredFrameCount);
        if (scenario == "emergency") Assert.True(report.Channel.OverBudget > 0);
    }

    [Fact]
    public void PerfectChannel_BothSchedulesDeliverSameFramesAndValue()
    {
        var scenario = DataKit.Config.Scenario("calm");
        var sequential = new SessionRunner(DataKit.Config, Options("sequential")).Run(DataKit.Session, scenario, 1, new PerfectChannel());
        var greedy = new SessionRunner(DataKit.Config, Options("greedy")).Run(DataKit.Session, scenario, 1, new PerfectChannel());
        Assert.Equal(sequential.BuiltBits, greedy.BuiltBits);
        Assert.Equal(sequential.SentBits, greedy.SentBits);
        Assert.Equal(sequential.DeliveredValue, greedy.DeliveredValue);
        Assert.Equal(sequential.RecoveredFrameCount, greedy.RecoveredFrameCount);
        Assert.Equal(1.0, greedy.R);
    }

    [Fact]
    public void SameSeed_ReproducesGreedyDeliveryCounts()
    {
        var runner = new SessionRunner(DataKit.Config, Options("greedy"));
        var first = runner.Run(DataKit.Session, DataKit.Config.Scenario("emergency"), 425243);
        var second = runner.Run(DataKit.Session, DataKit.Config.Scenario("emergency"), 425243);
        Assert.Equal(first.Channel, second.Channel);
        Assert.Equal(first.SentBits, second.SentBits);
        Assert.Equal(first.RecoveredCriticalFrameCount, second.RecoveredCriticalFrameCount);
    }
}

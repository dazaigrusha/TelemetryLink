using TelemetryLink.Core.Channel;
using TelemetryLink.Core.Configuration;

namespace TelemetryLink.Tests;

public class ChannelTests
{
    [Fact]
    public void CalmScenario_LosesNothing()
    {
        var channel = new MarkovChannel(DataKit.Config.Scenario("calm"), 42);
        for (int i = 0; i < 50; i++) Assert.True(channel.Send(new byte[128]).Delivered);
        Assert.Equal(50, channel.Statistics.Sent);
        Assert.Equal(0, channel.Statistics.Lost);
        Assert.Equal(0, channel.Statistics.OverBudget);
    }

    [Fact]
    public void SameSeed_ProducesIdenticalLossesArrivalsAndStatistics()
    {
        var scenario = DataKit.Config.Scenario("emergency") with { TransmissionBudgetSeconds = double.PositiveInfinity };
        var first = new MarkovChannel(scenario, 425243); var second = new MarkovChannel(scenario, 425243);
        for (int i = 0; i < 200; i++) Assert.Equal(first.Send(new byte[100 + i % 30]), second.Send(new byte[100 + i % 30]));
        Assert.Equal(first.Statistics, second.Statistics);
        Assert.True(first.Statistics.Lost > 0);
    }

    [Fact]
    public void BadStateProbability_UsesSpecifiedFormula()
        => Assert.Equal(1.0 / 18, DataKit.Config.Scenario("emergency").BadStateProbability, 12);

    [Fact]
    public void OverBudget_DoesNotConsumeBandwidth_AndSmallerNextPacketCanFit()
    {
        var scenario = DataKit.Config.Scenario("calm") with { BandwidthBitsPerSecond = 8, TransmissionBudgetSeconds = 10 };
        var channel = new MarkovChannel(scenario, 1);
        Assert.False(channel.Send(new byte[11]).Delivered);
        Assert.Equal(0, channel.Statistics.Sent);
        Assert.Equal(0.0, channel.Statistics.ChannelTimeSeconds);
        Assert.True(channel.Send(new byte[10]).Delivered);
        Assert.Equal(1, channel.Statistics.Sent);
        Assert.Equal(1, channel.Statistics.OverBudget);
        Assert.Equal(10.0, channel.Statistics.ChannelTimeSeconds);
    }

    [Fact]
    public void LostPacket_StillConsumesBandwidth()
    {
        var scenario = DataKit.Config.Scenario("calm") with { LossProbability = 1, BandwidthBitsPerSecond = 8 };
        var channel = new MarkovChannel(scenario, 1);
        Assert.False(channel.Send(new byte[10]).Delivered);
        Assert.Equal(1, channel.Statistics.Sent);
        Assert.Equal(1, channel.Statistics.Lost);
        Assert.Equal(10.0, channel.Statistics.ChannelTimeSeconds);
    }
}

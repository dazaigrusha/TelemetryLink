using TelemetryLink.Core;
using TelemetryLink.Core.Channel;
using TelemetryLink.Core.Format;
using TelemetryLink.Core.InputOutput;
using TelemetryLink.Core.Metrics;
using TelemetryLink.Core.Pipeline;

namespace TelemetryLink.Tests;

public class QualityMetricTests
{
    private static PipelineOptions Options() => new() { Codec = AlgorithmCatalog.Codec("none"),
        Packer = AlgorithmCatalog.Packer("sequential"), Scheduler = AlgorithmCatalog.Scheduler("sequential"),
        Classifier = AlgorithmCatalog.Classifier("majority") };

    [Fact]
    public void Q_EqualsWeightedSum_OfComponents()
        => Assert.Equal(0.5 * 0.3 + 0.35 * 0.8 + 0.15 * 0.9,
            QualityMetric.Combine(DataKit.Config, 0.3, 0.8, 0.9), 12);

    [Fact]
    public void Q_IsZero_WhenNoCriticalFrameRecovered()
        => Assert.Equal(0.0, QualityMetric.Combine(DataKit.Config, 1, 0, 1));

    [Fact]
    public void ReceiverException_IsReportedWithZeroQuality_AndNoFurtherDecoding()
    {
        int calls = 0;
        var report = new SessionRunner(DataKit.Config, Options()).Run(
            new TelemetrySession(DataKit.Session.Frames.Take(100).ToArray(), null), DataKit.Config.Scenario("calm"), 1,
            new PerfectChannel(), (packet, keys) =>
            {
                calls++;
                if (calls == 2) throw new InvalidOperationException("private packet data");
                return PacketReader.Read(packet, keys);
            });
        Assert.True(report.ReceiverCrashed);
        Assert.False(report.ExactRestore);
        Assert.Equal(0.0, report.Q);
        Assert.Equal(2, calls);
        Assert.Equal(1, report.PacketsAccepted);
        Assert.True(report.RecoveredCriticalFrameCount > 0);
        Assert.True(report.R > 0); // Q=0 саме через збій, а не відсутність critical.
        Assert.Equal(report.BuiltBits, report.SentBits);
    }

    [Fact]
    public void NormalRejection_IsDifferentFromReceiverCrash_ButQualityIsZero()
    {
        var report = new SessionRunner(DataKit.Config, Options()).Run(
            new TelemetrySession(DataKit.Session.Frames.Take(1).ToArray(), null), DataKit.Config.Scenario("calm"), 1,
            new PerfectChannel(), (packet, keys) =>
            {
                byte[] changed = packet.ToArray(); changed[^1] ^= 1;
                return PacketReader.Read(changed, keys);
            });
        Assert.False(report.ReceiverCrashed);
        Assert.False(report.ExactRestore);
        Assert.Equal(0.0, report.Q);
        Assert.Equal(1, report.Rejections[RejectionCode.E_HMAC]);
    }

    [Fact]
    public void ComponentsStayNormalized_OnManyValidInputs()
    {
        for (int i = 0; i <= 100; i++)
        {
            Assert.InRange(QualityMetric.TransmissionEfficiency(i * 96L, (100 - i) * 100L), 0.0, 1.0);
            Assert.InRange(QualityMetric.CriticalRecovery(i, 100), 0.0, 1.0);
            Assert.InRange(QualityMetric.Speed(20, i), 0.0, 1.0);
        }
    }

    [Fact]
    public void RecoveryIsOne_WhenAllCriticalFramesArrive_OrThereAreNone()
    {
        Assert.Equal(1.0, QualityMetric.CriticalRecovery(7, 7));
        Assert.Equal(1.0, QualityMetric.CriticalRecovery(0, 0));
    }
}

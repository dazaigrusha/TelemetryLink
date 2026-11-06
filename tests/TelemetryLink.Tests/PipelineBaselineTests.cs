using System.Text.Json.Nodes;
using TelemetryLink.Core.Channel;
using TelemetryLink.Core.Compression;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Format;
using TelemetryLink.Core.InputOutput;
using TelemetryLink.Core.Metrics;
using TelemetryLink.Core.Packing;
using TelemetryLink.Core.Pipeline;
using TelemetryLink.Core.Prediction;
using TelemetryLink.Core.Scheduling;
using TelemetryLink.Core.Security;

namespace TelemetryLink.Tests;

public class PipelineBaselineTests
{
    private static PipelineOptions Options(string codec = "huffman", Tuning? tuning = null) => new()
    {
        Codec = codec == "none" ? new NoCompressionCodec() : new HuffmanCodec(),
        Packer = new SequentialPacker(), Scheduler = new SequentialScheduler(),
        Classifier = new MajorityClassifier(), Tuning = tuning ?? Tuning.Default
    };

    private static TelemetryFrame[] Frames(int count, bool critical = true)
        => Enumerable.Range(0, count).Select(i => new TelemetryFrame((uint)i, 1000, 215, -3, 1,
            (byte)(critical ? 1 : 0))).ToArray();

    private static (IReadOnlyList<Packet> Packets, SessionKeys Keys) Pack(
        TelemetryFrame[] frames, PipelineOptions? options = null, TeamConfig? config = null)
    {
        byte[] salt = new byte[16];
        var keys = SessionKeys.Derive("test", salt);
        return (new Transmitter(config ?? DataKit.Config, options ?? Options(), keys, salt, new byte[8])
            .BuildPackets(frames), keys);
    }

    private static TeamConfig WithPacketLimit(int maxBytes)
    {
        var json = JsonNode.Parse(File.ReadAllText(DataKit.Path("team_config.json")))!;
        json["пакування"]!["max_packet_bytes"] = maxBytes;
        json["пакування"]!["max_payload_bytes"] = maxBytes - PacketWriter.MinimumPacketSize;
        string path = Path.GetTempFileName();
        try { File.WriteAllText(path, json.ToJsonString()); return TeamConfig.Load(path); }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("calm", "huffman")]
    [InlineData("working", "huffman")]
    [InlineData("emergency", "huffman")]
    [InlineData("calm", "none")]
    [InlineData("working", "none")]
    [InlineData("emergency", "none")]
    public void FullSession_CompletesWithAccountingAndQuality(string scenario, string codec)
    {
        var report = new SessionRunner(DataKit.Config, Options(codec)).Run(DataKit.Session,
            DataKit.Config.Scenario(scenario), DataKit.Config.ChannelSeed);
        Assert.True(report.ExactRestore);
        Assert.Empty(report.Rejections);
        Assert.Equal(3000, report.PackedFrameCount);
        Assert.Equal(report.PacketsBuilt, report.Channel.Sent + report.Channel.OverBudget);
        Assert.Equal(report.Channel.Sent - report.Channel.Lost, report.PacketsAccepted);
        Assert.InRange(report.Q, 0.0, 1.0);
        Assert.Equal(report.S, QualityMetric.TransmissionEfficiency(report.ReferenceBits, report.SentBits));
        Assert.Equal(report.R, QualityMetric.CriticalRecovery(report.RecoveredCriticalFrameCount, report.CriticalFrameCount));
        Assert.Equal(report.T, QualityMetric.Speed(DataKit.Config.ReferenceTimeSeconds, report.ElapsedSeconds));
        if (scenario == "calm")
        {
            Assert.Equal(3000, report.RecoveredFrameCount);
            Assert.Equal(300, report.RecoveredCriticalFrameCount);
            Assert.Equal(1.0, report.R);
        }
        if (scenario == "emergency") Assert.True(report.Channel.OverBudget > 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(64)]
    [InlineData(300)]
    public void Packetization_PreservesIndexesBytesSizeAndNonceUniqueness(int count)
    {
        var frames = Frames(count);
        var (packets, keys) = Pack(frames);
        Assert.Equal(Enumerable.Range(0, count), packets.SelectMany(p => p.FrameIndexes).OrderBy(i => i));
        Assert.NotEmpty(packets);
        Assert.Equal(packets.Count, packets.Select(p => PacketHeader.ReadFrom(p.Bytes).NonceHex).Distinct().Count());
        foreach (var packet in packets)
        {
            Assert.InRange(packet.Bytes.Length, PacketWriter.MinimumPacketSize, DataKit.Config.MaxPacketBytes);
            var received = PacketReader.Read(packet.Bytes, keys);
            Assert.True(received.IsAccepted);
            Assert.Equal(packet.Sequence, received.Header.PacketSequence);
            Assert.Equal(TelemetryFrame.Serialize(packet.FrameIndexes.Select(i => frames[i]).ToArray()),
                TelemetryFrame.Serialize(received.Frames));
        }
    }

    [Fact]
    public void DuplicateFrameValues_AreCountedAsSeparateFrames()
    {
        var identical = Enumerable.Repeat(new TelemetryFrame(0, 0, 0, 0, 0, 1), 90).ToArray();
        var report = new SessionRunner(DataKit.Config, Options()).Run(new TelemetrySession(identical, null),
            DataKit.Config.Scenario("calm"), 1, new PerfectChannel());
        Assert.Equal(90, report.RecoveredFrameCount);
        Assert.Equal(90, report.RecoveredCriticalFrameCount);
    }

    [Fact]
    public void FirstPacket_UsesTableFromEntireCandidateWindow()
    {
        var frames = DataKit.Session.Frames.Take(64).ToArray();
        var (packets, keys) = Pack(frames);
        var payload = PacketReader.Read(packets[0].Bytes, keys).Payload!;
        Assert.Equal(PacketFlags.None, payload.Flags);
        Assert.True(payload.FrameCount < 64);
        var expected = new HuffmanCodec().BuildTable(TelemetryFrame.Serialize(frames));
        Assert.Equal(expected.Entries.ToArray(), payload.Table.Entries.ToArray());
    }

    [Fact]
    public void EmptySession_ProducesOneValidPacketAndFiniteMetrics()
    {
        var report = new SessionRunner(DataKit.Config, Options()).Run(new TelemetrySession([], null),
            DataKit.Config.Scenario("calm"), 1);
        Assert.Equal(1, report.PacketsBuilt);
        Assert.Equal(0, report.RecoveredFrameCount);
        Assert.Equal(1.0, report.R);
        Assert.True(report.ExactRestore);
        Assert.True(double.IsFinite(report.Q));
    }

    [Fact]
    public void NoCriticalFrames_RecoveryIsOne()
    {
        var report = new SessionRunner(DataKit.Config, Options()).Run(new TelemetrySession(Frames(30, false), null),
            DataKit.Config.Scenario("calm"), 1);
        Assert.Equal(0, report.CriticalFrameCount);
        Assert.Equal(1.0, report.R);
        Assert.True(report.Q > 0);
    }

    [Fact]
    public void SameChannelSeed_ReproducesDeliveryCountsDespiteRandomEncryptionSalt()
    {
        var runner = new SessionRunner(DataKit.Config, Options());
        var first = runner.Run(DataKit.Session, DataKit.Config.Scenario("emergency"), 425243);
        var second = runner.Run(DataKit.Session, DataKit.Config.Scenario("emergency"), 425243);
        Assert.Equal(first.Channel, second.Channel);
        Assert.Equal(first.SentBits, second.SentBits);
        Assert.Equal(first.BuiltBits, second.BuiltBits);
        Assert.Equal(first.RecoveredFrameCount, second.RecoveredFrameCount);
        Assert.Equal(first.RecoveredCriticalFrameCount, second.RecoveredCriticalFrameCount);
        // Час обчислень, T та Q навмисно не порівнюються.
    }

    [Fact]
    public void ZeroBudget_DoesNotConsumeBandwidthAndQualityIsZero()
    {
        var scenario = DataKit.Config.Scenario("calm") with { TransmissionBudgetSeconds = 0 };
        var report = new SessionRunner(DataKit.Config, Options()).Run(new TelemetrySession(Frames(50), null), scenario, 1);
        Assert.Equal(0, report.SentBits);
        Assert.Equal(0, report.Channel.Sent);
        Assert.Equal(report.PacketsBuilt, report.Channel.OverBudget);
        Assert.Equal(0.0, report.Q);
        Assert.Equal(0, report.RecoveredCriticalFrameCount);
    }

    [Fact]
    public void OverBudgetPacket_DoesNotPreventSmallerLaterPacketFromBeingSent()
    {
        // 36 сирих кадрів → пакети 509 та 101 байт. Перший не вміщається,
        // другий ще може використати весь незайнятий бюджет.
        var scenario = DataKit.Config.Scenario("calm") with
        { BandwidthBitsPerSecond = 8, TransmissionBudgetSeconds = 101 };
        var report = new SessionRunner(DataKit.Config, Options("none")).Run(
            new TelemetrySession(Frames(36), null), scenario, 1);
        Assert.Equal(2, report.PacketsBuilt);
        Assert.Equal(1, report.Channel.OverBudget);
        Assert.Equal(1, report.Channel.Sent);
        Assert.Equal(808L, report.SentBits);
        Assert.Equal(1, report.RecoveredFrameCount);
        Assert.Equal(101.0, report.Channel.ChannelTimeSeconds);
        Assert.Equal(QualityMetric.Speed(20, report.ElapsedSeconds), report.T);
    }

    private sealed class LosingChannel : IChannel
    {
        private int _sent;
        public ChannelStatistics Statistics => new(_sent, _sent, 0, 0, double.PositiveInfinity);
        public TransmissionResult Send(ReadOnlySpan<byte> packet) { _sent++; return new(false, null); }
    }

    [Fact]
    public void LostPackets_ConsumeBandwidthAndQualityIsZero()
    {
        var report = new SessionRunner(DataKit.Config, Options("none")).Run(new TelemetrySession(Frames(100), null),
            DataKit.Config.Scenario("calm"), 1, new LosingChannel());
        Assert.Equal(report.BuiltBits, report.SentBits);
        Assert.Equal(report.PacketsBuilt, report.Channel.Lost);
        Assert.Equal(0.0, report.Q);
    }

    [Fact]
    public void SmallPacketLimit_FallsBackToRawAndStillRestores()
    {
        var config = WithPacketLimit(120);
        var frames = DataKit.Session.Frames.Take(64).ToArray();
        var (packets, keys) = Pack(frames, config: config);
        Assert.Contains(packets, p => (PacketReader.Read(p.Bytes, keys).Payload!.Flags & PacketFlags.NoCompression) != 0);
        Assert.All(packets, p => Assert.True(p.Bytes.Length <= 120));
        Assert.Equal(64, packets.Sum(p => PacketReader.Read(p.Bytes, keys).Frames.Count));
    }

    [Fact]
    public void ImpossiblePacketLimit_IsRejectedWithoutLooping()
        => Assert.Throws<ArgumentException>(() => Pack(Frames(1), Options("none"), WithPacketLimit(100)));

    [Fact]
    public void HugeCompressionThreshold_ForcesRawMode()
    {
        var (packets, keys) = Pack(Frames(100), Options(tuning: new Tuning(64, 1000, 1)));
        Assert.All(packets, p => Assert.Equal(PacketFlags.NoCompression, PacketReader.Read(p.Bytes, keys).Payload!.Flags));
    }

    [Fact]
    public void SequentialPacker_StopsAtFirstOversizedItem()
    {
        var selected = new SequentialPacker().Select(new[] {
            new FrameCandidate(0, 10, 1), new FrameCandidate(1, 100, 9), new FrameCandidate(2, 10, 1) }, 30);
        Assert.Equal(new[] { 0 }, selected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SequentialPacker_NonPositiveCapacity_ReturnsEmpty(int capacity)
        => Assert.Empty(new SequentialPacker().Select(new[] { new FrameCandidate(0, 96, 1) }, capacity));

    [Fact]
    public void Receiver_CountsAcceptedAndRejectedPackets()
    {
        var (packets, keys) = Pack(Frames(1));
        var receiver = new Receiver(keys);
        Assert.True(receiver.Accept(packets[0].Bytes).IsAccepted);
        byte[] bad = (byte[])packets[0].Bytes.Clone(); bad[^1] ^= 1;
        Assert.Equal(RejectionCode.E_HMAC, receiver.Accept(bad).Code);
        Assert.Equal(1, receiver.AcceptedCount);
        Assert.Equal(1, receiver.Rejections[RejectionCode.E_HMAC]);
    }

    [Fact]
    public void Metric_KnownNumbersAndZeroEdges()
    {
        Assert.Equal(0.4, QualityMetric.TransmissionEfficiency(200, 300));
        Assert.Equal(1.0, QualityMetric.TransmissionEfficiency(0, 0));
        Assert.InRange(QualityMetric.TransmissionEfficiency(long.MaxValue, long.MaxValue), 0.49999, 0.50001);
        Assert.Equal(0.75, QualityMetric.CriticalRecovery(3, 4));
        Assert.Equal(1.0, QualityMetric.CriticalRecovery(0, 0));
        Assert.Equal(0.8, QualityMetric.Speed(20, 5));
        Assert.Equal(1.0, QualityMetric.Speed(20, 0));
        Assert.Equal(0.5 * 0.4 + 0.35 * 0.75 + 0.15 * 0.8,
            QualityMetric.Combine(DataKit.Config, 0.4, 0.75, 0.8), 12);
        Assert.Equal(0.0, QualityMetric.Combine(DataKit.Config, 1, 0, 1));
    }

    [Fact]
    public void InvalidWindow_IsRejected()
        => Assert.Throws<ArgumentException>(() => Pack(Frames(1), Options(tuning: new Tuning(0, 1, 1))));
}

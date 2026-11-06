using System.Text.Json.Nodes;
using TelemetryLink.Core;
using TelemetryLink.Core.Channel;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Format;
using TelemetryLink.Core.InputOutput;
using TelemetryLink.Core.Pipeline;
using TelemetryLink.Core.Prediction;
using TelemetryLink.Core.Scheduling;
using TelemetryLink.Core.Security;

namespace TelemetryLink.Tests;

public class TransmitterTests
{
    private static PipelineOptions Options(string packer, string codec = "huffman", int window = 64) => new()
    {
        Codec = AlgorithmCatalog.Codec(codec), Packer = AlgorithmCatalog.Packer(packer),
        Scheduler = new SequentialScheduler(), Classifier = new MajorityClassifier(),
        Tuning = new Tuning(window, Tuning.Default.CompressionThreshold, 1)
    };

    [Theory]
    [InlineData("dp", "huffman")]
    [InlineData("greedy", "huffman")]
    [InlineData("brute", "huffman")]
    [InlineData("dp", "none")]
    [InlineData("greedy", "none")]
    [InlineData("brute", "none")]
    public void EveryPacker_PreservesAllIndexes_StaysWithinLimit_AndRoundTrips(string packer, string codec)
    {
        var frames = DataKit.Session.Frames.Take(97).ToArray();
        byte[] salt = new byte[16];
        var keys = SessionKeys.Derive("integration", salt);
        var options = Options(packer, codec, packer == "brute" ? 8 : 64);
        var packets = new Transmitter(DataKit.Config, options, keys, salt, new byte[8]).BuildPackets(frames);
        Assert.Equal(Enumerable.Range(0, frames.Length), packets.SelectMany(p => p.FrameIndexes).OrderBy(i => i));
        Assert.Equal(packets.Count, packets.Select(p => PacketHeader.ReadFrom(p.Bytes).NonceHex).Distinct().Count());
        foreach (var packet in packets)
        {
            Assert.True(packet.SizeInBytes <= DataKit.Config.MaxPacketBytes);
            var restored = PacketReader.Read(packet.Bytes, keys);
            Assert.True(restored.IsAccepted);
            Assert.Equal(TelemetryFrame.Serialize(packet.FrameIndexes.Select(i => frames[i]).ToArray()),
                TelemetryFrame.Serialize(restored.Frames));
        }
    }

    [Theory]
    [InlineData("dp", 0)]
    [InlineData("greedy", 0)]
    [InlineData("brute", 0)]
    [InlineData("dp", 1)]
    [InlineData("greedy", 1)]
    [InlineData("brute", 1)]
    public void EmptyAndSingleFrameStreams_WorkThroughEntirePipeline(string packer, int count)
    {
        var frames = DataKit.Session.Frames.Take(count).ToArray();
        var report = new SessionRunner(DataKit.Config, Options(packer)).Run(new TelemetrySession(frames, null),
            DataKit.Config.Scenario("calm"), 1, new PerfectChannel());
        Assert.True(report.ExactRestore);
        Assert.Equal(count, report.RecoveredFrameCount);
        Assert.Equal(1, report.PacketsBuilt);
    }

    [Theory]
    [InlineData("dp", "calm")]
    [InlineData("dp", "working")]
    [InlineData("dp", "emergency")]
    [InlineData("greedy", "calm")]
    [InlineData("greedy", "working")]
    [InlineData("greedy", "emergency")]
    public void FullSession_NewPackersCompleteAllScenarios(string packer, string scenario)
    {
        var report = new SessionRunner(DataKit.Config, Options(packer)).Run(DataKit.Session,
            DataKit.Config.Scenario(scenario), DataKit.Config.ChannelSeed);
        Assert.True(report.ExactRestore);
        Assert.Equal(3000, report.PackedFrameCount);
        Assert.InRange(report.Q, 0.0, 1.0);
        if (scenario == "calm") Assert.Equal(3000, report.RecoveredFrameCount);
    }

    [Theory]
    [InlineData("dp")]
    [InlineData("greedy")]
    public void SmallLimit_UsesRawFallbackWithoutLosingFrames(string packer)
    {
        var json = JsonNode.Parse(File.ReadAllText(DataKit.Path("team_config.json")))!;
        json["пакування"]!["max_packet_bytes"] = 120;
        json["пакування"]!["max_payload_bytes"] = 45;
        string path = Path.GetTempFileName();
        TeamConfig config;
        try { File.WriteAllText(path, json.ToJsonString()); config = TeamConfig.Load(path); }
        finally { File.Delete(path); }
        var salt = new byte[16];
        var keys = SessionKeys.Derive("small", salt);
        var frames = DataKit.Session.Frames.Take(64).ToArray();
        var packets = new Transmitter(config, Options(packer), keys, salt, new byte[8]).BuildPackets(frames);
        Assert.All(packets, p => Assert.InRange(p.SizeInBytes, 75, 120));
        Assert.Contains(packets, p => (PacketReader.Read(p.Bytes, keys).Payload!.Flags & PacketFlags.NoCompression) != 0);
        Assert.Equal(64, packets.Sum(p => PacketReader.Read(p.Bytes, keys).Frames.Count));
    }

    [Fact]
    public void BruteForce_Default64FrameWindow_GivesClearValidationError()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SessionRunner(DataKit.Config, Options("brute"))
            .Run(DataKit.Session, DataKit.Config.Scenario("calm"), 1));
        Assert.Contains("20", ex.Message);
    }
}

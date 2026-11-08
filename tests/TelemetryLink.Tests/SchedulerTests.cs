using TelemetryLink.Core.Channel;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Format;
using TelemetryLink.Core.Packing;
using TelemetryLink.Core.Pipeline;
using TelemetryLink.Core.Prediction;
using TelemetryLink.Core.Compression;
using TelemetryLink.Core.Scheduling;
using TelemetryLink.Core.Security;

namespace TelemetryLink.Tests;

public class SchedulerTests
{
    private static Packet P(uint sequence, int bytes, int value) => new(sequence, new byte[bytes], [], value);

    [Fact]
    public void Sequential_PreservesInputOrder()
    {
        Packet[] packets = [P(3, 10, 1), P(1, 10, 5), P(2, 10, 9)];
        Assert.Equal(packets, new SequentialScheduler().Order(packets));
    }

    [Fact]
    public void ValueDensity_PutsHighestDensityFirst()
    {
        Packet[] packets = [P(1, 100, 100), P(2, 10, 30), P(3, 20, 50)];
        Assert.Equal(new uint[] { 2, 3, 1 }, new ValueDensityScheduler().Order(packets).Select(p => p.Sequence));
    }

    [Fact]
    public void EqualDensities_AreOrderedBySequence()
    {
        Packet[] packets = [P(8, 10, 20), P(2, 20, 40), P(5, 5, 10)];
        Assert.Equal(new uint[] { 2, 5, 8 }, new ValueDensityScheduler().Order(packets).Select(p => p.Sequence));
    }

    [Fact]
    public void IdenticalSequenceAndDensity_PreserveInputOrder()
    {
        var first = P(1, 10, 20); var second = P(1, 20, 40);
        var result = new ValueDensityScheduler().Order([first, second]);
        Assert.Same(first, result[0]); Assert.Same(second, result[1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyAndSinglePacket_Work(bool greedy)
    {
        ISendScheduler scheduler = greedy ? new ValueDensityScheduler() : new SequentialScheduler();
        Assert.Empty(scheduler.Order([]));
        var packet = P(4, 10, 1);
        Assert.Same(packet, Assert.Single(scheduler.Order([packet])));
        Assert.Throws<ArgumentNullException>(() => scheduler.Order(null!));
    }

    [Fact]
    public void Order_IsPermutationWithoutMutatingInput_On200RandomSets()
    {
        var random = new Random(71);
        for (int sample = 0; sample < 200; sample++)
        {
            var packets = Enumerable.Range(0, random.Next(0, 65))
                .Select(i => P((uint)i, random.Next(1, 513), random.Next(0, 100))).OrderBy(_ => random.Next()).ToArray();
            var snapshot = packets.ToArray();
            var ordered = new ValueDensityScheduler().Order(packets);
            Assert.Equal(snapshot, packets);
            Assert.Equal(packets.Length, ordered.Count);
            Assert.Equal(packets.Select(p => p.Sequence).OrderBy(i => i), ordered.Select(p => p.Sequence).OrderBy(i => i));
            foreach (var packet in ordered) Assert.Same(packets.Single(p => p.Sequence == packet.Sequence), packet);
            for (int i = 1; i < ordered.Count; i++)
            {
                Assert.True((long)ordered[i - 1].Value * ordered[i].SizeInBytes >= (long)ordered[i].Value * ordered[i - 1].SizeInBytes);
            }
            Assert.Equal(ordered, new ValueDensityScheduler().Order(packets));
        }
    }

    [Fact]
    public void ZeroLengthPacket_UsesDomainZeroDensityRule()
    {
        var empty = P(0, 0, 100); var valuable = P(2, 10, 1); var zero = P(1, 5, 0);
        Assert.Equal(new uint[] { 2, 0, 1 }, new ValueDensityScheduler().Order([empty, zero, valuable]).Select(p => p.Sequence));
    }

    [Fact]
    public void LargeValues_DoNotOverflowRatioComparison()
    {
        Packet[] packets = [P(0, 100000, int.MaxValue), P(1, 50000, int.MaxValue - 1)];
        Assert.Equal(new uint[] { 1, 0 }, new ValueDensityScheduler().Order(packets).Select(p => p.Sequence));
    }

    [Fact]
    public void SortingEncryptedPackets_PreservesBytesHeaderSequenceAndHmac()
    {
        byte[] salt = new byte[16];
        var keys = SessionKeys.Derive("scheduler", salt);
        var options = new PipelineOptions { Codec = new HuffmanCodec(), Packer = new DynamicProgrammingPacker(),
            Scheduler = new SequentialScheduler(), Classifier = new MajorityClassifier() };
        var packets = new Transmitter(DataKit.Config, options, keys, salt, new byte[8])
            .BuildPackets(DataKit.Session.Frames.Take(200).ToArray());
        var bytesBefore = packets.ToDictionary(p => p.Sequence, p => p.Bytes.ToArray());
        foreach (var packet in new ValueDensityScheduler().Order(packets.Reverse().ToArray()))
        {
            Assert.Equal(bytesBefore[packet.Sequence], packet.Bytes);
            var accepted = PacketReader.Read(packet.Bytes, keys);
            Assert.True(accepted.IsAccepted);
            Assert.Equal(packet.Sequence, accepted.Header.PacketSequence);
        }
    }

    [Fact]
    public void LimitedBudget_ExampleDeliversHigherValueWithGreedy()
    {
        Packet[] packets = [P(0, 10, 1), P(1, 10, 9)];
        var scenario = DataKit.Config.Scenario("calm") with { BandwidthBitsPerSecond = 8, TransmissionBudgetSeconds = 10 };
        int Deliver(ISendScheduler scheduler)
        {
            var channel = new MarkovChannel(scenario, 1);
            return scheduler.Order(packets).Where(p => channel.Send(p.Bytes).Delivered).Sum(p => p.Value);
        }
        Assert.Equal(1, Deliver(new SequentialScheduler()));
        Assert.Equal(9, Deliver(new ValueDensityScheduler()));
    }
}

using TelemetryLink.Core;
using TelemetryLink.Core.Channel;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Format;
using TelemetryLink.Core.InputOutput;
using TelemetryLink.Core.Metrics;
using TelemetryLink.Core.Pipeline;
using TelemetryLink.Core.Prediction;
using TelemetryLink.Core.Scheduling;
using TelemetryLink.Core.Security;
namespace TelemetryLink.Tests;

public class CriticalDeliveryTests
{
    static PipelineOptions Options(DeliveryPolicy delivery = DeliveryPolicy.CriticalFirst) => new()
    {
        Codec = AlgorithmCatalog.Codec("huffman"), Packer = AlgorithmCatalog.Packer("dp"),
        Scheduler = AlgorithmCatalog.Scheduler("greedy"), Classifier = new MajorityClassifier(), Delivery = delivery
    };
    sealed class RecordingClassifier : IFrameClassifier
    {
        public string Name => "recording"; public int Calls; public TelemetryFrame[] Seen = [];
        public FrameClass[] Classify(IReadOnlyList<TelemetryFrame> frames)
        { Calls++; Seen = frames.ToArray(); return frames.Select((_, i) => (FrameClass)(i % 3)).ToArray(); }
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void GroupingPreservesIndexesClassesWireFormatAndPacketLimits(int mode)
    {
        var frames = DataKit.Session.Frames.Take(151).Select((f,i) => f with { Critical = (byte)(mode == 0 ? 0 : mode == 1 ? 1 : i % 3 == 0 ? 1 : 0) }).ToArray();
        var classifier = new RecordingClassifier();var options = Options() with { Classifier = classifier };
        var keys = SessionKeys.Derive("test",new byte[16]);
        var transmitter = new Transmitter(DataKit.Config,options,keys,new byte[16],new byte[8]);
        var packets = transmitter.BuildPackets(frames);
        Assert.Equal(1,classifier.Calls);Assert.Equal(frames,classifier.Seen);
        Assert.Equal(Enumerable.Range(0,frames.Length),packets.SelectMany(p=>p.FrameIndexes).OrderBy(i=>i));
        Assert.Equal(Enumerable.Range(0,frames.Length).Select(i=>(FrameClass)(i%3)),transmitter.LastClasses);
        Assert.Equal(packets.Count,packets.Select(p=>PacketHeader.ReadFrom(p.Bytes).NonceHex).Distinct().Count());
        foreach(var p in packets)
        {
            Assert.Single(p.FrameIndexes.Select(i=>frames[i].Critical).Distinct());
            if (p.FrameIndexes.Any(i=>frames[i].IsCritical)) Assert.InRange(p.FrameIndexes.Count,1,DeliveryPolicies.MaxCriticalFramesPerPacket);
            Assert.True(p.SizeInBytes<=DataKit.Config.MaxPacketBytes);
            Assert.Equal(p.FrameIndexes.OrderBy(i=>i),p.FrameIndexes);
            var result=PacketReader.Read(p.Bytes,keys);Assert.True(result.IsAccepted);
            Assert.Equal(TelemetryFrame.Serialize(p.FrameIndexes.Select(i=>frames[i]).ToArray()),TelemetryFrame.Serialize(result.Frames));
            int expected=p.FrameIndexes.Sum(i=>DataKit.Config.ValueOf((FrameClass)(i%3),false)+(frames[i].IsCritical?DataKit.Config.CriticalBonus:0));
            Assert.Equal(expected,p.Value);
        }
    }
    sealed class ReverseScheduler : ISendScheduler
    {
        public string Name=>"reverse";
        public IReadOnlyList<Packet> Order(IReadOnlyList<Packet> packets)=>packets.Reverse().ToArray();
    }
    sealed class RecordingChannel : IChannel
    {
        public readonly List<byte[]> Bytes=[];
        public ChannelStatistics Statistics=>new(Bytes.Count,0,0,0,double.PositiveInfinity);
        public TransmissionResult Send(ReadOnlySpan<byte> packet){Bytes.Add(packet.ToArray());return new(true,0);}
    }
    [Fact] public void CriticalTierPrecedesNormalEvenWhenInnerSchedulerReverses()
    {
        var frames=DataKit.Session.Frames.Take(151).ToArray();var channel=new RecordingChannel();
        var options=Options() with { Scheduler=new ReverseScheduler() };
        var report=new SessionRunner(DataKit.Config,options).Run(new(frames,null),DataKit.Config.Scenario("calm"),1,channel);
        bool seenNormal=false;var keys=SessionKeys.Derive(DataKit.Config.SessionPassword,PacketHeader.ReadFrom(channel.Bytes[0]).Salt);
        foreach(var bytes in channel.Bytes)
        {
            var result=PacketReader.Read(bytes,keys);Assert.True(result.IsAccepted);
            bool critical=result.Frames.Any(f=>f.IsCritical);
            if(!critical)seenNormal=true;else Assert.False(seenNormal);
        }
        Assert.Equal(frames.Length,report.RecoveredFrameCount);Assert.Equal(1,report.R);
        Assert.Equal(report.FrameCount,report.Diagnostics!.Frames.Total);
        Assert.Equal(report.CriticalFrameCount,report.Diagnostics.CriticalFrames.Recovered);
    }
    [Theory] [InlineData(DeliveryPolicy.Balanced)] [InlineData(DeliveryPolicy.CriticalFirst)]
    public void RealEmergencyDiagnosticsPartitionAllFramesAndStayReproducible(DeliveryPolicy delivery)
    {
        var runner=new SessionRunner(DataKit.Config,Options(delivery));
        var a=runner.Run(DataKit.Session,DataKit.Config.Scenario("emergency"),425243);
        var b=runner.Run(DataKit.Session,DataKit.Config.Scenario("emergency"),425243);
        Assert.Equal(a.Diagnostics,b.Diagnostics);Assert.Equal(a.Channel,b.Channel);
        Assert.Equal(a.FrameCount,a.Diagnostics!.Frames.Total);Assert.Equal(a.CriticalFrameCount,a.Diagnostics.CriticalFrames.Total);
        Assert.Equal(a.RecoveredCriticalFrameCount,a.Diagnostics.CriticalFrames.Recovered);
        Assert.Equal(0,a.Diagnostics.CriticalFrames.NotPacked);Assert.Equal(0,a.Diagnostics.CriticalFrames.ReceiverRejected);
        Assert.True(a.ExactRestore);
        if(delivery==DeliveryPolicy.CriticalFirst)Assert.Equal(0,a.Diagnostics.CriticalFrames.OverBudget);
    }
    sealed class ScriptedChannel : IChannel
    {
        int index,sent,lost,budget;
        public ChannelStatistics Statistics=>new(sent,lost,budget,0,1);
        public TransmissionResult Send(ReadOnlySpan<byte> packet)
        {
            int current=index++;
            if(current is 2 or 7){budget++;return new(false,null);}
            sent++;if(current==1){lost++;return new(false,null);}return new(true,0);
        }
    }
    [Fact] public void EveryFailureCauseIsCountedSeparately()
    {
        var frames=Enumerable.Range(0,8).Select(i=>new TelemetryFrame((uint)i,0,0,0,0,1)).ToArray();int calls=0;
        var options=Options(DeliveryPolicy.Balanced) with {Codec=AlgorithmCatalog.Codec("none"),Tuning=new(1,.5,1),Scheduler=AlgorithmCatalog.Scheduler("sequential")};
        var report=new SessionRunner(DataKit.Config,options).Run(new(frames,null),DataKit.Config.Scenario("calm"),1,new ScriptedChannel(),(bytes,keys)=>
        {
            calls++;if(calls==2)return ReceiveResult.Rejected(RejectionCode.E_HMAC);
            if(calls==4)throw new InvalidOperationException("failure");
            var result=PacketReader.Read(bytes,keys);
            return calls==3? result with { Frames=[result.Frames[0] with { AltitudeDm=123 }] }:result;
        });
        Assert.Equal(new DeliveryBreakdown(1,2,1,1,2,1,0),report.Diagnostics!.CriticalFrames);
        Assert.Equal(report.Diagnostics.Frames,report.Diagnostics.CriticalFrames);
        Assert.Equal(4,calls);Assert.Equal(0,report.Q);Assert.True(report.ReceiverCrashed);Assert.False(report.ExactRestore);
    }
    [Fact] public void EmptySessionHasZeroDiagnosticCounts()
    {
        var report=new SessionRunner(DataKit.Config,Options()).Run(new TelemetrySession([],null),DataKit.Config.Scenario("calm"),1,new PerfectChannel());
        Assert.Equal(0,report.Diagnostics!.Frames.Total);Assert.Equal(0,report.Diagnostics.CriticalFrames.Total);Assert.True(report.ExactRestore);
    }
    [Fact] public void NoCriticalSessionPreservesBalancedPacking()
    {
        var frames=DataKit.Session.Frames.Take(100).Select(f=>f with { Critical=0 }).ToArray();
        var a=new SessionRunner(DataKit.Config,Options()).Run(new(frames,null),DataKit.Config.Scenario("calm"),1,new PerfectChannel());
        var b=new SessionRunner(DataKit.Config,Options(DeliveryPolicy.Balanced)).Run(new(frames,null),DataKit.Config.Scenario("calm"),1,new PerfectChannel());
        Assert.Equal(a.BuiltBits,b.BuiltBits);Assert.Equal(a.RecoveredFrameCount,b.RecoveredFrameCount);Assert.Equal(0,a.Diagnostics!.CriticalFrames.Total);
    }
}

using TelemetryLink.Core;
using TelemetryLink.Core.Channel;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Format;
using TelemetryLink.Core.InputOutput;
using TelemetryLink.Core.Metrics;
using TelemetryLink.Core.Pipeline;
namespace TelemetryLink.Tests;
public class CriticalRepeatTests
{
    static PipelineOptions Options(int copies=2,int gap=8)=>new()
    {Codec=AlgorithmCatalog.Codec("none"),Packer=AlgorithmCatalog.Packer("sequential"),Scheduler=AlgorithmCatalog.Scheduler("sequential"),
        Classifier=AlgorithmCatalog.Classifier("majority"),Delivery=DeliveryPolicy.CriticalRepeat,CriticalCopies=copies,RepeatGapPackets=gap,Tuning=new(8,.5,1)};
    static TelemetrySession Session(int count=16,byte critical=1)=>new(Enumerable.Range(0,count).Select(i=>new TelemetryFrame((uint)i,0,0,0,0,critical)).ToArray(),null);
    [Fact] public void PlanUsesSeparateRoundsAndExactObjectsWithoutCopyingOrdinaryPackets()
    {
        var frames=Enumerable.Range(0,5).Select(i=>new TelemetryFrame((uint)i,0,0,0,0,(byte)(i%2==0&&i<3?1:0))).ToArray();
        var packets=Enumerable.Range(0,5).Select(i=>new Packet((uint)i,[(byte)i],[i],1)).ToArray();
        Packet[] scheduled=[packets[1],packets[2],packets[3],packets[0],packets[4]];
        var plan=TransmissionPlan.Create(scheduled,frames,Options(3,1));
        Assert.Equal(new uint[]{2,0,1,2,0,3,2,0,4},plan.Select(p=>p.Sequence));
        foreach(var p in plan){Assert.Same(packets[p.Sequence],p);Assert.Same(packets[p.Sequence].Bytes,p.Bytes);}
        Assert.Equal(1,plan.Count(p=>p.Sequence==1));Assert.Equal(1,plan.Count(p=>p.Sequence==3));Assert.Equal(1,plan.Count(p=>p.Sequence==4));
    }
    [Fact] public void EmptyCriticalTierDoesNotDuplicateNormalTraffic()
    {
        var frame=Session(1,0).Frames;Packet[] packets=[new(0,[1],[0],1)];
        Assert.Equal(packets,TransmissionPlan.Create(packets,frame,Options(3,64)));
        Assert.Empty(TransmissionPlan.Create([],[],Options()));
    }
    [Theory] [InlineData(0,8)] [InlineData(4,8)] [InlineData(2,-1)] [InlineData(2,65)]
    public void InvalidSettingsRejected(int copies,int gap)=>Assert.Throws<ArgumentException>(()=>TransmissionPlan.Create([],[],Options(copies,gap)));
    [Fact] public void PerfectChannelPaysForCopiesButCreditsEachFrameAndValueOnce()
    {
        var result=new SessionRunner(DataKit.Config,Options()).Run(Session(),DataKit.Config.Scenario("calm"),1,new PerfectChannel());
        Assert.Equal(2*result.PacketsBuilt,result.TransmissionAttempts);Assert.Equal(result.TransmissionAttempts,result.Channel.Sent);
        Assert.Equal(2*result.BuiltBits,result.SentBits);Assert.Equal(result.BuiltBits,result.RepeatedSentBits);
        Assert.Equal(result.PacketsBuilt,result.SentRepeatAttempts);Assert.Equal(result.PacketsBuilt,result.DuplicateAcceptedPackets);
        Assert.Equal(result.PacketsBuilt,result.UniquePacketsAccepted);Assert.Equal(result.TransmissionAttempts,result.PacketsAccepted);
        Assert.Equal(16,result.RecoveredFrameCount);Assert.Equal(16,result.PackedFrameCount);Assert.Equal(result.SelectedValue,result.DeliveredValue);
        Assert.Equal(0,result.CriticalRecoveredByRepeat);Assert.True(result.ExactRestore);Assert.Equal(1,result.R);
    }
    sealed class ScriptedChannel(Func<int,int> action):IChannel
    {
        int count,sent,lost,budget;
        public ChannelStatistics Statistics=>new(sent,lost,budget,0,1);
        public TransmissionResult Send(ReadOnlySpan<byte> packet)
        {
            int outcome=action(count++);if(outcome==2){budget++;return new(false,null);}
            sent++;if(outcome==1){lost++;return new(false,null);}return new(true,0);
        }
    }
    [Fact] public void LostFirstRoundIsRecoveredBySecondRoundWithoutAck()
    {
        var r=new SessionRunner(DataKit.Config,Options()).Run(Session(),DataKit.Config.Scenario("calm"),1,new ScriptedChannel(i=>i<2?1:0));
        Assert.Equal(16,r.CriticalRecoveredByRepeat);Assert.Equal(16,r.RecoveredByRepeatFrames);Assert.Equal(1,r.R);
        Assert.Equal(r.SelectedValue,r.DeliveredValue);Assert.Equal(0,r.DuplicateAcceptedPackets);
        Assert.Equal(new DeliveryBreakdown(16,0,0,0,0,0,0),r.Diagnostics!.CriticalFrames);
        Assert.Equal(2,r.Channel.Lost);Assert.Equal(4,r.Channel.Sent);Assert.Equal(2*r.BuiltBits,r.SentBits);
    }
    [Fact] public void BudgetRejectedCopyDoesNotUndoSuccessfulFirstRoundOrSpendBits()
    {
        var r=new SessionRunner(DataKit.Config,Options()).Run(Session(),DataKit.Config.Scenario("calm"),1,new ScriptedChannel(i=>i<2?0:2));
        Assert.Equal(1,r.R);Assert.Equal(2,r.PlannedRepeatAttempts);Assert.Equal(0,r.SentRepeatAttempts);
        Assert.Equal(0,r.RepeatedSentBits);Assert.Equal(r.BuiltBits,r.SentBits);Assert.Equal(2,r.Channel.OverBudget);
        Assert.Equal(0,r.Diagnostics!.CriticalFrames.OverBudget);
    }
    [Fact] public void ChannelLossIsNotHiddenByLaterBudgetFailure()
    {
        var r=new SessionRunner(DataKit.Config,Options()).Run(Session(),DataKit.Config.Scenario("calm"),1,new ScriptedChannel(i=>i<2?1:2));
        Assert.Equal(0,r.R);Assert.Equal(0,r.Q);Assert.Equal(16,r.Diagnostics!.CriticalFrames.ChannelLost);
        Assert.Equal(0,r.Diagnostics.CriticalFrames.OverBudget);Assert.Equal(16,r.Diagnostics.CriticalFrames.Total);
    }
    [Fact] public void IntegrityFailureIsNotErasedBySuccessfulRepeat()
    {
        int calls=0;var r=new SessionRunner(DataKit.Config,Options()).Run(Session(8),DataKit.Config.Scenario("calm"),1,new PerfectChannel(),(bytes,keys)=>
        {if(++calls==1){var changed=bytes.ToArray();changed[^1]^=1;return PacketReader.Read(changed,keys);}return PacketReader.Read(bytes,keys);});
        Assert.Equal(1,r.R);Assert.Equal(8,r.CriticalRecoveredByRepeat);Assert.False(r.ExactRestore);Assert.Equal(0,r.Q);
        Assert.Equal(1,r.Rejections[RejectionCode.E_HMAC]);
    }
    [Fact] public void RepeatedPacketMustStillPassAuthentication()
    {
        int calls=0;var r=new SessionRunner(DataKit.Config,Options()).Run(Session(8),DataKit.Config.Scenario("calm"),1,new PerfectChannel(),(bytes,keys)=>
        {if(++calls==2){var changed=bytes.ToArray();changed[^1]^=1;return PacketReader.Read(changed,keys);}return PacketReader.Read(bytes,keys);});
        Assert.Equal(2,calls);Assert.Equal(1,r.R);Assert.False(r.ExactRestore);Assert.Equal(0,r.Q);Assert.Equal(0,r.DuplicateAcceptedPackets);
    }
    [Fact] public void NoCriticalFramesCreateNoRepeatAttempts()
    {
        var r=new SessionRunner(DataKit.Config,Options(3)).Run(Session(100,0),DataKit.Config.Scenario("calm"),1,new PerfectChannel());
        Assert.Equal(0,r.PlannedRepeatAttempts);Assert.Equal(r.BuiltBits,r.SentBits);Assert.Equal(100,r.RecoveredFrameCount);
        Assert.Equal(0,r.Diagnostics!.CriticalFrames.Total);
    }
    [Fact] public void SingleCopyMatchesPreviousPriorityPolicy()
    {
        var a=new SessionRunner(DataKit.Config,Options(1)).Run(DataKit.Session,DataKit.Config.Scenario("emergency"),425243);
        var b=new SessionRunner(DataKit.Config,Options() with{Delivery=DeliveryPolicy.CriticalFirst}).Run(DataKit.Session,DataKit.Config.Scenario("emergency"),425243);
        Assert.Equal(a.Channel,b.Channel);Assert.Equal(a.SentBits,b.SentBits);Assert.Equal(a.Diagnostics,b.Diagnostics);Assert.Equal(0,a.PlannedRepeatAttempts);
    }
    [Fact] public void RealChannelStillUsesOriginalModelAndReproducesCounts()
    {
        var runner=new SessionRunner(DataKit.Config,Options());var a=runner.Run(DataKit.Session,DataKit.Config.Scenario("emergency"),1);
        var b=runner.Run(DataKit.Session,DataKit.Config.Scenario("emergency"),1);
        Assert.Equal(a.Channel,b.Channel);Assert.Equal(a.Diagnostics,b.Diagnostics);Assert.Equal(a.CriticalRecoveredByRepeat,b.CriticalRecoveredByRepeat);
        Assert.Equal(a.TransmissionAttempts,a.Channel.Sent+a.Channel.OverBudget);
        Assert.Equal(a.FrameCount,a.Diagnostics!.Frames.Total);Assert.True(a.DeliveredValue<=a.SelectedValue);Assert.True(a.ExactRestore);
    }
}

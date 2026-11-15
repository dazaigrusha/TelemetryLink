using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Metrics;
using TelemetryLink.Core.Optimization;
namespace TelemetryLink.Tests;
public class ReliabilityFitnessTests
{
    static ReliabilityCell Cell(double q=.8,double loss=0,double limit=.2,bool exact=true,bool crash=false)=>new("emergency",1,1,q,loss,limit,exact,crash,100,100,1000,1);
    [Fact] public void FeasibilityBeatsHighQualityWithAnyViolation()
    {
        var feasible=ReliabilityFitness.Assess([Cell(q:0)]);
        var highQ=ReliabilityFitness.Assess([Cell(q:1,loss:.21)]);
        Assert.True(feasible.Feasible);Assert.False(highQ.Feasible);Assert.True(feasible.Score>highQ.Score);
        Assert.True(ReliabilityFitness.Assess([Cell(q:.9)]).Score>ReliabilityFitness.Assess([Cell(q:.2)]).Score);
    }
    [Fact] public void EveryCellMustPassNotJustAverage()
    {
        var a=ReliabilityFitness.Assess([Cell(loss:0),Cell(loss:.3)]);
        Assert.False(a.Feasible);Assert.Equal(1,a.ThresholdFailures);Assert.Equal(.05,a.MeanExcess,10);
    }
    [Fact] public void FewerFailedCellsAlwaysOutrankSeverityForSameGrid()
    {
        var oneSevere=ReliabilityFitness.Assess([Cell(loss:1,limit:0),Cell(),Cell()]);
        var twoSlight=ReliabilityFitness.Assess([Cell(loss:.20001),Cell(loss:.20001),Cell()]);
        Assert.True(oneSevere.Score>twoSlight.Score);
        Assert.True(ReliabilityFitness.Assess([Cell(loss:.25)]).Score>ReliabilityFitness.Assess([Cell(loss:.8)]).Score);
    }
    [Theory] [InlineData(false,false)] [InlineData(true,true)]
    public void IntegrityFailureCannotWinEvenWithHighQ(bool exact,bool crash)
    {
        var broken=ReliabilityFitness.Assess([Cell(q:1,exact:exact,crash:crash)]);
        var healthyFailed=ReliabilityFitness.Assess([Cell(q:0,loss:1)]);
        Assert.Equal(1,broken.IntegrityFailures);Assert.False(broken.Feasible);Assert.True(broken.Score<healthyFailed.Score);
    }
    [Fact] public void ZeroThresholdAndToleranceDoNotDivideByZero()
    {
        Assert.True(ReliabilityFitness.Assess([Cell(loss:0,limit:0)]).Feasible);
        Assert.True(ReliabilityFitness.Assess([Cell(loss:.2000000001)]).Feasible);
        Assert.False(ReliabilityFitness.Assess([Cell(loss:.20001)]).Feasible);
    }
    [Fact] public void InvalidOrEmptyMetricsRejected()
    {
        Assert.Throws<ArgumentException>(()=>ReliabilityFitness.Assess([]));
        foreach(var c in new[]{Cell(q:double.NaN),Cell(loss:double.PositiveInfinity),Cell(limit:-1),Cell(q:1.1)})
            Assert.Throws<ArgumentException>(()=>ReliabilityFitness.Assess([c]));
    }
    public static SessionReport Report(string scenario,double q=.8,int critical=90)=>new()
    {
        ScenarioName=scenario,CodecName="none",PackerName="dp",SchedulerName="greedy",ClassifierName="majority",Tuning=Tuning.Default,
        FrameCount=100,CriticalFrameCount=100,RecoveredFrameCount=critical,RecoveredCriticalFrameCount=critical,
        ReferenceBits=9600,SentBits=1000,BuiltBits=1000,PackedFrameCount=100,ElapsedSeconds=.1,PacketsBuilt=1,PacketsAccepted=1,
        SelectedValue=100,DeliveredValue=critical,ExactRestore=true,Channel=new(1,0,0,0,1),Rejections=new Dictionary<TelemetryLink.Core.Format.RejectionCode,int>(),S=.5,R=critical/100.0,T=.9,Q=q
    };
    [Fact] public void FullGridRunsEvenAfterThresholdFailure()
    {
        var scenarios=new[]{DataKit.Config.Scenario("calm"),DataKit.Config.Scenario("working")};var calls=new List<(string,int)>();
        var result=ReliabilityFitness.Evaluate(Tuning.Default,scenarios,[7,8,9],2,_=>0,(t,s,seed)=>{calls.Add((s.Name,seed));return Report(s.Name);});
        Assert.Equal(12,calls.Count);Assert.Equal(12,result.Cells.Count);Assert.Equal(12,result.Assessment.ThresholdFailures);
        Assert.All(result.Cells,c=>Assert.InRange(c.Repeat,1,2));Assert.Equal(2,calls.Count(c=>c==("calm",7)));
    }
}

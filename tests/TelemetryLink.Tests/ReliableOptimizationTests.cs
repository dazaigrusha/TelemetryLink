using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Optimization;
namespace TelemetryLink.Tests;
public class ReliableOptimizationTests
{
    sealed class FixedOptimizer(string name):IParameterOptimizer
    {
        public string Name=>name;
        public OptimizationResult Optimize(Func<Tuning,double> fitness,int budget,int seed)
        {
            var a=new Tuning(16,.5,1);var b=new Tuning(32,.5,1);double fa=fitness(a),fb=fitness(b);
            return new(fa>=fb?a:b,Math.Max(fa,fb),[],2);
        }
    }
    static ReliabilityEvaluation Eval(Tuning t,double q,double loss=0)
    {
        ReliabilityCell[] cells=[new("emergency",1,1,q,loss,.2,true,false,100,100,1000,1)];
        return new(t,ReliabilityFitness.Assess(cells),cells);
    }
    [Fact] public void AllSelectionsAreFrozenBeforeValidationAndNeverReplacedByHoldout()
    {
        var events=new List<string>();
        var result=ReliableOptimizationExperiment.Run([new FixedOptimizer("one"),new FixedOptimizer("two")],2,1,Tuning.Default,
            t=>{events.Add("train");return Eval(t,t.WindowFrames==16?.9:.1);},
            t=>{events.Add("validation");return Eval(t,t.WindowFrames==16?0:1,t.WindowFrames==16?1:0);});
        Assert.Equal(new[]{"train","train","train","train","train","validation","validation","validation"},events);
        Assert.Equal(4,result.Trials.Count);Assert.All(result.Results,r=>{Assert.Equal(16,r.Search.Best.WindowFrames);Assert.True(r.Training.Assessment.Feasible);Assert.False(r.Validation.Assessment.Feasible);});
        Assert.True(result.BaselineValidation.Assessment.Feasible);
    }
    [Fact] public void BothAlgorithmsUseExactlyBudgetAndNeverCallValidationForLosers()
    {
        int training=0,validation=0;
        var result=ReliableOptimizationExperiment.Run([new RandomSearchOptimizer(),new GeneticOptimizer(new(PopulationSize:4))],9,3,Tuning.Default,
            t=>{training++;return Eval(t,t.WindowFrames/64.0);},t=>{validation++;return Eval(t,.5);});
        Assert.Equal(19,training);Assert.Equal(3,validation);Assert.Equal(18,result.Trials.Count);
        Assert.All(result.Results,r=>Assert.Equal(9,r.Search.Evaluations));
        Assert.All(result.Results,r=>Assert.Contains(result.Trials,t=>t.Optimizer==r.Optimizer&&t.Result.Tuning==r.Search.Best&&t.Result.Assessment.Score==r.Search.BestFitness));
    }
}

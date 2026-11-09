using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Optimization;
namespace TelemetryLink.Tests;
public class OptimizerTests
{
    static double Fitness(Tuning t) => -Math.Pow(t.WindowFrames - 35, 2) - Math.Pow(t.CompressionThreshold - .7, 2) - Math.Pow(t.CriticalWeight - 2, 2);
    static IParameterOptimizer Optimizer(string name) => name == "ga" ? new GeneticOptimizer() : new RandomSearchOptimizer();
    [Theory]
    [InlineData("ga", 1)] [InlineData("ga", 5)] [InlineData("ga", 12)] [InlineData("ga", 13)] [InlineData("ga", 37)] [InlineData("ga", 200)]
    [InlineData("random", 1)] [InlineData("random", 5)] [InlineData("random", 200)]
    public void ExactBudgetBestAndMonotonicHistory(string name, int budget)
    {
        var seen = new List<(Tuning T, double Score)>();
        var result = Optimizer(name).Optimize(t => { double s = Fitness(t); seen.Add((t,s)); return s; }, budget, 7);
        Assert.Equal(budget, seen.Count); Assert.Equal(budget, result.Evaluations);
        Assert.Equal(seen.Max(x => x.Score), result.BestFitness); Assert.Equal(Fitness(result.Best), result.BestFitness);
        Assert.Contains(seen, x => x.T == result.Best);
        Assert.Equal(budget, result.History[^1].Evaluations);
        for (int i = 1; i < result.History.Count; i++)
        { Assert.True(result.History[i].BestFitness >= result.History[i-1].BestFitness); Assert.True(result.History[i].Evaluations > result.History[i-1].Evaluations); }
    }
    [Theory] [InlineData("ga")] [InlineData("random")]
    public void FixedSeedAndDeterministicFitnessReproduceEveryCandidate(string name)
    {
        var a = new List<Tuning>(); var b = new List<Tuning>();
        var first = Optimizer(name).Optimize(t => { a.Add(t); return Fitness(t); }, 57, 3);
        var second = Optimizer(name).Optimize(t => { b.Add(t); return Fitness(t); }, 57, 3);
        Assert.Equal(a,b); Assert.Equal(first.Best,second.Best); Assert.Equal(first.History,second.History);
    }
    [Fact] public void TournamentPrefersHigherScore()
    {
        var optimizer = new GeneticOptimizer(); var random = new Random(3); int best = 0;
        for (int i = 0; i < 10000; i++) if (optimizer.SelectByTournament([0,1,2,3], random) == 3) best++;
        Assert.InRange(best, 5400, 6200);
    }
    [Fact] public void CrossoverGenesComeFromParents()
    {
        var a = new Tuning(8,.3,0); var b = new Tuning(64,1.6,3); var random = new Random(4);
        for (int n = 0; n < 500; n++)
        { var genes = GeneticOptimizer.Crossover(a,b,random).ToGenes(); for (int i = 0; i < 3; i++) Assert.True(genes[i] == a.ToGenes()[i] || genes[i] == b.ToGenes()[i]); }
    }
    [Fact] public void ZeroMutationPreservesChromosome()
    { var t = new Tuning(31,.71,2.12); Assert.Equal(t, new GeneticOptimizer(new(MutationProbability:0)).Mutate(t,new Random(1))); }
    [Fact] public void RandomAndMutatedGenesStayWithinBounds()
    {
        var random = new Random(2); var optimizer = new GeneticOptimizer(new(MutationProbability:1,MutationScale:10));
        bool changed = false;
        for (int n = 0; n < 2000; n++)
        {
            var original = RandomSearchOptimizer.RandomTuning(random); var mutated = optimizer.Mutate(original,random); changed |= original != mutated;
            foreach (var t in new[] { original,mutated }) for (int i = 0; i < 3; i++) Assert.InRange(t.ToGenes()[i], Tuning.Bounds[i].Low,Tuning.Bounds[i].High);
        }
        Assert.True(changed);
    }
    [Fact] public void PopulationOneUsesExactBudgetAndRetainsBest()
    {
        int calls=0;var result=new GeneticOptimizer(new(PopulationSize:1)).Optimize(t=>{ calls++;return Fitness(t);},17,1);
        Assert.Equal(17,calls);Assert.Equal(17,result.History.Count); Assert.True(result.History[^1].BestFitness>=result.History[0].BestFitness);
    }
    [Fact] public void ElitesAreNotReevaluated()
    {
        int calls=0;var result=new GeneticOptimizer(new(PopulationSize:4,EliteCount:2)).Optimize(t=>++calls,9,1);
        Assert.Equal(new[]{4,6,8,9},result.History.Select(h=>h.Evaluations).ToArray());
    }
    [Theory] [InlineData("ga")] [InlineData("random")]
    public void InvalidBudgetOrFitnessRejected(string name)
    {
        var optimizer=Optimizer(name);Assert.Throws<ArgumentOutOfRangeException>(()=>optimizer.Optimize(Fitness,0,1));
        Assert.Throws<InvalidOperationException>(()=>optimizer.Optimize(_=>double.NaN,1,1));
        Assert.Throws<InvalidOperationException>(()=>optimizer.Optimize(_=>double.PositiveInfinity,1,1));
    }
    [Fact] public void InvalidGeneticOptionsRejected()
    {
        foreach(var options in new[]{new GeneticOptions(PopulationSize:0),new GeneticOptions(EliteCount:0),new GeneticOptions(EliteCount:12),new GeneticOptions(MutationProbability:1.1),new GeneticOptions(MutationScale:double.NaN),new GeneticOptions(TournamentSize:0)})
            Assert.Throws<ArgumentException>(()=>new GeneticOptimizer(options));
        Assert.Throws<ArgumentException>(()=>Tuning.FromGenes([1,2]));Assert.Throws<ArgumentException>(()=>Tuning.FromGenes([1,double.NaN,2]));
    }
}

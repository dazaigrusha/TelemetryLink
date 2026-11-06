using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Packing;

namespace TelemetryLink.Tests;

public class PackerTests
{
    public static IEnumerable<object[]> AlgorithmNames => new[] { "dp", "greedy", "brute", "sequential" }.Select(n => new object[] { n });
    private static IFramePacker Packer(string name) => TelemetryLink.Core.AlgorithmCatalog.Packer(name);
    private static long Value(IReadOnlyList<FrameCandidate> candidates, IReadOnlyList<int> indexes)
        => indexes.Sum(i => (long)candidates[i].Value);

    [Fact]
    public void DynamicProgramming_MatchesBruteForce_On200SeededInstances()
    {
        var random = new Random(2026);
        for (int sample = 0; sample < 200; sample++)
        {
            var candidates = Enumerable.Range(0, random.Next(0, 14))
                .Select(i => new FrameCandidate(100 + i, random.Next(0, 40), random.Next(0, 50))).ToArray();
            int capacity = random.Next(1, 160);
            var dp = new DynamicProgrammingPacker().Select(candidates, capacity);
            var brute = new BruteForcePacker().Select(candidates, capacity);
            Assert.Equal(Value(candidates, brute), Value(candidates, dp));
            Assert.Equal(dp.Distinct().OrderBy(i => i), dp);
            Assert.True(dp.Sum(i => (long)candidates[i].WeightBits) <= capacity);
        }
    }

    [Fact]
    public void DynamicProgramming_NeverWorseThanGreedy_On500Instances()
    {
        var random = new Random(53);
        for (int sample = 0; sample < 500; sample++)
        {
            var candidates = Enumerable.Range(0, random.Next(1, 65))
                .Select(i => new FrameCandidate(i, random.Next(1, 140), random.Next(1, 12))).ToArray();
            int capacity = random.Next(0, 1400);
            Assert.True(Value(candidates, Packer("dp").Select(candidates, capacity))
                >= Value(candidates, Packer("greedy").Select(candidates, capacity)));
        }
    }

    [Fact]
    public void Greedy_IsSuboptimal_OnKnownCounterexample()
    {
        FrameCandidate[] candidates = [new(0, 10, 60), new(1, 20, 100), new(2, 30, 120)];
        Assert.Equal(new[] { 0, 1 }, Packer("greedy").Select(candidates, 50));
        Assert.Equal(160L, Value(candidates, Packer("greedy").Select(candidates, 50)));
        Assert.Equal(new[] { 1, 2 }, Packer("dp").Select(candidates, 50));
        Assert.Equal(220L, Value(candidates, Packer("brute").Select(candidates, 50)));
    }

    [Theory]
    [MemberData(nameof(AlgorithmNames))]
    public void EveryPacker_RespectsCapacityAndReturnsUniqueAscendingListIndexes(string name)
    {
        FrameCandidate[] candidates = [new(200, 12, 8), new(300, 9, 6), new(999, 7, 9), new(-5, 30, 100)];
        var result = Packer(name).Select(candidates, 22);
        Assert.True(result.Sum(i => (long)candidates[i].WeightBits) <= 22);
        Assert.Equal(result.Distinct().OrderBy(i => i), result);
        Assert.All(result, i => Assert.InRange(i, 0, candidates.Length - 1));
    }

    [Theory]
    [MemberData(nameof(AlgorithmNames))]
    public void EmptyCandidates_ProduceEmptySelection(string name)
        => Assert.Empty(Packer(name).Select([], 100));

    [Theory]
    [MemberData(nameof(AlgorithmNames))]
    public void NonPositiveCapacity_ProducesEmptySelection(string name)
    {
        Assert.Empty(Packer(name).Select([new(0, 10, 5)], 0));
        Assert.Empty(Packer(name).Select([new(0, 10, 5)], -1));
    }

    [Theory]
    [MemberData(nameof(AlgorithmNames))]
    public void OversizedSingleItem_IsNotSelected(string name)
        => Assert.Empty(Packer(name).Select([new(0, 1000, 1)], 10));

    [Theory]
    [MemberData(nameof(AlgorithmNames))]
    public void AllItemsFit_SelectsEveryPositiveValueItem(string name)
        => Assert.Equal(new[] { 0, 1, 2 }, Packer(name).Select([new(0, 2, 5), new(1, 3, 4), new(2, 4, 3)], 9));

    [Fact]
    public void Greedy_SkipsOversizedItemWhereSequentialStops()
    {
        FrameCandidate[] candidates = [new(0, 100, 1000), new(1, 2, 1)];
        Assert.Empty(Packer("sequential").Select(candidates, 3));
        Assert.Equal(new[] { 1 }, Packer("greedy").Select(candidates, 3));
    }

    [Fact]
    public void DynamicProgramming_DoesNotReuseItemWhenCapacityAllowsTwoCopies()
    {
        FrameCandidate[] candidates = [new(0, 2, 10), new(1, 3, 1)];
        Assert.Equal(new[] { 0 }, Packer("dp").Select(candidates, 4));
    }

    [Theory]
    [InlineData("dp")]
    [InlineData("greedy")]
    [InlineData("brute")]
    public void ZeroWeightPositiveValue_IsTakenOnce(string name)
    {
        FrameCandidate[] candidates = [new(0, 0, 7), new(1, 3, 4), new(2, 4, 8)];
        var chosen = Packer(name).Select(candidates, 4);
        Assert.Contains(0, chosen);
        Assert.Equal(1, chosen.Count(i => i == 0));
        Assert.Equal(15L, Value(candidates, chosen));
    }

    [Fact]
    public void Greedy_UsesExactRatioComparison_ForNearlyEqualLargeValues()
    {
        FrameCandidate[] candidates = [new(0, int.MaxValue - 1, int.MaxValue - 2), new(1, int.MaxValue, int.MaxValue - 1)];
        Assert.Equal(new[] { 1 }, Packer("greedy").Select(candidates, int.MaxValue));
    }

    [Theory]
    [InlineData("dp")]
    [InlineData("greedy")]
    [InlineData("brute")]
    public void LargeValuesAndCapacity_DoNotOverflowScores(string name)
    {
        FrameCandidate[] candidates = [new(0, 2, int.MaxValue), new(1, 3, int.MaxValue), new(2, 4, 1)];
        Assert.Equal(2L * int.MaxValue, Value(candidates, Packer(name).Select(candidates, 5)));
    }

    [Fact]
    public void DynamicProgramming_LargeCapacityWithSmallInput_DoesNotAllocateByCapacity()
        => Assert.Equal(new[] { 0, 1 }, Packer("dp").Select([new(0, 2, 1), new(1, 3, 1)], int.MaxValue));

    [Fact]
    public void DynamicProgramming_LargeWeights_UsesCommonDivisor()
        => Assert.Equal(new[] { 1 }, Packer("dp").Select([new(0, int.MaxValue, 3), new(1, int.MaxValue, 5)], int.MaxValue));

    [Theory]
    [InlineData("dp")]
    [InlineData("greedy")]
    [InlineData("brute")]
    public void InvalidNegativeWeightsAndValues_AreRejected(string name)
    {
        Assert.Throws<ArgumentException>(() => Packer(name).Select([new(0, -1, 2)], 10));
        Assert.Throws<ArgumentException>(() => Packer(name).Select([new(0, 1, -2)], 10));
    }

    [Fact]
    public void BruteForce_MoreThan20Candidates_IsRejected()
        => Assert.Throws<ArgumentException>(() => Packer("brute").Select(Enumerable.Repeat(new FrameCandidate(0, 1, 1), 21).ToArray(), 10));

    [Theory]
    [InlineData("dp")]
    [InlineData("greedy")]
    [InlineData("brute")]
    public void EqualInputs_ProduceDeterministicSelectionsWithoutChangingInput(string name)
    {
        FrameCandidate[] candidates = [new(0, 10, 10), new(1, 10, 10), new(2, 10, 10)];
        var snapshot = candidates.ToArray();
        var expected = Packer(name).Select(candidates, 20).ToArray();
        for (int i = 0; i < 5; i++) Assert.Equal(expected, Packer(name).Select(candidates, 20));
        Assert.Equal(snapshot, candidates);
    }
}

using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Packing;

/// <summary>Точний рюкзак 0/1: O(n*W) часу та O(n*W) пам'яті для відновлення.</summary>
public sealed class DynamicProgrammingPacker : IFramePacker
{
    public string Name => "dp";

    public IReadOnlyList<int> Select(IReadOnlyList<FrameCandidate> candidates, int capacityBits)
    {
        PackerInput.Validate(candidates);
        if (capacityBits <= 0 || candidates.Count == 0) return [];
        int[] eligible = Enumerable.Range(0, candidates.Count)
            .Where(i => candidates[i].Value > 0 && candidates[i].WeightBits <= capacityBits).ToArray();
        if (eligible.Length == 0) return [];
        long totalWeight = eligible.Sum(i => (long)candidates[i].WeightBits);
        if (totalWeight <= capacityBits) return eligible;

        // Спільний дільник ваг зменшує W без зміни множини допустимих наборів.
        int divisor = 0;
        foreach (int index in eligible) divisor = Gcd(divisor, candidates[index].WeightBits);
        divisor = Math.Max(divisor, 1);
        int capacity = capacityBits / divisor;
        var best = new long[checked(capacity + 1)];
        var taken = new bool[eligible.Length][];
        for (int row = 0; row < eligible.Length; row++)
        {
            var item = candidates[eligible[row]];
            int weight = item.WeightBits / divisor;
            taken[row] = new bool[checked(capacity + 1)];
            // Зворотний порядок не дозволяє використати предмет двічі.
            for (int w = capacity; w >= weight; w--)
            {
                long alternative = best[w - weight] + item.Value;
                if (alternative > best[w])
                {
                    best[w] = alternative;
                    taken[row][w] = true;
                }
            }
        }
        var selected = new List<int>();
        int remaining = capacity;
        for (int row = eligible.Length - 1; row >= 0; row--)
            if (taken[row][remaining])
            {
                int index = eligible[row];
                selected.Add(index);
                remaining -= candidates[index].WeightBits / divisor;
            }
        selected.Reverse();
        return selected;
    }

    private static int Gcd(int left, int right)
    {
        while (right != 0) (left, right) = (right, left % right);
        return left;
    }
}

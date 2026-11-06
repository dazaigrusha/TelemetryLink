using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Packing;

/// <summary>Точний еталон: повний перебір до 20 кандидатів. Складність O(n*2^n).</summary>
public sealed class BruteForcePacker : IFramePacker
{
    public const int MaxItems = 20;
    public string Name => "brute";

    public IReadOnlyList<int> Select(IReadOnlyList<FrameCandidate> candidates, int capacityBits)
    {
        PackerInput.Validate(candidates);
        if (candidates.Count > MaxItems)
            throw new ArgumentException($"Повний перебір підтримує до {MaxItems} кандидатів. Для CLI задайте --window 16.", nameof(candidates));
        if (capacityBits <= 0 || candidates.Count == 0) return [];
        long bestValue = 0;
        uint bestMask = 0;
        uint limit = 1u << candidates.Count;
        // Кожна маска однозначно задає підмножину; кадр береться максимум раз.
        for (uint mask = 1; mask < limit; mask++)
        {
            long weight = 0, value = 0;
            for (int i = 0; i < candidates.Count; i++)
                if ((mask & (1u << i)) != 0)
                {
                    weight += candidates[i].WeightBits;
                    value += candidates[i].Value;
                }
            if (weight <= capacityBits && value > bestValue)
            {
                bestValue = value;
                bestMask = mask;
            }
        }
        return Enumerable.Range(0, candidates.Count).Where(i => (bestMask & (1u << i)) != 0).ToArray();
    }
}

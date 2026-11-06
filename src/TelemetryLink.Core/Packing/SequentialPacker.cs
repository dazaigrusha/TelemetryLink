using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Packing;

/// <summary>Базовий вибір: кадри підряд до першого, який не вміщається.</summary>
public sealed class SequentialPacker : IFramePacker
{
    public string Name => "sequential";

    public IReadOnlyList<int> Select(IReadOnlyList<FrameCandidate> candidates, int capacityBits)
    {
        var result = new List<int>();
        if (capacityBits <= 0) return result;
        int remaining = capacityBits;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].WeightBits < 0) throw new ArgumentException("Вага кадру не може бути від'ємною.");
            if (candidates[i].WeightBits > remaining) break;
            result.Add(i);
            remaining -= candidates[i].WeightBits;
        }
        return result;
    }
}

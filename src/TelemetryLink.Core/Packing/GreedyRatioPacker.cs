using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Packing;

/// <summary>Евристика: цінність/вага за спаданням. Оптимальність не гарантується.</summary>
public sealed class GreedyRatioPacker : IFramePacker
{
    public string Name => "greedy";

    public IReadOnlyList<int> Select(IReadOnlyList<FrameCandidate> candidates, int capacityBits)
    {
        PackerInput.Validate(candidates);
        if (capacityBits <= 0 || candidates.Count == 0) return [];
        int[] order = Enumerable.Range(0, candidates.Count).Where(i => candidates[i].Value > 0).ToArray();
        Array.Sort(order, (left, right) =>
        {
            // Перехресне множення порівнює відношення точно, без double.
            // Додатна цінність з нульовою вагою має найвищу щільність.
            long leftProduct = (long)candidates[left].Value * candidates[right].WeightBits;
            long rightProduct = (long)candidates[right].Value * candidates[left].WeightBits;
            int comparison = rightProduct.CompareTo(leftProduct);
            return comparison != 0 ? comparison : left.CompareTo(right);
        });
        int remaining = capacityBits;
        var selected = new List<int>();
        foreach (int index in order)
        {
            int weight = candidates[index].WeightBits;
            if (weight > remaining) continue;
            selected.Add(index);
            remaining -= weight;
        }
        selected.Sort();
        return selected;
    }
}

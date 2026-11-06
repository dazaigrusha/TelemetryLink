using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Packing;

/// <summary>Спільна перевірка числового контракту задачі рюкзака.</summary>
internal static class PackerInput
{
    public static void Validate(IReadOnlyList<FrameCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Any(c => c.WeightBits < 0 || c.Value < 0))
            throw new ArgumentException("Вага та цінність мають бути невід'ємними.", nameof(candidates));
    }
}

namespace TelemetryLink.Core.Pipeline;

/// <summary>Політика пакетування записаного сеансу; формат пакета не змінює.</summary>
public enum DeliveryPolicy { Balanced, CriticalFirst }

public static class DeliveryPolicies
{
    // Limit the critical payload exposed to one lost packet; ordinary windows stay unchanged.
    public const int MaxCriticalFramesPerPacket = 8;
    public static DeliveryPolicy Parse(string name) => name.ToLowerInvariant() switch
    {
        "balanced" => DeliveryPolicy.Balanced,
        "critical-first" => DeliveryPolicy.CriticalFirst,
        _ => throw new ArgumentException("--delivery: balanced або critical-first.")
    };
    public static string Name(DeliveryPolicy policy) => policy switch
    {
        DeliveryPolicy.Balanced => "balanced",
        DeliveryPolicy.CriticalFirst => "critical-first",
        _ => throw new ArgumentOutOfRangeException(nameof(policy))
    };
}

using TelemetryLink.Core.Domain;
namespace TelemetryLink.Core.Pipeline;

/// <summary>Відкритий план без ACK та без доступу до seed/результатів каналу.</summary>
public static class TransmissionPlan
{
    public static IReadOnlyList<Packet> Create(IReadOnlyList<Packet> scheduled,
        IReadOnlyList<TelemetryFrame> frames, PipelineOptions options)
    {
        _ = DeliveryPolicies.Name(options.Delivery);
        if (options.CriticalCopies is < 1 or > 3 || options.RepeatGapPackets is < 0 or > 64)
            throw new ArgumentException("Copies має бути 1..3, repeat-gap — 0..64.");
        if (options.Delivery == DeliveryPolicy.Balanced) return scheduled;
        var critical = scheduled.Where(p => p.FrameIndexes.Any(i => frames[i].IsCritical)).ToArray();
        var normal = scheduled.Where(p => !p.FrameIndexes.Any(i => frames[i].IsCritical)).ToArray();
        int copies = options.Delivery == DeliveryPolicy.CriticalRepeat ? options.CriticalCopies : 1;
        var plan = new List<Packet>(); int nextNormal = 0;
        if (critical.Length == 0) return normal;
        for (int round = 0; round < copies; round++)
        {
            // Reuse exact packet objects/bytes: no re-encryption, new nonce, or modified header.
            plan.AddRange(critical);
            if (round + 1 < copies)
            {
                int take = Math.Min(options.RepeatGapPackets, normal.Length - nextNormal);
                plan.AddRange(normal.Skip(nextNormal).Take(take)); nextNormal += take;
            }
        }
        plan.AddRange(normal.Skip(nextNormal));
        return plan.AsReadOnly();
    }
}

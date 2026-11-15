namespace TelemetryLink.Core.Metrics;

public enum FrameDeliveryOutcome { NotPacked, Recovered, OverBudget, ChannelLost, ReceiverRejected, ReceiverFailed, ContentMismatch }

/// <summary>Взаємно виключні кінцеві стани унікальних кадрів.</summary>
public sealed record DeliveryBreakdown(int Recovered, int OverBudget, int ChannelLost, int ReceiverRejected,
    int ReceiverFailed, int ContentMismatch, int NotPacked)
{
    public int Total => Recovered + OverBudget + ChannelLost + ReceiverRejected + ReceiverFailed + ContentMismatch + NotPacked;
    public static DeliveryBreakdown Count(IEnumerable<FrameDeliveryOutcome> outcomes)
    {
        var counts = outcomes.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        return new(counts.GetValueOrDefault(FrameDeliveryOutcome.Recovered), counts.GetValueOrDefault(FrameDeliveryOutcome.OverBudget),
            counts.GetValueOrDefault(FrameDeliveryOutcome.ChannelLost), counts.GetValueOrDefault(FrameDeliveryOutcome.ReceiverRejected),
            counts.GetValueOrDefault(FrameDeliveryOutcome.ReceiverFailed), counts.GetValueOrDefault(FrameDeliveryOutcome.ContentMismatch),
            counts.GetValueOrDefault(FrameDeliveryOutcome.NotPacked));
    }
}
public sealed record DeliveryDiagnostics(DeliveryBreakdown Frames, DeliveryBreakdown CriticalFrames);

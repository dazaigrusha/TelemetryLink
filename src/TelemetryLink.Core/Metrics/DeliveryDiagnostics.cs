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


/// <summary>Одна успішна копія відновлює кадр; інакше зберігаємо найінформативнішу причину.</summary>
public static class FrameDeliveryOutcomes
{
    public static FrameDeliveryOutcome Merge(FrameDeliveryOutcome previous, FrameDeliveryOutcome current)
        => Rank(current) > Rank(previous) ? current : previous;
    private static int Rank(FrameDeliveryOutcome outcome) => outcome switch
    {
        FrameDeliveryOutcome.NotPacked => 0, FrameDeliveryOutcome.OverBudget => 1,
        FrameDeliveryOutcome.ChannelLost => 2, FrameDeliveryOutcome.ReceiverRejected => 3,
        FrameDeliveryOutcome.ReceiverFailed => 4, FrameDeliveryOutcome.ContentMismatch => 5,
        FrameDeliveryOutcome.Recovered => 6, _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };
}

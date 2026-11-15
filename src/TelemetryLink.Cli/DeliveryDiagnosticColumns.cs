using TelemetryLink.Core.Metrics;
using TelemetryLink.Core.Pipeline;
namespace TelemetryLink.Cli;

/// <summary>Спільний додаток до CSV run та compare; нуль не підміняє відсутні виміри.</summary>
public static class DeliveryDiagnosticColumns
{
    public const string Header = "delivery,critical_over_budget,critical_channel_lost,critical_receiver_rejected,critical_receiver_failed,critical_content_mismatch,critical_not_packed,critical_copies,repeat_gap_packets,transmission_attempts,planned_repeat_attempts,sent_repeat_attempts,repeated_sent_bits,duplicate_accepted_packets,unique_packets_accepted,recovered_by_repeat_frames,critical_recovered_by_repeat";
    public static object?[] Values(SessionReport report)
    {
        var c = report.Diagnostics?.CriticalFrames;
        return [DeliveryPolicies.Name(report.Delivery), c?.OverBudget, c?.ChannelLost, c?.ReceiverRejected,
            c?.ReceiverFailed, c?.ContentMismatch, c?.NotPacked,
            report.CriticalCopies, report.RepeatGapPackets, report.TransmissionAttempts, report.PlannedRepeatAttempts,
            report.SentRepeatAttempts, report.RepeatedSentBits, report.DuplicateAcceptedPackets, report.UniquePacketsAccepted,
            report.RecoveredByRepeatFrames, report.CriticalRecoveredByRepeat];
    }
}

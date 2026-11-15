using TelemetryLink.Core.Metrics;
using TelemetryLink.Core.Pipeline;
namespace TelemetryLink.Cli;

/// <summary>Спільний додаток до CSV run та compare; нуль не підміняє відсутні виміри.</summary>
public static class DeliveryDiagnosticColumns
{
    public const string Header = "delivery,critical_over_budget,critical_channel_lost,critical_receiver_rejected,critical_receiver_failed,critical_content_mismatch,critical_not_packed";
    public static object?[] Values(SessionReport report)
    {
        var c = report.Diagnostics?.CriticalFrames;
        return [DeliveryPolicies.Name(report.Delivery), c?.OverBudget, c?.ChannelLost, c?.ReceiverRejected,
            c?.ReceiverFailed, c?.ContentMismatch, c?.NotPacked];
    }
}

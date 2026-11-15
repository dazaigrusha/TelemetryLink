using System.Diagnostics;
using System.Security.Cryptography;
using TelemetryLink.Core.Channel;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.InputOutput;
using TelemetryLink.Core.Metrics;
using TelemetryLink.Core.Security;

namespace TelemetryLink.Core.Pipeline;

/// <summary>Повний прогін: кадри → пакети → канал → приймач → S, R, T, Q.</summary>
public sealed class SessionRunner
{
    private readonly TeamConfig _config;
    private readonly PipelineOptions _options;

    public SessionRunner(TeamConfig config, PipelineOptions options)
    {
        _config = config;
        _options = options;
    }

    /// <param name="channel">Необов'язковий канал для тестування; типово незмінена модель викладача.</param>
    public SessionReport Run(TelemetrySession session, ScenarioConfig scenario, int channelSeed, IChannel? channel = null, PacketDecoder? decoder = null)
    {
        var stopwatch = Stopwatch.StartNew();
        byte[] salt = SessionKeys.GenerateSalt();
        // PBKDF2 — один раз, ключі спільні для всіх пакетів цього сеансу.
        var keys = SessionKeys.Derive(_config.SessionPassword, salt);
        var transmitter = new Transmitter(_config, _options, keys, salt, RandomNumberGenerator.GetBytes(8));
        var packets = transmitter.BuildPackets(session.Frames);
        var scheduled = _options.Scheduler.Order(packets);
        if (scheduled.Count != packets.Count || !scheduled.Select(p => p.Sequence).OrderBy(i => i)
            .SequenceEqual(packets.Select(p => p.Sequence).OrderBy(i => i)))
            throw new InvalidOperationException("Планувальник має повернути перестановку сформованих пакетів.");
        scheduled = TransmissionPlan.Create(scheduled, session.Frames, _options);
        var outcomes = new FrameDeliveryOutcome[session.Frames.Count];
        void Mark(Packet packet, FrameDeliveryOutcome outcome)
        {
            foreach (int index in packet.FrameIndexes)
                outcomes[index] = FrameDeliveryOutcomes.Merge(outcomes[index], outcome);
        }
        channel ??= new MarkovChannel(scenario, channelSeed);
        var receiver = new Receiver(keys, decoder);
        var recovered = new HashSet<int>();
        bool exact = true;
        bool receiverCrashed = false;
        long sentBits = 0;
        int deliveredValue = 0, plannedRepeats = 0, sentRepeats = 0, duplicateAccepted = 0;
        int recoveredByRepeat = 0, criticalRecoveredByRepeat = 0;
        long repeatedSentBits = 0;
        var credited = new HashSet<uint>();
        var attempts = new Dictionary<uint, int>();
        foreach (var packet in scheduled)
        {
            int attempt = attempts.GetValueOrDefault(packet.Sequence) + 1; attempts[packet.Sequence] = attempt;
            bool repeat = attempt > 1;
            if (repeat) plannedRepeats++;
            int sentBefore = channel.Statistics.Sent;
            var transmission = channel.Send(packet.Bytes);
            // Втрачені пакети витрачають смугу, over-budget — ні.
            if (channel.Statistics.Sent > sentBefore)
            {
                long bits = (long)packet.SizeInBytes * 8; sentBits += bits;
                if (repeat) { sentRepeats++; repeatedSentBits += bits; }
            }
            if (!transmission.Delivered)
            {
                Mark(packet, channel.Statistics.Sent > sentBefore ? FrameDeliveryOutcome.ChannelLost : FrameDeliveryOutcome.OverBudget);
                continue;
            }
            if (receiverCrashed) { Mark(packet, FrameDeliveryOutcome.ReceiverFailed); continue; }
            Format.ReceiveResult result;
            try { result = receiver.Accept(packet.Bytes); }
            catch (Exception exception) when (exception is not (OutOfMemoryException or OperationCanceledException))
            {
                // Порушення декодера — провал приймання, а не успішний прогін.
                // Вміст пакета та текст винятку у звіт не потрапляють.
                Mark(packet, FrameDeliveryOutcome.ReceiverFailed);
                receiverCrashed = true;
                exact = false;
                continue;
            }
            if (!result.IsAccepted)
            {
                Mark(packet, FrameDeliveryOutcome.ReceiverRejected);
                exact = false;
                continue;
            }
            byte[] expected = TelemetryFrame.Serialize(packet.FrameIndexes.Select(i => session.Frames[i]).ToArray());
            bool equal = expected.AsSpan().SequenceEqual(TelemetryFrame.Serialize(result.Frames));
            exact &= equal;
            if (!equal) { Mark(packet, FrameDeliveryOutcome.ContentMismatch); continue; }
            Mark(packet, FrameDeliveryOutcome.Recovered);
            foreach (int index in packet.FrameIndexes)
                if (recovered.Add(index) && repeat)
                {
                    recoveredByRepeat++;
                    if (session.Frames[index].IsCritical) criticalRecoveredByRepeat++;
                }
            if (credited.Add(packet.Sequence)) deliveredValue = checked(deliveredValue + packet.Value);
            else duplicateAccepted++;
        }
        stopwatch.Stop();
        int recoveredCritical = recovered.Count(i => session.Frames[i].IsCritical);
        double s = QualityMetric.TransmissionEfficiency(session.ReferenceBits, sentBits);
        double r = QualityMetric.CriticalRecovery(recoveredCritical, session.CriticalCount);
        double t = QualityMetric.Speed(_config.ReferenceTimeSeconds, stopwatch.Elapsed.TotalSeconds);
        return new SessionReport
        {
            ScenarioName = scenario.Name,
            ChannelSeed = channelSeed,
            Delivery = _options.Delivery,
            CriticalCopies = _options.Delivery == DeliveryPolicy.CriticalRepeat ? _options.CriticalCopies : 1,
            RepeatGapPackets = _options.Delivery == DeliveryPolicy.CriticalRepeat ? _options.RepeatGapPackets : 0,
            TransmissionAttempts = scheduled.Count, PlannedRepeatAttempts = plannedRepeats,
            SentRepeatAttempts = sentRepeats, RepeatedSentBits = repeatedSentBits,
            DuplicateAcceptedPackets = duplicateAccepted, UniquePacketsAccepted = credited.Count,
            RecoveredByRepeatFrames = recoveredByRepeat, CriticalRecoveredByRepeat = criticalRecoveredByRepeat,
            Diagnostics = new DeliveryDiagnostics(DeliveryBreakdown.Count(outcomes),
                DeliveryBreakdown.Count(outcomes.Where((_, i) => session.Frames[i].IsCritical))),
            CodecName = _options.Codec.Name,
            PackerName = _options.Packer.Name,
            SchedulerName = _options.Scheduler.Name,
            ClassifierName = _options.Classifier.Name,
            Tuning = _options.Tuning,
            FrameCount = session.Frames.Count,
            CriticalFrameCount = session.CriticalCount,
            RecoveredFrameCount = recovered.Count,
            RecoveredCriticalFrameCount = recoveredCritical,
            ReferenceBits = session.ReferenceBits,
            SentBits = sentBits,
            BuiltBits = packets.Sum(p => (long)p.SizeInBytes * 8),
            PackedFrameCount = packets.Sum(p => p.FrameIndexes.Count),
            ElapsedSeconds = stopwatch.Elapsed.TotalSeconds,
            PacketsBuilt = packets.Count,
            PacketsAccepted = receiver.AcceptedCount,
            SelectedValue = packets.Sum(p => p.Value),
            DeliveredValue = deliveredValue,
            ExactRestore = exact,
            ReceiverCrashed = receiverCrashed,
            Channel = channel.Statistics,
            Rejections = new Dictionary<Format.RejectionCode, int>(receiver.Rejections),
            S = s, R = r, T = t,
            Q = QualityMetric.Combine(_config, s, r, t, receiverHealthy: exact && !receiverCrashed)
        };
    }
}

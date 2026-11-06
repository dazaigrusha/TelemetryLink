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
    public SessionReport Run(TelemetrySession session, ScenarioConfig scenario, int channelSeed, IChannel? channel = null)
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
        channel ??= new MarkovChannel(scenario, channelSeed);
        var receiver = new Receiver(keys);
        var recovered = new HashSet<int>();
        bool exact = true;
        long sentBits = 0;
        int deliveredValue = 0;
        foreach (var packet in scheduled)
        {
            int sentBefore = channel.Statistics.Sent;
            var transmission = channel.Send(packet.Bytes);
            // Втрачені пакети витрачають смугу, over-budget — ні.
            if (channel.Statistics.Sent > sentBefore) sentBits += (long)packet.SizeInBytes * 8;
            if (!transmission.Delivered) continue;
            var result = receiver.Accept(packet.Bytes);
            if (!result.IsAccepted)
            {
                exact = false;
                continue;
            }
            byte[] expected = TelemetryFrame.Serialize(packet.FrameIndexes.Select(i => session.Frames[i]).ToArray());
            bool equal = expected.AsSpan().SequenceEqual(TelemetryFrame.Serialize(result.Frames));
            exact &= equal;
            if (!equal) continue;
            foreach (int index in packet.FrameIndexes) recovered.Add(index);
            deliveredValue = checked(deliveredValue + packet.Value);
        }
        stopwatch.Stop();
        int recoveredCritical = recovered.Count(i => session.Frames[i].IsCritical);
        double s = QualityMetric.TransmissionEfficiency(session.ReferenceBits, sentBits);
        double r = QualityMetric.CriticalRecovery(recoveredCritical, session.CriticalCount);
        double t = QualityMetric.Speed(_config.ReferenceTimeSeconds, stopwatch.Elapsed.TotalSeconds);
        return new SessionReport
        {
            ScenarioName = scenario.Name,
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
            Channel = channel.Statistics,
            Rejections = new Dictionary<Format.RejectionCode, int>(receiver.Rejections),
            S = s, R = r, T = t,
            Q = exact ? QualityMetric.Combine(_config, s, r, t) : 0.0
        };
    }
}

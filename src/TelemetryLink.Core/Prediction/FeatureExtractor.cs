using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.Prediction;

/// <summary>Причинні ознаки: поточний кадр і лише доступна історія.</summary>
public sealed class FeatureExtractor
{
    public const int FeatureCount = 10;
    public const string Schema = "telemetry-history-v1";
    public int WindowSize { get; }
    private static readonly string[] Names = ["Δaltitude", "Δtemp", "|vibration|", "vibration",
        "σ(vibration) по вікну", "σ(altitude) по вікну", "середнє |vibration| по вікну",
        "частота зміни mode", "дрейф altitude по вікну", "critical"];
    public static IReadOnlyList<string> FeatureNames { get; } = Array.AsReadOnly(Names);

    public FeatureExtractor(int windowSize = 8)
    {
        if (windowSize < 1) throw new ArgumentOutOfRangeException(nameof(windowSize));
        WindowSize = windowSize;
    }

    public double[][] Extract(IReadOnlyList<TelemetryFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Any(frame => !frame.IsValid)) throw new ArgumentException("Некоректна телеметрія.", nameof(frames));
        var result = new double[frames.Count][];
        for (int i = 0; i < frames.Count; i++)
        {
            int start = Math.Max(0, i - WindowSize + 1), count = i - start + 1;
            double meanVibration = 0, meanAltitude = 0, varianceVibration = 0, varianceAltitude = 0, sumAbs = 0;
            int changes = 0;
            for (int j = start; j <= i; j++)
            {
                int n = j - start + 1;
                double dv = frames[j].Vibration - meanVibration;
                meanVibration += dv / n;
                varianceVibration += dv * (frames[j].Vibration - meanVibration);
                double da = frames[j].AltitudeDm - meanAltitude;
                meanAltitude += da / n;
                varianceAltitude += da * (frames[j].AltitudeDm - meanAltitude);
                sumAbs += Math.Abs((double)frames[j].Vibration);
                if (j > start && frames[j].Mode != frames[j - 1].Mode) changes++;
            }
            var frame = frames[i];
            result[i] = [i == 0 ? 0 : frame.AltitudeDm - frames[i - 1].AltitudeDm,
                i == 0 ? 0 : frame.TempC10 - frames[i - 1].TempC10,
                Math.Abs((double)frame.Vibration), frame.Vibration,
                Math.Sqrt(Math.Max(0, varianceVibration / count)), Math.Sqrt(Math.Max(0, varianceAltitude / count)),
                sumAbs / count, count == 1 ? 0 : (double)changes / (count - 1),
                frame.AltitudeDm - frames[start].AltitudeDm, frame.Critical];
        }
        return result;
    }
}

using System.Globalization;
using TelemetryLink.Core.Domain;

namespace TelemetryLink.Core.InputOutput;

/// <summary>Сеанс телеметрії: кадри і, якщо файл їх містить, мітки класів.</summary>
public sealed record TelemetrySession(IReadOnlyList<TelemetryFrame> Frames, IReadOnlyList<int>? Labels)
{
    public int CriticalCount => Frames.Count(f => f.IsCritical);

    /// <summary>B_ref у бітах: обсяг сирої телеметрії без стиснення.</summary>
    public long ReferenceBits => (long)Frames.Count * TelemetryFrame.SizeInBytes * 8;
}

/// <summary>
/// Читання телеметрії з CSV.
/// Заголовок: timestamp_ms,altitude_dm,temp_c10,vibration,mode,critical[,label]
/// </summary>
public static class TelemetryCsvReader
{
    public static TelemetrySession Read(string path)
    {
        var frames = new List<TelemetryFrame>();
        var labels = new List<int>();
        bool hasLabels = false;

        using var reader = new StreamReader(path);
        string? header = reader.ReadLine()
            ?? throw new InvalidDataException($"Файл {path} порожній.");

        var columns = header.Split(',');
        int labelColumn = Array.IndexOf(columns, "label");
        hasLabels = labelColumn >= 0;

        int lineNumber = 1;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = line.Split(',');
            if (fields.Length < 6)
                throw new InvalidDataException($"{path}, рядок {lineNumber}: очікується щонайменше 6 полів.");

            var frame = new TelemetryFrame(
                uint.Parse(fields[0], CultureInfo.InvariantCulture),
                short.Parse(fields[1], CultureInfo.InvariantCulture),
                short.Parse(fields[2], CultureInfo.InvariantCulture),
                short.Parse(fields[3], CultureInfo.InvariantCulture),
                byte.Parse(fields[4], CultureInfo.InvariantCulture),
                byte.Parse(fields[5], CultureInfo.InvariantCulture));

            // Валідація вхідної телеметрії — критерій C2, пункт 1.
            if (!frame.IsValid)
                throw new InvalidDataException(
                    $"{path}, рядок {lineNumber}: mode={frame.Mode}, critical={frame.Critical} поза допустимим діапазоном.");

            frames.Add(frame);
            if (hasLabels) labels.Add(int.Parse(fields[labelColumn], CultureInfo.InvariantCulture));
        }

        return new TelemetrySession(frames, hasLabels ? labels : null);
    }
}

/// <summary>Читання телеметрії з бінарного файла: по 12 байтів на кадр.</summary>
public static class TelemetryBinaryReader
{
    public static TelemetrySession Read(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length % TelemetryFrame.SizeInBytes != 0)
            throw new InvalidDataException(
                $"{path}: довжина {bytes.Length} не кратна {TelemetryFrame.SizeInBytes} байтам.");

        return new TelemetrySession(TelemetryFrame.Deserialize(bytes), null);
    }

    public static void Write(string path, IReadOnlyList<TelemetryFrame> frames)
        => File.WriteAllBytes(path, TelemetryFrame.Serialize(frames));
}

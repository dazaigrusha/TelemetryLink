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
        string[] expected = ["timestamp_ms", "altitude_dm", "temp_c10", "vibration", "mode", "critical"];
        if ((columns.Length != 6 && columns.Length != 7)
            || !columns.Take(6).SequenceEqual(expected)
            || (columns.Length == 7 && columns[6] != "label"))
            throw new InvalidDataException($"{path}: невірний заголовок CSV.");
        int labelColumn = Array.IndexOf(columns, "label");
        hasLabels = labelColumn >= 0;

        int lineNumber = 1;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = line.Split(',');
            if (fields.Length != columns.Length)
                throw new InvalidDataException($"{path}, рядок {lineNumber}: кількість полів не відповідає заголовку.");

            try
            {
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
                if (hasLabels)
                {
                    int label = int.Parse(fields[labelColumn], CultureInfo.InvariantCulture);
                    if (label < 0 || label > 2)
                        throw new InvalidDataException($"{path}, рядок {lineNumber}: label має бути 0..2.");
                    labels.Add(label);
                }
            }
            catch (Exception exception) when (exception is FormatException or OverflowException)
            {
                throw new InvalidDataException($"{path}, рядок {lineNumber}: некоректне число або вихід за діапазон типу.", exception);
            }
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

        var frames = TelemetryFrame.Deserialize(bytes);
        for (int i = 0; i < frames.Length; i++)
            if (!frames[i].IsValid)
                throw new InvalidDataException($"{path}, кадр {i}: mode має бути 0..7, critical — 0 або 1.");
        return new TelemetrySession(frames, null);
    }

    public static void Write(string path, IReadOnlyList<TelemetryFrame> frames)
        => File.WriteAllBytes(path, TelemetryFrame.Serialize(frames));
}

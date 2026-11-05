using System.Buffers.Binary;

namespace TelemetryLink.Core.Domain;

/// <summary>
/// Кадр телеметрії. Рівно 12 байтів, big-endian (FORMAT_SPEC §1).
/// Це базова одиниця даних: B_ref у метриці Q дорівнює 12 × кількість кадрів.
/// </summary>
public readonly record struct TelemetryFrame(
    uint TimestampMs,
    short AltitudeDm,
    short TempC10,
    short Vibration,
    byte Mode,
    byte Critical)
{
    public const int SizeInBytes = 12;

    /// <summary>Кадр важливий для доставки. Дає компоненту R метрики Q.</summary>
    public bool IsCritical => Critical == 1;

    /// <summary>Серіалізує кадр у 12 байтів без вирівнювання і доповнення.</summary>
    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < SizeInBytes)
            throw new ArgumentException($"Потрібно {SizeInBytes} байтів.", nameof(destination));

        BinaryPrimitives.WriteUInt32BigEndian(destination[..4], TimestampMs);
        BinaryPrimitives.WriteInt16BigEndian(destination.Slice(4, 2), AltitudeDm);
        BinaryPrimitives.WriteInt16BigEndian(destination.Slice(6, 2), TempC10);
        BinaryPrimitives.WriteInt16BigEndian(destination.Slice(8, 2), Vibration);
        destination[10] = Mode;
        destination[11] = Critical;
    }

    public static TelemetryFrame ReadFrom(ReadOnlySpan<byte> source)
    {
        if (source.Length < SizeInBytes)
            throw new ArgumentException($"Потрібно {SizeInBytes} байтів.", nameof(source));

        return new TelemetryFrame(
            BinaryPrimitives.ReadUInt32BigEndian(source[..4]),
            BinaryPrimitives.ReadInt16BigEndian(source.Slice(4, 2)),
            BinaryPrimitives.ReadInt16BigEndian(source.Slice(6, 2)),
            BinaryPrimitives.ReadInt16BigEndian(source.Slice(8, 2)),
            source[10],
            source[11]);
    }

    /// <summary>
    /// Валідація вхідної телеметрії (критерій C2, пункт 1): mode тільки 0..7,
    /// critical тільки 0 або 1. Інші значення недопустимі.
    /// </summary>
    public bool IsValid => Mode <= 7 && Critical <= 1;

    /// <summary>Байти всіх кадрів підряд — те, що подається кодеку.</summary>
    public static byte[] Serialize(IReadOnlyList<TelemetryFrame> frames)
    {
        var bytes = new byte[frames.Count * SizeInBytes];
        for (int i = 0; i < frames.Count; i++)
            frames[i].WriteTo(bytes.AsSpan(i * SizeInBytes, SizeInBytes));
        return bytes;
    }

    public static TelemetryFrame[] Deserialize(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length % SizeInBytes != 0)
            throw new ArgumentException("Довжина не кратна 12 байтам.", nameof(bytes));

        var frames = new TelemetryFrame[bytes.Length / SizeInBytes];
        for (int i = 0; i < frames.Length; i++)
            frames[i] = ReadFrom(bytes.Slice(i * SizeInBytes, SizeInBytes));
        return frames;
    }
}

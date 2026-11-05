namespace TelemetryLink.Core.Format;

/// <summary>Прапорці навантаження (FORMAT_SPEC §3.1).</summary>
[Flags]
public enum PacketFlags : byte
{
    None = 0x00,

    /// <summary>Кодуються залишки прогнозу. КОМАНДНЕ РОЗШИРЕННЯ (§3.1.1).</summary>
    Prediction = 0x01,

    /// <summary>Пакет містить дубльовані критичні кадри. КОМАНДНЕ РОЗШИРЕННЯ (§3.1.1).</summary>
    Duplication = 0x02,

    /// <summary>Присутня секція class_table.</summary>
    ClassTable = 0x04,

    /// <summary>Сирі байти без кодування Гаффмана (свідома відмова від стиснення).</summary>
    NoCompression = 0x08,

    /// <summary>Біти 4–7 зарезервовані і мають бути нулями.</summary>
    Reserved = 0xF0
}

public static class PacketFlagsExtensions
{
    /// <summary>
    /// Базовий формат — це flags &amp; 0x03 == 0. Тільки на таких пакетах
    /// перевіряється крос-сумісність між командами (кейс 18).
    /// </summary>
    public static bool IsBaseFormat(this PacketFlags flags)
        => (flags & (PacketFlags.Prediction | PacketFlags.Duplication)) == 0;
}

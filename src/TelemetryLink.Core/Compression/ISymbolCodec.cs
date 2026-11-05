using TelemetryLink.Core.Format;

namespace TelemetryLink.Core.Compression;

/// <summary>
/// Точка підключення «кодування байтів».
///
/// Кодек не знає нічого про телеметрію: він бачить послідовність байтів.
/// Завдяки цьому його можна тестувати на рядку «aaabbc», а не на сеансі
/// з трьох тисяч кадрів.
/// </summary>
public interface ISymbolCodec
{
    /// <summary>Ім'я для CLI і звітів: huffman, none.</summary>
    string Name { get; }

    /// <summary>Прапорці, які кодек додає до навантаження.</summary>
    PacketFlags Flags { get; }

    /// <summary>
    /// Будує таблицю за вибіркою. Викликається ОДИН раз на вікно кандидатів,
    /// до вибору кадрів: інакше вага кадру залежала б від того, які кадри
    /// обрано, а вибір — від ваг (FORMAT_SPEC §3.5).
    /// </summary>
    CodeTable BuildTable(ReadOnlySpan<byte> sample);

    /// <summary>Кодує дані ТІЄЮ САМОЮ таблицею, що використана для ваг.</summary>
    byte[] Encode(ReadOnlySpan<byte> data, CodeTable table);

    /// <summary>Декодує рівно symbolCount символів.</summary>
    RejectionCode Decode(ReadOnlyMemory<byte> bitstream, CodeTable table, int symbolCount, out byte[] data);
}

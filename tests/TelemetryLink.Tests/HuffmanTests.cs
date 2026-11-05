using System.Text;
using TelemetryLink.Core.Compression;
using TelemetryLink.Core.Format;

namespace TelemetryLink.Tests;

/// <summary>
/// ЗРАЗОК. Цей файл показує, як мають виглядати ваші тести.
///
/// Чотири категорії, по одній на кожен тип сценарію:
///
///   стандартний   типовий вхід, очікуваний результат
///   крайній       вироджений вхід, межа діапазону
///   хибний        некоректний вхід → визначена відмова, а не виняток
///   інваріант     властивість, яка має триматися завжди
///
/// Назва тесту пояснює СЦЕНАРІЙ: Що_ЗаЯкихУмов_ЩоМаєСтатися.
/// Один тест — одна причина падіння.
///
/// Решта тестів цього файла — заготовки. Знімайте Skip і реалізуйте їх
/// у міру просування; перелік усіх потрібних тестів — у TestsToWrite.cs.
/// </summary>
public class HuffmanTests
{
    private readonly HuffmanCodec _codec = new();

    private byte[] RoundTrip(byte[] data)
    {
        var table = _codec.BuildTable(data);
        byte[] bitstream = _codec.Encode(data, table);
        Assert.Equal(RejectionCode.None, _codec.Decode(bitstream, table, data.Length, out byte[] decoded));
        return decoded;
    }

    // ---------- СТАНДАРТНИЙ СЦЕНАРІЙ ----------

    [Fact]
    public void EncodeThenDecode_ReturnsOriginal_ForTextData()
    {
        byte[] data = Encoding.UTF8.GetBytes("абракадабра-abracadabra-0123456789");

        Assert.Equal(data, RoundTrip(data));
    }

    /// <summary>
    /// Звірка з контрольним прикладом FORMAT_SPEC §7. Якщо довжини збігаються,
    /// а коди ні — помилка в алгоритмі §3.3, а не в побудові дерева.
    /// </summary>
    [Fact]
    public void SpecificationExample_ProducesExpectedCodesAndBitstream()
    {
        byte[] frame = Convert.FromHexString("00000000" + "03E8" + "00D7" + "FFFD" + "01" + "01");

        var table = _codec.BuildTable(frame);

        Assert.Equal(7, table.SymbolCount);
        Assert.Equal(1, table.LengthOf(0x00));
        Assert.Equal(3, table.LengthOf(0x01));

        // Код 100 дістається символу 01, бо він МЕНШИЙ ЗА ЗНАЧЕННЯМ, ніж FF.
        Assert.Equal(0b100u, table.CodeOf(0x01));
        Assert.Equal(0b101u, table.CodeOf(0xFF));

        Assert.Equal("0CE6DF90", Convert.ToHexString(_codec.Encode(frame, table)));
    }

    // ---------- КРАЙНІЙ ВИПАДОК ----------

    /// <summary>Приймальний кейс C1 №7. Довжина 0 тут — невідповідність формату.</summary>
    [Fact]
    public void SingleDistinctSymbol_GetsLengthOne_AndCodeZero()
    {
        byte[] data = Enumerable.Repeat((byte)0xAB, 100).ToArray();

        var table = _codec.BuildTable(data);

        Assert.Equal(1, table.SymbolCount);
        Assert.Equal(1, table.LengthOf(0xAB));
        Assert.Equal(0u, table.CodeOf(0xAB));
        Assert.Equal(data, RoundTrip(data));
    }

    // ---------- ХИБНИЙ СЦЕНАРІЙ ----------

    /// <summary>Зіпсовані дані дають КОД ВІДМОВИ, а не виняток.</summary>
    [Fact]
    public void Decode_BitstreamEndsMidCode_ReturnsECode()
    {
        byte[] data = Encoding.UTF8.GetBytes("abracadabra");
        var table = _codec.BuildTable(data);
        byte[] bitstream = _codec.Encode(data, table);

        Assert.Equal(RejectionCode.E_CODE, _codec.Decode(bitstream, table, data.Length + 50, out _));
    }

    // ---------- ІНВАРІАНТ ----------

    /// <summary>Жоден код не є префіксом іншого — інакше декодування неоднозначне.</summary>
    [Fact]
    public void Table_IsPrefixFree()
    {
        var random = new Random(3);
        var data = new byte[2000];
        random.NextBytes(data);
        var table = _codec.BuildTable(data);

        var codes = table.Entries
            .Select(e => Convert.ToString(table.CodeOf(e.Symbol), 2).PadLeft(e.Length, '0'))
            .ToList();

        foreach (string code in codes)
            foreach (string other in codes)
                if (!ReferenceEquals(code, other) && code.Length < other.Length)
                    Assert.False(other.StartsWith(code, StringComparison.Ordinal),
                        $"Код {code} є префіксом коду {other}.");
    }

    // ---------- ЗАГОТОВКИ ----------

    [Fact(Skip = "TODO(S3)")]
    public void EncodeThenDecode_ReturnsOriginal_ForRandomData() { }

    [Fact(Skip = "TODO(S3): усі 256 символів мають отримати довжину 8")]
    public void AllSymbolsEquallyLikely_ProducesFlatTable() { }

    [Fact(Skip = "TODO(S3): порожній вхід → порожня таблиця, без падіння")]
    public void EmptyInput_ProducesEmptyTable() { }

    [Fact(Skip = "TODO(S3): частоти Фібоначчі, жоден код не довший за 15 біт")]
    public void NoCodeLengthExceeds15Bits_OnAdversarialFrequencies() { }

    [Fact(Skip = "TODO(S3): однакові дані → однакові коди від запуску до запуску")]
    public void BuildTable_IsDeterministic_AcrossRuns() { }

    [Fact(Skip = "TODO(S3): символ, відсутній у таблиці, — помилка програміста")]
    public void Encode_SymbolAbsentFromTable_Throws() { }
}

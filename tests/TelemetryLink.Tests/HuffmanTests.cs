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

    [Fact]
    public void EncodeThenDecode_ReturnsOriginal_ForRandomData()
    {
        var random = new Random(42);
        for (int sample = 0; sample < 100; sample++)
        {
            var data = new byte[random.Next(0, 4097)];
            random.NextBytes(data);
            Assert.Equal(data, RoundTrip(data));
        }
    }

    [Fact]
    public void AllSymbolsEquallyLikely_ProducesFlatTable()
    {
        byte[] data = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();
        var table = _codec.BuildTable(data);
        Assert.Equal(256, table.SymbolCount);
        Assert.All(table.Entries, entry => Assert.Equal((byte)8, entry.Length));
        Assert.Equal(data, _codec.Encode(data, table));
        Assert.Equal(data, RoundTrip(data));
    }

    [Fact]
    public void EmptyInput_ProducesEmptyTable()
    {
        Assert.Empty(_codec.BuildTable(Array.Empty<byte>()).Entries);
        Assert.Empty(RoundTrip(Array.Empty<byte>()));
    }

    [Fact]
    public void NoCodeLengthExceeds15Bits_OnAdversarialFrequencies()
    {
        var frequencies = new int[256];
        frequencies[0] = frequencies[1] = 1;
        for (int i = 2; i < 46; i++) frequencies[i] = checked(frequencies[i - 1] + frequencies[i - 2]);
        int[] original = (int[])frequencies.Clone();
        byte[] lengths = HuffmanTreeBuilder.BuildLengths(frequencies);
        Assert.Equal(original, frequencies);
        for (int i = 0; i < 46; i++) Assert.InRange((int)lengths[i], 1, 15);
        Assert.All(lengths.Skip(46), length => Assert.Equal((byte)0, length));
        var table = CodeTable.FromLengths(lengths);
        byte[] symbols = Enumerable.Range(0, 46).Select(i => (byte)i).ToArray();
        byte[] encoded = _codec.Encode(symbols, table);
        Assert.Equal(RejectionCode.None, _codec.Decode(encoded, table, symbols.Length, out var decoded));
        Assert.Equal(symbols, decoded);
    }

    [Fact]
    public void BuildTable_IsDeterministic_AcrossRuns()
    {
        byte[] data = Encoding.UTF8.GetBytes("deterministic huffman tree 0123456789");
        var expected = _codec.BuildTable(data);
        for (int i = 0; i < 10; i++)
        {
            var actual = _codec.BuildTable(data.Reverse().ToArray());
            Assert.Equal(expected.Entries.ToArray(), actual.Entries.ToArray());
            Assert.Equal(_codec.Encode(data, expected), _codec.Encode(data, actual));
        }
    }

    [Fact]
    public void Encode_SymbolAbsentFromTable_Throws()
        => Assert.Throws<ArgumentException>(() => _codec.Encode(new byte[] { 2 }, _codec.BuildTable(new byte[] { 1 })));

    [Fact]
    public void ClassicHistogram_HasKnownOptimalWeightedLength()
    {
        var frequencies = new int[256];
        new[] { 5, 9, 12, 13, 16, 45 }.CopyTo(frequencies, 0);
        byte[] lengths = HuffmanTreeBuilder.BuildLengths(frequencies);
        Assert.Equal(224L, Enumerable.Range(0, 6).Sum(i => (long)frequencies[i] * lengths[i]));
    }

    [Fact]
    public void MaximumFrequencies_DoNotOverflowInternalWeights()
    {
        byte[] lengths = HuffmanTreeBuilder.BuildLengths(Enumerable.Repeat(int.MaxValue, 256).ToArray());
        Assert.All(lengths, length => Assert.Equal((byte)8, length));
    }

    [Fact]
    public void InvalidFrequencies_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => HuffmanTreeBuilder.BuildLengths(new int[255]));
        var frequencies = new int[256];
        frequencies[0] = -1;
        Assert.Throws<ArgumentException>(() => HuffmanTreeBuilder.BuildLengths(frequencies));
    }

    [Fact]
    public void Limiter_RaisesOnlyPresentSymbols_AndMakesProgress()
    {
        var frequencies = new long[256];
        frequencies[0] = 1;
        frequencies[1] = 1000000;
        CodeLengthLimiter.RaiseFloor(new[] { 0, 1 }, frequencies, 15);
        long previous = frequencies[0];
        Assert.True(previous > 1);
        CodeLengthLimiter.RaiseFloor(new[] { 0, 1 }, frequencies, 15);
        Assert.True(frequencies[0] > previous);
        Assert.Equal(0L, frequencies[2]);
    }

    [Theory]
    [InlineData("ref_01_single_frame")]
    [InlineData("ref_03_degenerate")]
    [InlineData("ref_04_session")]
    [InlineData("ref_05_no_compression")]
    [InlineData("ref_06_class_table")]
    public void BuildTable_MatchesReferenceFrequencies(string name)
    {
        var result = PacketReader.Read(File.ReadAllBytes(DataKit.ReferencePath(name + ".bin")), DataKit.Keys());
        Assert.True(result.IsAccepted);
        byte[] data = TelemetryLink.Core.Domain.TelemetryFrame.Serialize(result.Frames);
        Assert.Equal(data, RoundTrip(data));
        // Сирий еталон таблиці не містить. Решта таблиць побудовані за тими самими кадрами.
        if ((result.Payload!.Flags & PacketFlags.NoCompression) == 0)
            Assert.Equal(result.Payload.Table.Entries.ToArray(), _codec.BuildTable(data).Entries.ToArray());
    }
}

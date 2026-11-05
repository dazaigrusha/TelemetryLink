using TelemetryLink.Core.Format;

namespace TelemetryLink.Tests;

/// <summary>
/// Перевірки на ЧУЖИХ даних — еталонних пакетах викладача.
///
/// Це найцінніший набір тестів у проєкті. Власний передавач і власний приймач
/// можуть мати однакову помилку і чудово розуміти один одного; справжня
/// перевірка формату — це пакети, які згенерувала інша реалізація.
/// </summary>
public class ReferencePacketTests
{
    [Theory]
    [InlineData("ref_01_single_frame.bin", 1)]
    [InlineData("ref_02_empty.bin", 0)]
    [InlineData("ref_03_degenerate.bin", 1)]
    [InlineData("ref_04_session.bin", 64)]
    [InlineData("ref_05_no_compression.bin", 32)]
    [InlineData("ref_06_class_table.bin", 32)]
    public void ReferencePacket_IsAccepted_WithExpectedFrameCount(string fileName, int expectedFrames)
    {
        byte[] packet = File.ReadAllBytes(DataKit.ReferencePath(fileName));

        var result = PacketReader.Read(packet, DataKit.Keys());

        Assert.Equal(RejectionCode.None, result.Code);
        Assert.Equal(expectedFrames, result.Frames.Count);
    }

    [Theory]
    [InlineData("bad_01_tampered_byte.bin", RejectionCode.E_HMAC)]
    [InlineData("bad_02_tampered_tag.bin", RejectionCode.E_HMAC)]
    [InlineData("bad_03_truncated.bin", RejectionCode.E_LENGTH)]
    [InlineData("bad_04_bad_magic.bin", RejectionCode.E_MAGIC)]
    [InlineData("bad_05_reserved.bin", RejectionCode.E_PAYLOAD)]
    public void CorruptedPacket_IsRejected_WithExpectedCode(string fileName, RejectionCode expected)
    {
        byte[] packet = File.ReadAllBytes(DataKit.ReferencePath(fileName));

        var result = PacketReader.Read(packet, DataKit.Keys());

        Assert.Equal(expected, result.Code);
        Assert.Empty(result.Frames);
    }

    /// <summary>
    /// Кейс C1 №6: дерево Гаффмана відновлюється з канонічних довжин кодів.
    /// Довжини і коди звіряються з ref_01_single_frame.json — файлом, який
    /// написала чужа реалізація.
    /// </summary>
    [Fact]
    public void SingleFramePacket_CodeTable_MatchesReferenceJson()
    {
        byte[] packet = File.ReadAllBytes(DataKit.ReferencePath("ref_01_single_frame.bin"));
        var result = PacketReader.Read(packet, DataKit.Keys());
        Assert.Equal(RejectionCode.None, result.Code);

        using var expected = DataKit.ReferenceJson("ref_01_single_frame.json");
        var table = result.Payload!.Table;

        foreach (var entry in expected.RootElement.GetProperty("таблиця_кодів").EnumerateArray())
        {
            byte symbol = (byte)entry.GetProperty("символ").GetInt32();
            byte length = (byte)entry.GetProperty("довжина").GetInt32();
            string code = entry.GetProperty("код").GetString()!;

            Assert.Equal(length, table.LengthOf(symbol));
            Assert.Equal(code, Convert.ToString(table.CodeOf(symbol), 2).PadLeft(length, '0'));
        }
    }

    /// <summary>
    /// Кейс C1 №12: max_packet_bytes — обмеження ПЕРЕДАВАЧА, а не умова
    /// валідності формату. ref_04 має 771 байт при ліміті 512 і має
    /// прийматися. Приймач, який відхиляє пакет за розміром, кейс не проходить.
    /// </summary>
    [Fact]
    public void PacketLargerThanMaxPacketBytes_IsStillAccepted()
    {
        byte[] packet = File.ReadAllBytes(DataKit.ReferencePath("ref_04_session.bin"));
        Assert.True(packet.Length > DataKit.Config.MaxPacketBytes);

        Assert.Equal(RejectionCode.None, PacketReader.Read(packet, DataKit.Keys()).Code);
    }

    /// <summary>
    /// Кейс C1 №11: невірний пароль дає E_HMAC, а не спотворені кадри.
    /// Розшифрування не повинно навіть починатися.
    /// </summary>
    [Fact]
    public void ReferencePacket_WrongPassword_ReturnsEHmac()
    {
        byte[] packet = File.ReadAllBytes(DataKit.ReferencePath("ref_04_session.bin"));

        var result = PacketReader.Read(packet, DataKit.Keys("не той пароль"));

        Assert.Equal(RejectionCode.E_HMAC, result.Code);
        Assert.Empty(result.Frames);
    }

    /// <summary>ref_06 містить необов'язкову секцію class_table — приймач має її пропустити.</summary>
    [Fact]
    public void ClassTablePacket_ParsesOptionalSection()
    {
        byte[] packet = File.ReadAllBytes(DataKit.ReferencePath("ref_06_class_table.bin"));

        var result = PacketReader.Read(packet, DataKit.Keys());

        Assert.Equal(RejectionCode.None, result.Code);
        Assert.NotNull(result.Payload!.ClassTable);
        Assert.Equal(result.Frames.Count, result.Payload.ClassTable!.Length);
    }

    /// <summary>ref_03: вироджений алфавіт — один символ, довжина коду 1 (кейс C1 №7).</summary>
    [Fact]
    public void DegeneratePacket_HasSingleSymbolWithLengthOne()
    {
        byte[] packet = File.ReadAllBytes(DataKit.ReferencePath("ref_03_degenerate.bin"));

        var result = PacketReader.Read(packet, DataKit.Keys());

        Assert.Equal(RejectionCode.None, result.Code);
        Assert.Equal(1, result.Payload!.Table.SymbolCount);
        Assert.Equal(1, result.Payload.Table.Entries[0].Length);
    }
}

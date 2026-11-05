using TelemetryLink.Core.Format;

namespace TelemetryLink.Tests;

public class CanonicalCodesTests
{
    [Fact]
    public void Assign_MatchesSpecificationExample()
    {
        var lengths = new byte[256];
        new byte[] { 1, 3, 4, 4, 4, 4, 3 }.CopyTo(lengths, 0);
        Assert.Equal(new uint[] { 0, 4, 12, 13, 14, 15, 5 }, CanonicalCodes.Assign(lengths).Take(7).ToArray());
    }

    [Fact]
    public void EqualLengths_GetCodesInAscendingSymbolOrder()
    {
        var table = CodeTable.FromEntries(new[] { new CodeTableEntry(9, 1), new CodeTableEntry(2, 1) });
        Assert.Equal(0u, table.CodeOf(2));
        Assert.Equal(1u, table.CodeOf(9));
    }

    [Fact]
    public void Assign_EmptyLengths_ProducesNoCodes() => Assert.Empty(CodeTable.Empty.Entries);

    [Fact]
    public void Assign_LengthAbove15_Throws()
    {
        var lengths = new byte[256];
        lengths[0] = 16;
        Assert.Throws<ArgumentException>(() => CanonicalCodes.Assign(lengths));
    }

    [Fact]
    public void Assign_OversubscribedLengths_Throws()
    {
        var lengths = new byte[256];
        lengths[0] = lengths[1] = lengths[2] = 1;
        Assert.Throws<ArgumentException>(() => CanonicalCodes.Assign(lengths));
    }

    [Fact]
    public void Decoder_RestoresSymbolsAndRejectsNonzeroPadding()
    {
        var table = CodeTable.FromEntries(new[] { new CodeTableEntry(2, 1), new CodeTableEntry(9, 1) });
        Assert.Equal(RejectionCode.None, CanonicalDecoder.TryDecode(new byte[] { 0x40 }, table, 3, out var data));
        Assert.Equal(new byte[] { 2, 9, 2 }, data);
        Assert.Equal(RejectionCode.E_CODE, CanonicalDecoder.TryDecode(new byte[] { 0x41 }, table, 3, out _));
    }

    [Fact]
    public void FromLengths_CopiesInput()
    {
        var lengths = new byte[256];
        lengths[2] = 1;
        var table = CodeTable.FromLengths(lengths);
        lengths[2] = 7;
        Assert.Equal((byte)1, table.LengthOf(2));
    }
}

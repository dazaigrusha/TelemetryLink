using TelemetryLink.Core.Format;

namespace TelemetryLink.Tests;

public class BitIoTests
{
    [Fact]
    public void WriteBits_PacksMostSignificantBitFirst()
    {
        var writer = new BitWriter();
        writer.WriteBit(1);
        Assert.Equal(new byte[] { 0x80 }, writer.ToArray());
    }

    [Fact]
    public void SpecificationExample_ProducesExpectedBytes()
    {
        var writer = new BitWriter();
        writer.WriteBits(0x0CE6DF9, 28);
        Assert.Equal(Convert.FromHexString("0CE6DF90"), writer.ToArray());
        Assert.Equal(28, writer.BitCount);
    }

    [Fact]
    public void ReadBit_ReturnsSameSequenceThatWasWritten()
    {
        var writer = new BitWriter();
        int[] bits = [1, 0, 1, 1, 0, 0, 1, 0, 1];
        foreach (int bit in bits) writer.WriteBit(bit);
        var reader = new BitReader(writer.ToArray());
        foreach (int expected in bits)
        {
            Assert.True(reader.TryReadBit(out int actual));
            Assert.Equal(expected, actual);
        }
        Assert.True(reader.RemainingBitsAreZero());
    }

    [Fact]
    public void LastByte_IsPaddedWithZeros()
    {
        var writer = new BitWriter();
        writer.WriteBits(5, 3);
        Assert.Equal(new byte[] { 0xA0 }, writer.ToArray());
        Assert.Equal(writer.ToArray(), writer.ToArray());
        writer.WriteBit(1);
        Assert.Equal(new byte[] { 0xB0 }, writer.ToArray());
    }

    [Fact]
    public void NoBitsWritten_ProducesEmptyArray() => Assert.Empty(new BitWriter().ToArray());

    [Fact]
    public void TryReadBit_PastEndOfStream_ReturnsFalse()
    {
        var reader = new BitReader(new byte[] { 0 });
        for (int i = 0; i < 8; i++) Assert.True(reader.TryReadBit(out _));
        Assert.False(reader.TryReadBit(out _));
        Assert.Equal(8, reader.BitPosition);
    }

    [Fact]
    public void RemainingBitsAreZero_DetectsNonZeroPadding()
    {
        var reader = new BitReader(new byte[] { 0x81 });
        Assert.True(reader.TryReadBit(out _));
        Assert.False(reader.RemainingBitsAreZero());
        Assert.Equal(1, reader.BitPosition);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(33)]
    public void WriteBits_InvalidCount_Throws(int count)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new BitWriter().WriteBits(0, count));
}

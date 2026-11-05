using TelemetryLink.Core.Domain;

namespace TelemetryLink.Tests;

public class TelemetryFrameTests
{
    private static readonly TelemetryFrame Sample = new(0x01020304, -2, 229, -3, 7, 1);

    [Fact]
    public void WriteTo_ProducesExactly12Bytes_InBigEndian()
    {
        byte[] bytes = TelemetryFrame.Serialize(new[] { Sample });
        Assert.Equal(Convert.FromHexString("01020304FFFE00E5FFFD0701"), bytes);
    }

    [Fact]
    public void ReadFrom_OfWriteTo_RestoresFrame()
    {
        byte[] bytes = TelemetryFrame.Serialize(new[] { Sample });
        Assert.Equal(Sample, TelemetryFrame.ReadFrom(bytes));
    }

    [Fact]
    public void NegativeValues_UseTwosComplement()
    {
        byte[] bytes = TelemetryFrame.Serialize(new[] { Sample });
        Assert.Equal((byte)0xFF, bytes[8]);
        Assert.Equal((byte)0xFD, bytes[9]);
    }

    [Fact]
    public void Serialize_EmptyList_ProducesEmptyArray()
        => Assert.Empty(TelemetryFrame.Serialize(Array.Empty<TelemetryFrame>()));

    [Theory]
    [InlineData(8, 0)]
    [InlineData(0, 2)]
    public void InvalidFieldValues_AreRejectedByValidation(byte mode, byte critical)
        => Assert.False((Sample with { Mode = mode, Critical = critical }).IsValid);

    [Fact]
    public void Deserialize_LengthNotMultipleOf12_Throws()
        => Assert.Throws<ArgumentException>(() => TelemetryFrame.Deserialize(new byte[13]));
}

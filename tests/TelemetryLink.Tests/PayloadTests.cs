using System.Buffers.Binary;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Format;
using TelemetryLink.Core.Compression;

namespace TelemetryLink.Tests;

public class PayloadTests
{
    private static byte[] Raw(int count = 1, bool withClasses = false)
    {
        var frame = new TelemetryFrame(54, 1138, 229, -3, 4, 1);
        var bytes = TelemetryFrame.Serialize(Enumerable.Repeat(frame, count).ToArray());
        var codec = new NoCompressionCodec();
        return PayloadWriter.Write(new PayloadContent(count,
            codec.Flags | (withClasses ? PacketFlags.ClassTable : PacketFlags.None),
            codec.BuildTable(bytes), bytes.Length, codec.Encode(bytes, CodeTable.Empty),
            withClasses ? new byte[count] : null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(64)]
    public void WriteThenRead_RestoresAllSections(int count)
    {
        byte[] bytes = Raw(count);
        Assert.Equal(14 + count * 12, bytes.Length);
        Assert.Equal(RejectionCode.None, PayloadReader.TryRead(bytes, out var info));
        Assert.NotNull(info);
        Assert.Equal(count, info!.Frames.Count);
        Assert.Equal(count * 12, info.SymbolCount);
        Assert.Equal(bytes, PayloadWriter.Write(new PayloadContent(info.FrameCount, info.Flags,
            info.Table, info.SymbolCount, info.Bitstream, info.ClassTable)));
    }

    [Fact]
    public void Write_SingleFrame_MatchesIndependentHexVector()
        => Assert.Equal(Convert.FromHexString("0001080000000000000C0000000C00000036047200E5FFFD0401"), Raw());

    [Theory]
    [InlineData(3, 1)]
    [InlineData(2, 0x18)]
    [InlineData(2, 0x09)]
    [InlineData(2, 0x0A)]
    [InlineData(9, 11)]
    [InlineData(13, 11)]
    [InlineData(24, 8)]
    [InlineData(25, 2)]
    public void Read_InvalidFields_ReturnsEPayload(int offset, byte value)
    {
        byte[] bytes = Raw();
        bytes[offset] = value;
        Assert.Equal(RejectionCode.E_PAYLOAD, PayloadReader.TryRead(bytes, out var info));
        Assert.Null(info);
    }

    [Fact]
    public void Read_ExtraTrailingBytes_ReturnsEPayload()
        => Assert.Equal(RejectionCode.E_PAYLOAD, PayloadReader.TryRead(Raw().Concat(new byte[1]).ToArray(), out _));

    [Fact]
    public void Read_EveryTruncation_ReturnsRejection()
    {
        byte[] bytes = Raw();
        for (int length = 0; length < bytes.Length; length++)
            Assert.NotEqual(RejectionCode.None, PayloadReader.TryRead(bytes.AsSpan(0, length), out _));
    }

    [Fact]
    public void Read_HugeStreamLength_ReturnsEPayload()
    {
        byte[] bytes = Raw();
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(10), uint.MaxValue);
        Assert.Equal(RejectionCode.E_PAYLOAD, PayloadReader.TryRead(bytes, out _));
    }

    [Fact]
    public void ClassTable_RoundTripsAndRejectsInvalidClass()
    {
        byte[] bytes = Raw(1, true);
        Assert.Equal(RejectionCode.None, PayloadReader.TryRead(bytes, out var info));
        Assert.Equal(new byte[] { 0 }, info!.ClassTable);
        bytes[^1] = 3;
        Assert.Equal(RejectionCode.E_PAYLOAD, PayloadReader.TryRead(bytes, out _));
    }

    [Fact]
    public void NoCompression_DecodeRejectsWrongLength()
        => Assert.Equal(RejectionCode.E_PAYLOAD,
            new NoCompressionCodec().Decode(new byte[1], CodeTable.Empty, 2, out _));
}

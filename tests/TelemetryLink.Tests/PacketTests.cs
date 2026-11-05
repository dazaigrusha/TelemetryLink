using System.Buffers.Binary;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Format;
using TelemetryLink.Core.Security;

namespace TelemetryLink.Tests;

public class PacketTests
{
    private static readonly byte[] Salt = new byte[16];
    private static readonly SessionKeys Keys = SessionKeys.Derive("test-password", Salt);

    private static byte[] Build()
    {
        var frame = new TelemetryFrame(54, 1138, 229, -3, 4, 1);
        return PacketWriter.Build(new PayloadContent(1, PacketFlags.NoCompression, CodeTable.Empty,
            12, TelemetryFrame.Serialize(new[] { frame })), 123, 7, Salt,
            PacketCipher.BuildNonce(new byte[8], 7), Keys);
    }

    [Fact]
    public void BuildThenRead_WorksInNoCompressionMode()
    {
        byte[] packet = Build();
        var result = PacketReader.Read(packet, Keys);
        Assert.True(result.IsAccepted);
        Assert.Equal(new TelemetryFrame(54, 1138, 229, -3, 4, 1), Assert.Single(result.Frames));
        Assert.Equal((ushort)123, result.Header.DeviceId);
        Assert.Equal(7u, result.Header.PacketSequence);
        Assert.Equal(101, packet.Length);
    }

    [Theory]
    [InlineData(43)]
    [InlineData(100)]
    [InlineData(5)]
    public void TamperedAuthenticatedBytes_ReturnEHmac(int offset)
    {
        byte[] packet = Build();
        packet[offset] ^= 1;
        var result = PacketReader.Read(packet, Keys);
        Assert.Equal(RejectionCode.E_HMAC, result.Code);
        Assert.Empty(result.Frames);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(74)]
    public void PacketShorterThan75Bytes_ReturnsEShort(int length)
        => Assert.Equal(RejectionCode.E_SHORT, PacketReader.Read(new byte[length], Keys).Code);

    [Fact]
    public void TruncatedPacket_ReturnsELength()
        => Assert.Equal(RejectionCode.E_LENGTH, PacketReader.Read(Build()[..^1], Keys).Code);

    [Fact]
    public void BadMagic_ReturnsEMagic()
    {
        byte[] packet = Build();
        packet[0] ^= 1;
        Assert.Equal(RejectionCode.E_MAGIC, PacketReader.Read(packet, Keys).Code);
    }

    [Fact]
    public void WrongPassword_ReturnsEHmac_AndNoFrames()
    {
        var result = PacketReader.Read(Build(), SessionKeys.Derive("wrong", Salt));
        Assert.Equal(RejectionCode.E_HMAC, result.Code);
        Assert.Empty(result.Frames);
    }

    [Fact]
    public void HugeDeclaredLength_ReturnsELength()
    {
        byte[] packet = Build();
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(39), uint.MaxValue);
        Assert.Equal(RejectionCode.E_LENGTH, PacketReader.Read(packet, Keys).Code);
    }

    [Fact]
    public void AuthenticatedInvalidPayload_ReturnsEPayload()
    {
        byte[] packet = Build();
        // CTR: зміна біта шифротексту змінює той самий біт reserved.
        packet[PacketHeader.SizeInBytes + 3] ^= 1;
        int tagOffset = packet.Length - PacketAuthenticator.TagSize;
        PacketAuthenticator.ComputeTag(packet.AsSpan(0, tagOffset), Keys.MacKey).CopyTo(packet, tagOffset);
        Assert.Equal(RejectionCode.E_PAYLOAD, PacketReader.Read(packet, Keys).Code);
    }

    [Fact]
    public void HmacIsCheckedBeforeDecryption()
    {
        byte[] packet = Build();
        packet[^1] ^= 1;
        // Reserved також пошкоджений: без перевірки HMAC парсер дав би E_PAYLOAD.
        packet[PacketHeader.SizeInBytes + 3] ^= 1;
        var result = PacketReader.Read(packet, Keys);
        Assert.Equal(RejectionCode.E_HMAC, result.Code);
        Assert.Null(result.Payload);
        Assert.Empty(result.Frames);
    }
    [Theory]
    [InlineData("ref_01_single_frame.bin")]
    [InlineData("ref_02_empty.bin")]
    [InlineData("ref_03_degenerate.bin")]
    [InlineData("ref_04_session.bin")]
    [InlineData("ref_05_no_compression.bin")]
    [InlineData("ref_06_class_table.bin")]
    public void RebuildReferencePacket_MatchesEveryByte(string name)
    {
        byte[] original = File.ReadAllBytes(DataKit.ReferencePath(name));
        var keys = DataKit.Keys();
        var result = PacketReader.Read(original, keys);
        Assert.True(result.IsAccepted);
        var payload = result.Payload!;
        var header = result.Header;
        byte[] rebuilt = PacketWriter.Build(new PayloadContent(payload.FrameCount, payload.Flags,
            payload.Table, payload.SymbolCount, payload.Bitstream, payload.ClassTable),
            header.DeviceId, header.PacketSequence, header.Salt, header.Nonce, keys);
        Assert.Equal(original, rebuilt);
    }
}

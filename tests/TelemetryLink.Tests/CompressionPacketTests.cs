using TelemetryLink.Core.Compression;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Format;
using TelemetryLink.Core.Security;

namespace TelemetryLink.Tests;

public class CompressionPacketTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(64)]
    public void CompressedEncryptedPacket_RestoresEveryByte(int count)
    {
        var frames = Enumerable.Range(0, count)
            .Select(i => new TelemetryFrame((uint)(i * 50), (short)(1000 + i), 215, -3, (byte)(i % 8), (byte)(i % 2))).ToArray();
        byte[] raw = TelemetryFrame.Serialize(frames);
        var codec = new HuffmanCodec();
        var table = codec.BuildTable(raw);
        var salt = new byte[16];
        var keys = SessionKeys.Derive("compression-test", salt);
        byte[] packet = PacketWriter.Build(new PayloadContent(count, PacketFlags.None, table,
            raw.Length, codec.Encode(raw, table)), 1, 0, salt, new byte[12], keys);
        var result = PacketReader.Read(packet, keys);
        Assert.True(result.IsAccepted);
        Assert.Equal(raw, TelemetryFrame.Serialize(result.Frames));
    }
}

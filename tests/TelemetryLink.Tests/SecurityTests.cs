using System.Text;
using TelemetryLink.Core.Security;

namespace TelemetryLink.Tests;

public class SecurityTests
{
    private static readonly byte[] Salt = Enumerable.Range(0, 16).Select(x => (byte)x).ToArray();

    [Fact]
    public void Derive_MatchesIndependentPbkdf2Vector()
    {
        var keys = SessionKeys.Derive("algo2026", Salt);
        Assert.Equal(Convert.FromHexString("ccbd3d1522cd4b630dce6243981ee4aef489ff8365ec919364bdd9d79a75065f"), keys.EncryptionKey);
        Assert.Equal(Convert.FromHexString("9d401d46f3935df0cbc7ad815c567a7005b9e20a1dc2188fcdb481aabbf00d2d"), keys.MacKey);
    }

    [Fact]
    public void EncryptionKey_DiffersFromMacKey()
    {
        var keys = SessionKeys.Derive("пароль", Salt);
        Assert.False(keys.EncryptionKey.SequenceEqual(keys.MacKey));
        var again = SessionKeys.Derive("пароль", Salt);
        Assert.Equal(keys.EncryptionKey, again.EncryptionKey);
    }

    [Fact]
    public void CounterBlock_FirstBlockIndexIsOne()
    {
        var block = new byte[16];
        PacketCipher.BuildCounterBlock(new byte[12], 1, block);
        Assert.Equal(Convert.FromHexString("00000000000000000000000000000001"), block);
        Assert.Throws<ArgumentOutOfRangeException>(() => PacketCipher.BuildCounterBlock(new byte[12], 0, block));
    }

    [Fact]
    public void Transform_MatchesIndependentAes256Vector()
        => Assert.Equal(Convert.FromHexString("530f8afbc74536b9a963b4f1c4cb738b"),
            PacketCipher.Transform(new byte[16], new byte[32], new byte[12]));

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(65)]
    public void Transform_PreservesLengthAndRestoresPlaintext(int length)
    {
        byte[] data = Enumerable.Range(0, length).Select(x => (byte)x).ToArray();
        var encrypted = PacketCipher.Transform(data, new byte[32], new byte[12]);
        Assert.Equal(length, encrypted.Length);
        Assert.Equal(data, PacketCipher.Transform(encrypted, new byte[32], new byte[12]));
    }

    [Fact]
    public void BuildNonce_DiffersBetweenPackets()
    {
        var prefix = Convert.FromHexString("0102030405060708");
        Assert.Equal(Convert.FromHexString("010203040506070801020304"), PacketCipher.BuildNonce(prefix, 0x01020304));
        Assert.False(PacketCipher.BuildNonce(prefix, 1).SequenceEqual(PacketCipher.BuildNonce(prefix, 2)));
    }

    [Fact]
    public void ComputeTag_MatchesRfc4231Vector()
    {
        var key = Enumerable.Repeat((byte)0x0B, 20).ToArray();
        Assert.Equal(Convert.FromHexString("b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7"),
            PacketAuthenticator.ComputeTag(Encoding.ASCII.GetBytes("Hi There"), key));
    }

    [Fact]
    public void Verify_RejectsDifferentKeyAndTruncatedTag()
    {
        var data = new byte[] { 1, 2, 3 };
        var tag = PacketAuthenticator.ComputeTag(data, new byte[32]);
        Assert.True(PacketAuthenticator.Verify(data, tag, new byte[32]));
        Assert.False(PacketAuthenticator.Verify(data, tag, Enumerable.Repeat((byte)1, 32).ToArray()));
        Assert.False(PacketAuthenticator.Verify(data, tag[..31], new byte[32]));
    }

    [Fact]
    public void InvalidParameterSizes_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => SessionKeys.Derive("p", new byte[15]));
        Assert.Throws<ArgumentException>(() => PacketCipher.Transform(new byte[1], new byte[16], new byte[12]));
        Assert.Throws<ArgumentException>(() => PacketCipher.Transform(new byte[1], new byte[32], new byte[11]));
        Assert.Throws<ArgumentException>(() => PacketCipher.BuildNonce(new byte[7], 0));
    }
}

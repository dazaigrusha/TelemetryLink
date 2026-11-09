using System.Security.Cryptography;
using TelemetryLink.Core.Compression;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Format;
using TelemetryLink.Core.Security;
namespace TelemetryLink.Core.Metrics;

public readonly record struct OrderMeasurement(string Order, long RawBits, long ResultBits)
{
    public double Ratio => ResultBits == 0 ? 0 : (double)RawBits / ResultBits;
    public long TableBits { get; init; }
    public long BitstreamBits { get; init; }
    public bool ExactRestore { get; init; }
}

/// <summary>Лабораторне порівняння обсягу, без каналу та загальних накладних витрат пакета.</summary>
public static class OrderOfOperationsExperiment
{
    public static IReadOnlyList<OrderMeasurement> Run(IReadOnlyList<TelemetryFrame> frames, string password)
    {
        ArgumentNullException.ThrowIfNull(frames); ArgumentNullException.ThrowIfNull(password);
        if (frames.Any(x => !x.IsValid)) throw new ArgumentException("Некоректна телеметрія.");
        byte[] raw = TelemetryFrame.Serialize(frames);
        var keys = SessionKeys.Derive(password, SessionKeys.GenerateSalt());
        byte[] prefix = RandomNumberGenerator.GetBytes(8);
        byte[] nonceA = PacketCipher.BuildNonce(prefix, 0), nonceB = PacketCipher.BuildNonce(prefix, 1);
        var codec = new HuffmanCodec();
        try
        {
            var firstTable = codec.BuildTable(raw); byte[] firstStream = codec.Encode(raw, firstTable);
            // Таблиця передається разом із потоком: два байти на запис, доповнення потоку враховане.
            byte[] container = new byte[firstTable.SizeInBytes + firstStream.Length];
            int position = 0;
            foreach (var entry in firstTable.Entries) { container[position++] = entry.Symbol; container[position++] = entry.Length; }
            firstStream.CopyTo(container, position);
            byte[] encrypted = PacketCipher.Transform(container, keys.EncryptionKey, nonceA);
            byte[] restored = PacketCipher.Transform(encrypted, keys.EncryptionKey, nonceA);
            var recoveredEntries = new List<CodeTableEntry>();
            for (int i = 0; i < firstTable.SizeInBytes; i += 2) recoveredEntries.Add(new(restored[i], restored[i + 1]));
            var recoveredTable = CodeTable.FromEntries(recoveredEntries);
            bool firstExact = codec.Decode(restored.AsMemory(firstTable.SizeInBytes), recoveredTable, raw.Length, out var decoded) == RejectionCode.None
                && decoded.AsSpan().SequenceEqual(raw);

            byte[] rawCiphertext = PacketCipher.Transform(raw, keys.EncryptionKey, nonceB);
            var secondTable = codec.BuildTable(rawCiphertext); byte[] secondStream = codec.Encode(rawCiphertext, secondTable);
            bool secondExact = codec.Decode(secondStream, secondTable, raw.Length, out var recoveredCiphertext) == RejectionCode.None
                && PacketCipher.Transform(recoveredCiphertext, keys.EncryptionKey, nonceB).AsSpan().SequenceEqual(raw);
            return [new("compress-first", raw.LongLength * 8, encrypted.LongLength * 8)
                { TableBits = firstTable.SizeInBytes * 8L, BitstreamBits = firstStream.LongLength * 8, ExactRestore = firstExact },
                new("encrypt-first", raw.LongLength * 8, (secondTable.SizeInBytes + secondStream.LongLength) * 8L)
                { TableBits = secondTable.SizeInBytes * 8L, BitstreamBits = secondStream.LongLength * 8, ExactRestore = secondExact }];
        }
        finally { CryptographicOperations.ZeroMemory(keys.EncryptionKey); CryptographicOperations.ZeroMemory(keys.MacKey); }
    }
}

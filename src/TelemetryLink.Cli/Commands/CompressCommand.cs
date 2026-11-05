using System.Text.Json;
using TelemetryLink.Core.Compression;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Format;

namespace TelemetryLink.Cli.Commands;

/// <summary>Діагностика стиснення сеансу без моделювання передавання каналом.</summary>
public static class CompressCommand
{
    public static int Execute(CommandLineOptions options)
    {
        var context = new CommandContext(options);
        var session = context.LoadSession();
        if (session.Frames.Count > ushort.MaxValue)
            throw new ArgumentException("Для діагностики одного навантаження допускається до 65535 кадрів.");
        byte[] raw = TelemetryFrame.Serialize(session.Frames);
        var codec = new HuffmanCodec();
        var table = codec.BuildTable(raw);
        byte[] encoded = codec.Encode(raw, table);
        byte[] payload = PayloadWriter.Write(new PayloadContent(session.Frames.Count,
            PacketFlags.None, table, raw.Length, encoded));
        var code = PayloadReader.TryRead(payload, out var restored);
        bool exact = code == RejectionCode.None && raw.AsSpan().SequenceEqual(TelemetryFrame.Serialize(restored!.Frames));
        var report = new
        {
            Frames = session.Frames.Count,
            RawBytes = raw.Length,
            EncodedBits = table.MeasureBits(raw),
            EncodedBytes = encoded.Length,
            TableBytes = table.SizeInBytes,
            PayloadBytes = payload.Length,
            RawPayloadBytes = PayloadContent.FixedFieldsSize + raw.Length,
            SavedPayloadBytes = raw.Length + PayloadContent.FixedFieldsSize - payload.Length,
            ExactRoundTrip = exact
        };
        Console.WriteLine($"Кадрів: {report.Frames}; сирі дані: {report.RawBytes} байтів.");
        Console.WriteLine($"Бітовий потік: {report.EncodedBytes} байтів ({report.EncodedBits} значущих бітів).");
        Console.WriteLine($"Таблиця: {report.TableBytes} байтів; навантаження: {report.PayloadBytes} байтів.");
        Console.WriteLine($"Економія з урахуванням таблиці: {report.SavedPayloadBytes} байтів.");
        Console.WriteLine($"Побайтове відновлення: {(exact ? "OK" : "FAIL")}");
        Console.WriteLine("Це перевірка кодека на всьому сеансі. Пакування в обмежені пакети й канал ще не виконуються.");
        string? output = options.Value("out");
        if (output is not null)
            File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return exact ? 0 : 1;
    }
}

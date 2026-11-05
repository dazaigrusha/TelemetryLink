using TelemetryLink.Core.Format;
using TelemetryLink.Core.Security;

namespace TelemetryLink.Cli.Commands;

/// <summary>
/// Показує вміст одного пакета або сеансу людині.
///
/// Потрібен на етапі налагодження формату: байти друкуються поруч із
/// розібраними полями, тому розбіжність із ref_*.json видно відразу, без
/// відладчика.
/// </summary>
public static class InspectCommand
{
    public static int Execute(CommandLineOptions options)
    {
        var context = new CommandContext(options);

        if (!options.Has("packet")) return InspectSession(context);

        string path = options.Require("packet");
        byte[] packet = File.ReadAllBytes(path);

        if (packet.Length < PacketWriter.MinimumPacketSize)
        {
            Console.WriteLine($"Файл {path}: {packet.Length} байт — коротший за мінімальні 75.");
            return 1;
        }

        var header = PacketHeader.ReadFrom(packet);
        Console.WriteLine();
        Console.WriteLine($"ЗАГОЛОВОК (43 байти)   файл {Path.GetFileName(path)}, {packet.Length} байт");
        Console.WriteLine($"  magic           {Convert.ToHexString(packet.AsSpan(0, 4))}   " +
                          $"{(PacketHeader.HasValidMagic(packet) ? "OK" : "НЕВІРНИЙ")}");
        Console.WriteLine($"  version         {packet[4]}");
        Console.WriteLine($"  device_id       {header.DeviceId}");
        Console.WriteLine($"  packet_seq      {header.PacketSequence}");
        Console.WriteLine($"  salt            {header.SaltHex}");
        Console.WriteLine($"  nonce           {header.NonceHex}");
        Console.WriteLine($"  ciphertext_len  {header.CiphertextLength}");

        var keys = SessionKeys.Derive(options.Value("password", context.Config.SessionPassword), header.Salt);
        var result = PacketReader.Read(packet, keys);

        Console.WriteLine();
        if (!result.IsAccepted)
        {
            Console.WriteLine($"ВІДХИЛЕНО: {result.Code}");
            Console.WriteLine();
            return 1;
        }

        var payload = result.Payload!;
        Console.WriteLine("HMAC OK (перевірено до розшифрування)");
        Console.WriteLine();
        Console.WriteLine($"НАВАНТАЖЕННЯ");
        Console.WriteLine($"  frame_count {payload.FrameCount}   flags 0x{(byte)payload.Flags:X2}   " +
                          $"table_nsym {payload.Table.SymbolCount}   symbol_count {payload.SymbolCount}   " +
                          $"bitstream_len {payload.Bitstream.Length}");

        if (payload.Table.SymbolCount > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  символ  довж  код");
            foreach (var entry in payload.Table.Entries.Take(options.Value("max-symbols", 16)))
                Console.WriteLine($"    0x{entry.Symbol:X2}    {entry.Length,2}   " +
                                  $"{Convert.ToString(payload.Table.CodeOf(entry.Symbol), 2).PadLeft(entry.Length, '0')}");
            if (payload.Table.SymbolCount > options.Value("max-symbols", 16))
                Console.WriteLine($"    … ще {payload.Table.SymbolCount - options.Value("max-symbols", 16)} символів");
        }

        Console.WriteLine();
        Console.WriteLine($"  bitstream  {Convert.ToHexString(payload.Bitstream.AsSpan(0, Math.Min(16, payload.Bitstream.Length)))}" +
                          (payload.Bitstream.Length > 16 ? " …" : ""));

        Console.WriteLine();
        Console.WriteLine("КАДРИ");
        Console.WriteLine($"  {"#",3}  {"timestamp",10}  {"altitude",8}  {"temp",6}  {"vibration",9}  {"mode",4}  critical");
        int limit = options.Value("max-frames", 10);
        for (int i = 0; i < Math.Min(limit, result.Frames.Count); i++)
        {
            var frame = result.Frames[i];
            Console.WriteLine($"  {i,3}  {frame.TimestampMs,10}  {frame.AltitudeDm,8}  {frame.TempC10,6}  " +
                              $"{frame.Vibration,9}  {frame.Mode,4}  {frame.Critical,8}");
        }
        if (result.Frames.Count > limit) Console.WriteLine($"  … ще {result.Frames.Count - limit} кадрів");

        Console.WriteLine();
        return 0;
    }

    private static int InspectSession(CommandContext context)
    {
        var session = context.LoadSession();

        Console.WriteLine();
        Console.WriteLine($"кадрів {session.Frames.Count}, критичних {session.CriticalCount}, " +
                          $"B_ref {session.ReferenceBits} біт");

        Console.WriteLine("розподіл mode — " + string.Join(", ",
            session.Frames.GroupBy(frame => frame.Mode).OrderBy(group => group.Key)
                .Select(group => $"mode {group.Key}: {group.Count()}")));

        if (session.Labels is not null)
        {
            var counts = session.Labels.GroupBy(l => l).OrderBy(g => g.Key)
                .Select(g => $"клас {g.Key}: {g.Count()} ({(double)g.Count() / session.Labels.Count:P1})");
            Console.WriteLine("розподіл класів — " + string.Join(", ", counts));
        }

        Console.WriteLine();
        Console.WriteLine($"  {"#",3}  {"timestamp",10}  {"altitude",8}  {"temp",6}  {"vibration",9}  {"mode",4}  critical");
        for (int i = 0; i < Math.Min(10, session.Frames.Count); i++)
        {
            var frame = session.Frames[i];
            Console.WriteLine($"  {i,3}  {frame.TimestampMs,10}  {frame.AltitudeDm,8}  {frame.TempC10,6}  " +
                              $"{frame.Vibration,9}  {frame.Mode,4}  {frame.Critical,8}");
        }

        Console.WriteLine();
        return 0;
    }
}

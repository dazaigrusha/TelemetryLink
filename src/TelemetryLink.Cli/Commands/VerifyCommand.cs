using System.Text.Json;
using TelemetryLink.Core.Format;
using TelemetryLink.Core.Security;

namespace TelemetryLink.Cli.Commands;

/// <summary>
/// Приймальні перевірки на еталонних пакетах викладача.
///
/// Це перевірка ЧУЖИМИ даними. Власний передавач і власний приймач можуть
/// мати однакову помилку і чудово розуміти один одного — саме тому формат
/// звіряється з пакетами, які згенерувала інша реалізація.
/// </summary>
public static class VerifyCommand
{
    public static int Execute(CommandLineOptions options)
    {
        var context = new CommandContext(options);
        string directory = context.ReferenceDirectory;
        string password = options.Value("password", context.Config.SessionPassword);

        if (!Directory.Exists(directory))
        {
            Console.Error.WriteLine($"Каталог {directory} не знайдено. Вкажіть --reference <шлях>.");
            return 2;
        }

        var keys = SessionKeys.Derive(password, LoadSalt(directory));
        int passed = 0, total = 0;

        Console.WriteLine();
        Console.WriteLine("еталонні пакети");

        foreach (string path in Directory.GetFiles(directory, "ref_*.bin").OrderBy(p => p))
        {
            total++;
            string name = Path.GetFileNameWithoutExtension(path);
            var result = PacketReader.Read(File.ReadAllBytes(path), keys);

            int? expectedFrames = ExpectedFrameCount(path);
            bool ok = result.IsAccepted && (expectedFrames is null || result.Frames.Count == expectedFrames);
            if (ok) passed++;

            string detail = result.IsAccepted
                ? $"{result.Frames.Count} кадрів, {result.Payload!.Table.SymbolCount} символів, " +
                  $"{new FileInfo(path).Length} байт"
                : $"ВІДХИЛЕНО: {result.Code}";
            Console.WriteLine($"  {(ok ? "OK  " : "FAIL")} {name,-26} {detail}");
        }

        Console.WriteLine();
        Console.WriteLine("пошкоджені пакети");

        var expectedFailures = LoadExpectedFailures(directory);
        foreach (string path in Directory.GetFiles(directory, "bad_*.bin").OrderBy(p => p))
        {
            total++;
            string fileName = Path.GetFileName(path);
            var result = PacketReader.Read(File.ReadAllBytes(path), keys);

            string expected = expectedFailures.GetValueOrDefault(fileName, "?");
            bool ok = result.Code.ToString() == expected;
            if (ok) passed++;

            Console.WriteLine($"  {(ok ? "OK  " : "FAIL")} {Path.GetFileNameWithoutExtension(path),-26} " +
                              $"{result.Code,-10} (очікувано {expected})");
        }

        Console.WriteLine();
        Console.WriteLine($"{passed}/{total} пройдено");
        Console.WriteLine();
        return passed == total ? 0 : 1;
    }

    /// <summary>Сіль однакова для всіх пакетів сеансу і лежить у відкритому заголовку.</summary>
    private static byte[] LoadSalt(string directory)
    {
        string anyPacket = Directory.GetFiles(directory, "ref_*.bin").OrderBy(p => p).First();
        return PacketHeader.ReadFrom(File.ReadAllBytes(anyPacket)).Salt;
    }

    private static int? ExpectedFrameCount(string binaryPath)
    {
        string jsonPath = Path.ChangeExtension(binaryPath, ".json");
        if (!File.Exists(jsonPath)) return null;

        using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
        return document.RootElement.TryGetProperty("frame_count", out var value) ? value.GetInt32() : null;
    }

    private static Dictionary<string, string> LoadExpectedFailures(string directory)
    {
        string path = Path.Combine(directory, "expected_failures.json");
        if (!File.Exists(path)) return [];

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.GetProperty("очікуваний_код").GetString() ?? "?");
    }
}

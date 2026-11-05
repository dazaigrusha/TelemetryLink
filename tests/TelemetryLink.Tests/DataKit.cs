using System.Text.Json;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.InputOutput;
using TelemetryLink.Core.Security;

namespace TelemetryLink.Tests;

/// <summary>
/// Доступ до комплекту викладача (каталог data/ у корені репозиторію).
///
/// Шлях шукається вгору від каталогу збірки, щоб тести працювали і з IDE,
/// і з `dotnet test`, і в CI без жодних налаштувань.
/// </summary>
public static class DataKit
{
    public static string Root { get; } = Locate();

    public static string Path(string relative) => System.IO.Path.Combine(Root, relative);
    public static string ReferencePath(string fileName) => Path(System.IO.Path.Combine("reference", fileName));

    private static TeamConfig? _config;
    public static TeamConfig Config => _config ??= TeamConfig.Load(Path("team_config.json"));

    public static SessionKeys Keys(string? password = null) =>
        SessionKeys.Derive(password ?? Config.SessionPassword, ReferenceSalt);

    /// <summary>Сіль однакова в усіх еталонних пакетах комплекту.</summary>
    public static byte[] ReferenceSalt { get; } =
        Convert.FromHexString("000102030405060708090A0B0C0D0E0F");

    public static TelemetrySession Session => TelemetryCsvReader.Read(Path("session.csv"));

    public static JsonDocument ReferenceJson(string fileName) =>
        JsonDocument.Parse(File.ReadAllText(ReferencePath(fileName)));

    private static string Locate()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = System.IO.Path.Combine(directory.FullName, "data");
            if (Directory.Exists(candidate) && File.Exists(System.IO.Path.Combine(candidate, "team_config.json")))
                return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Не знайдено каталог data/ із комплектом викладача. Розпакуйте його в корінь репозиторію.");
    }
}

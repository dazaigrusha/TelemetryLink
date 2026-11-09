using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using TelemetryLink.Cli;
using TelemetryLink.Cli.Commands;
using TelemetryLink.Core.InputOutput;

namespace TelemetryLink.Tests;

public class ComparisonCliTests
{
    public static IEnumerable<object[]> InvalidOptions => new[]
    {
        new[] { "compare", "--repeats", "0" }, new[] { "compare", "--repeats", "oops" },
        new[] { "compare", "--repeats" }, new[] { "compare", "--warmup", "-1" },
        new[] { "compare", "--seeds", "1,1" }, new[] { "compare", "--seeds", "1,x" },
        new[] { "compare", "--seeds", "1,", "--scenario", "calm" },
        new[] { "compare", "--seeds", "1", "--channel-seed", "2" },
        new[] { "compare", "--vary", "codec" }, new[] { "compare", "--vary", "codec=" },
        new[] { "compare", "--vary", "codec=none,none" }, new[] { "compare", "--vary", "wrong=a,b" },
        new[] { "compare", "--ablation", "--vary", "codec=none,huffman" },
        new[] { "compare", "--scenario", "calm,calm" }, new[] { "compare", "--scenario", ",calm" }
    }.Select(args => new object[] { args });

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void InvalidComparisonArguments_AreRejected(string[] args)
        => Assert.Throws<ArgumentException>(() => ComparisonSettings.Parse(new CommandLineOptions(args), 1));

    [Fact]
    public void ValidSettings_NormalizeNamesAndKeepExplicitSeeds()
    {
        var settings = ComparisonSettings.Parse(new CommandLineOptions(["compare", "--vary", "CODEC=NONE,HUFFMAN",
            "--scenario", "CALM", "--seeds", "7,8", "--repeats", "2", "--warmup", "0"]), 99);
        Assert.Equal("codec", settings.Dimension);
        Assert.Equal(new[] { "none", "huffman" }, settings.Variants);
        Assert.Equal(new[] { 7, 8 }, settings.Seeds);
        Assert.Equal(2, settings.Repeats);
    }

    [Fact]
    public void NumericOptions_DoNotSilentlyFallBackOnBadInput()
    {
        Assert.Throws<ArgumentException>(() => new CommandLineOptions(["run", "--window", "text"]).Value("window", 64));
        Assert.Throws<ArgumentException>(() => new CommandLineOptions(["run", "--threshold", "NaN"]).Value("threshold", 0.5));
        Assert.Throws<ArgumentException>(() => new CommandLineOptions(["run", "--window"]).Value("window", 64));
    }

    private static void WithDirectory(Action<string> action)
    {
        string directory = Path.Combine(Path.GetTempPath(), "tlm-report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(directory); }
        finally { Directory.Delete(directory, true); }
    }

    private static string Input(string directory)
    {
        string path = Path.Combine(directory, "session.bin");
        TelemetryBinaryReader.Write(path, DataKit.Session.Frames.Take(3).ToArray());
        return path;
    }

    private static CommandLineOptions Args(string directory, string session, params string[] extra)
        => new(new[] { "compare", "--config", DataKit.Path("team_config.json"), "--session", session,
            "--scenario", "calm", "--out", Path.Combine(directory, "result.csv"), "--warmup", "0", "--repeats", "2" }
            .Concat(extra).ToArray());

    [Fact]
    public void Compare_WritesAllRunsSummaryAndReproducibilityManifest()
        => WithDirectory(directory =>
        {
            string session = Input(directory);
            int code = CompareCommand.Execute(Args(directory, session, "--vary", "codec=none,huffman", "--seeds", "7,8"));
            Assert.Equal(0, code);
            string[] raw = File.ReadAllLines(Path.Combine(directory, "result.csv"));
            Assert.Equal(9, raw.Length); // 2 кодеки * 2 seed * 2 повтори + заголовок
            int columns = raw[0].Split(',').Length;
            Assert.All(raw.Skip(1), row => Assert.Equal(columns, row.Split(',').Length));
            Assert.Contains("exact_restore", raw[0]);
            Assert.Contains("receiver_crashed", raw[0]);
            Assert.Equal(3, File.ReadAllLines(Path.Combine(directory, "result.summary.csv")).Length);
            string json = File.ReadAllText(Path.Combine(directory, "result.manifest.json"));
            using var document = JsonDocument.Parse(json);
            Assert.Equal(8, document.RootElement.GetProperty("rows").GetInt32());
            Assert.Equal(8, document.RootElement.GetProperty("measuredRuns").GetInt32());
            Assert.False(document.RootElement.GetProperty("protocol").GetProperty("warmupIncludedInStatistics").GetBoolean());
            string hash = document.RootElement.GetProperty("inputs").GetProperty("session").GetProperty("sha256").GetString()!;
            Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(session))).ToLowerInvariant(), hash);
            Assert.DoesNotContain(DataKit.Config.SessionPassword, json);
        });

    [Fact]
    public void Ablation_MajorityAlreadySelected_IsExplicitlyMarkedAsReused()
        => WithDirectory(directory =>
        {
            Assert.Equal(0, CompareCommand.Execute(Args(directory, Input(directory), "--ablation", "--repeats", "1")));
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "result.manifest.json")));
            Assert.Equal(5, doc.RootElement.GetProperty("rows").GetInt32());
            Assert.Equal(4, doc.RootElement.GetProperty("measuredRuns").GetInt32());
            var alias = doc.RootElement.GetProperty("variants").EnumerateArray()
                .Single(v => v.GetProperty("name").GetString() == "majority-classifier");
            Assert.True(alias.GetProperty("reuseBaseline").GetBoolean());
            string[] rows = File.ReadAllLines(Path.Combine(directory, "result.summary.csv"));
            string[] fields = rows.Single(row => row.Contains(",majority-classifier,")).Split(',');
            Assert.Equal("0", fields[3]); // measured_runs
            Assert.Equal("0", fields[9]); // paired delta
        });

    [Fact]
    public void UnknownVariant_IsRejectedBeforeAnyResultFileIsCreated()
        => WithDirectory(directory =>
        {
            Assert.Throws<ArgumentException>(() => CompareCommand.Execute(Args(directory, Input(directory),
                "--vary", "codec=none,unknown")));
            Assert.False(File.Exists(Path.Combine(directory, "result.csv")));
        });

    [Fact]
    public void OutputCannotOverwriteSessionCsv()
        => WithDirectory(directory =>
        {
            string source = Path.Combine(directory, "session.csv");
            string contents = "timestamp_ms,altitude_dm,temp_c10,vibration,mode,critical\n0,1000,215,-3,1,1\n";
            File.WriteAllText(source, contents);
            Assert.Throws<ArgumentException>(() => CompareCommand.Execute(Args(directory, source,
                "--vary", "codec=none", "--out", source)));
            Assert.Equal(contents, File.ReadAllText(source));
        });

    [Fact]
    public void StrictMode_SignalsThresholdFailure_WithoutDroppingRows()
        => WithDirectory(directory =>
        {
            string session = Input(directory);
            var config = JsonNode.Parse(File.ReadAllText(DataKit.Path("team_config.json")))!;
            config["канал"]!["сценарії"]!["спокійний"]!["transmission_budget_s"] = 0;
            string configPath = Path.Combine(directory, "config.json"); File.WriteAllText(configPath, config.ToJsonString());
            Assert.Equal(0, CompareCommand.Execute(Args(directory, session, "--config", configPath,
                "--vary", "codec=none", "--repeats", "1")));
            Assert.Equal(1, CompareCommand.Execute(Args(directory, session, "--config", configPath,
                "--vary", "codec=none", "--repeats", "1", "--strict")));
            Assert.Equal(2, File.ReadAllLines(Path.Combine(directory, "result.csv")).Length);
        });

    [Fact]
    public void SingleRunCsv_IncludesSeedTuningAndIntegrityFlags()
        => WithDirectory(directory =>
        {
            string path = Path.Combine(directory, "run.csv");
            int code = RunCommand.Execute(new CommandLineOptions(["run", "--config", DataKit.Path("team_config.json"),
                "--session", Input(directory), "--scenario", "calm", "--channel-seed", "17", "--out", path]));
            Assert.Equal(0, code);
            string[] lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
            string[] header = lines[0].Split(','), values = lines[1].Split(',');
            Assert.Equal(header.Length, values.Length);
            var fields = header.Select((name, index) => (name, index)).ToDictionary(x => x.name, x => values[x.index]);
            Assert.Equal("17", fields["channel_seed"]);
            Assert.Equal("64", fields["window"]);
            Assert.Equal("true", fields["exact_restore"]);
            Assert.Equal("false", fields["receiver_crashed"]);
        });

    [Fact]
    public void CsvEscapesText_AndAlwaysUsesInvariantDecimals()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("uk-UA");
            Assert.Equal("\"one,two\",\"a\"\"b\",\"line\nend\",0.5,true",
                ExperimentExporter.CsvRow("one,two", "a\"b", "line\nend", 0.5, true));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}

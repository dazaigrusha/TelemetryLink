using System.Diagnostics;
using TelemetryLink.Core;
using TelemetryLink.Core.Compression;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Format;
using TelemetryLink.Core.InputOutput;
using TelemetryLink.Core.Metrics;
using TelemetryLink.Core.Packing;
using TelemetryLink.Core.Pipeline;
using TelemetryLink.Core.Prediction;
using TelemetryLink.Core.Scheduling;
using TelemetryLink.Core.Security;

namespace TelemetryLink.Tests;

/// <summary>
/// Дванадцять базових приймальних кейсів критерію C1 (умова, п. 9.1).
///
/// Рівень критерію = пройдені кейси / 12. Кожен кейс — окремий тест, щоб було
/// видно, які саме пройдено. Це ті самі перевірки, які виконує викладач.
/// </summary>
public class AcceptanceCaseTests
{
    private static PipelineOptions Pipeline(
        string codec = "huffman", string packer = "dp", string scheduler = "greedy",
        IFrameClassifier? classifier = null, Tuning? tuning = null) => new()
    {
        Codec = AlgorithmCatalog.Codec(codec),
        Packer = AlgorithmCatalog.Packer(packer),
        Scheduler = AlgorithmCatalog.Scheduler(scheduler),
        Classifier = classifier ?? new MajorityClassifier(),
        Tuning = tuning ?? new Tuning(DataKit.Config.CandidateWindowFrames,
                                      Tuning.Default.CompressionThreshold,
                                      Tuning.Default.CriticalWeight)
    };

    private static SessionReport Run(string scenario, PipelineOptions? options = null, TelemetrySession? session = null)
        => new SessionRunner(DataKit.Config, options ?? Pipeline())
            .Run(session ?? DataKit.Session, DataKit.Config.Scenario(scenario), DataKit.Config.ChannelSeed);

    /// <summary>Кейс 1. Потік із одного кадру проходить і відновлюється побайтово.</summary>
    [Fact]
    public void Case01_SingleFrameStream_RestoredByteForByte()
    {
        var frames = new[] { new TelemetryFrame(0, 1000, 215, -3, 1, 1) };
        var report = Run("calm", session: new TelemetrySession(frames, null));

        Assert.True(report.ExactRestore);
        Assert.Equal(1, report.RecoveredFrameCount);
    }

    /// <summary>Кейс 2. Порожній потік не спричиняє аварійного завершення.</summary>
    [Fact]
    public void Case02_EmptyStream_DoesNotCrash()
    {
        var report = Run("calm", session: new TelemetrySession([], null));

        Assert.Equal(0, report.FrameCount);
        Assert.True(report.ExactRestore);
    }

    /// <summary>Кейс 3. Спокійний сценарій: усі кадри відновлено точно.</summary>
    [Fact]
    public void Case03_CalmScenario_RestoresEveryFrame()
    {
        var report = Run("calm");

        Assert.Equal(report.FrameCount, report.RecoveredFrameCount);
        Assert.Equal(1.0, report.R);
        Assert.True(report.ExactRestore);
    }

    /// <summary>Кейс 4. Робочий сценарій: втрата критичних кадрів не перевищує поріг викладача.</summary>
    [Fact]
    public void Case04_WorkingScenario_CriticalLossWithinThreshold()
    {
        var report = Run("working");
        double threshold = DataKit.Config.CriticalLossThreshold("working");

        Assert.True(report.CriticalLossRatio <= threshold,
            $"втрачено {report.CriticalLossRatio:P1} критичних кадрів, поріг {threshold:P1}");
    }

    /// <summary>
    /// Кейс 5. Аварійний сценарій: система працює без аварійного завершення,
    /// Q обчислено. Порогу втрати критичних кадрів цей кейс НЕ вимагає.
    /// </summary>
    [Fact]
    public void Case05_EmergencyScenario_CompletesAndComputesQ()
    {
        var report = Run("emergency");

        Assert.InRange(report.Q, 0.0, 1.0);
        Assert.True(report.Channel.OverBudget > 0, "бюджет мав обмежити передавання");
        Assert.True(report.ExactRestore);
    }

    /// <summary>Кейс 6. Дерево відновлюється з канонічних довжин за правилами FORMAT_SPEC.</summary>
    [Theory]
    [InlineData("ref_01_single_frame")]
    [InlineData("ref_04_session")]
    public void Case06_CanonicalCodes_RebuiltFromLengths(string name)
    {
        byte[] packet = File.ReadAllBytes(DataKit.ReferencePath($"{name}.bin"));
        var result = PacketReader.Read(packet, DataKit.Keys());
        Assert.Equal(RejectionCode.None, result.Code);

        using var expected = DataKit.ReferenceJson($"{name}.json");
        var table = result.Payload!.Table;

        foreach (var entry in expected.RootElement.GetProperty("таблиця_кодів").EnumerateArray())
        {
            byte symbol = (byte)entry.GetProperty("символ").GetInt32();
            Assert.Equal(entry.GetProperty("код").GetString(),
                Convert.ToString(table.CodeOf(symbol), 2).PadLeft(table.LengthOf(symbol), '0'));
        }
    }

    /// <summary>Кейс 7. Вироджений алфавіт (один символ) кодується коректно.</summary>
    [Fact]
    public void Case07_DegenerateAlphabet_EncodesCorrectly()
    {
        var codec = new HuffmanCodec();
        byte[] data = Enumerable.Repeat((byte)0x42, 96).ToArray();

        var table = codec.BuildTable(data);
        byte[] bitstream = codec.Encode(data, table);

        Assert.Equal(1, table.LengthOf(0x42));
        Assert.Equal(RejectionCode.None, codec.Decode(bitstream, table, data.Length, out byte[] decoded));
        Assert.Equal(data, decoded);
    }

    /// <summary>Кейс 8. ДП-рюкзак дає цінність не меншу за жадібний на всіх наборах.</summary>
    [Fact]
    public void Case08_DynamicProgramming_NeverWorseThanGreedy()
    {
        var dp = new DynamicProgrammingPacker();
        var greedy = new GreedyRatioPacker();
        var random = new Random(2026);

        for (int instance = 0; instance < 500; instance++)
        {
            var candidates = Enumerable.Range(0, random.Next(1, 64))
                .Select(i => new FrameCandidate(i, random.Next(30, 140), random.Next(1, 11)))
                .ToArray();
            int capacity = random.Next(0, 1400);

            int dpValue = dp.Select(candidates, capacity).Sum(i => candidates[i].Value);
            int greedyValue = greedy.Select(candidates, capacity).Sum(i => candidates[i].Value);

            Assert.True(dpValue >= greedyValue, $"набір {instance}: ДП {dpValue} < жадібний {greedyValue}");
        }
    }

    /// <summary>
    /// Кейс 9. Класифікатор призначає клас усім кадрам, macro-F1 ≥ F1_min.
    ///
    /// Прихованої вибірки holdout.csv у команди немає і не буде — підсумковий
    /// macro-F1 рахує викладач. Тут перевіряється відкритий valid.csv.
    /// </summary>
    [Fact]
    public void Case09_Classifier_AssignsClassToEveryFrame_AndExceedsF1Minimum()
    {
        var train = TelemetryCsvReader.Read(DataKit.Path("train.csv"));
        var validation = TelemetryCsvReader.Read(DataKit.Path("valid.csv"));

        var model = new PerceptronTrainer().Train(train.Frames, train.Labels!, new TrainingOptions(Epochs: 8));
        var classifier = new PerceptronClassifier(model);
        var predicted = classifier.Classify(validation.Frames);

        Assert.Equal(validation.Frames.Count, predicted.Length);
        Assert.All(predicted, c => Assert.InRange((int)c, 0, 2));

        double macroF1 = new ClassificationReport(validation.Labels!, predicted.Select(c => (int)c).ToArray()).MacroF1;
        Assert.True(macroF1 >= DataKit.Config.MinimumMacroF1, $"macro-F1 = {macroF1:F4}");
    }

    /// <summary>Кейс 10. Пакет зі зміненим байтом і пакет зі зміненим HMAC відхиляються.</summary>
    [Theory]
    [InlineData("bad_01_tampered_byte.bin")]
    [InlineData("bad_02_tampered_tag.bin")]
    public void Case10_TamperedPacket_IsRejected(string fileName)
    {
        var result = PacketReader.Read(File.ReadAllBytes(DataKit.ReferencePath(fileName)), DataKit.Keys());

        Assert.Equal(RejectionCode.E_HMAC, result.Code);
    }

    /// <summary>
    /// Кейс 11. Невірний пароль дає відмову, а не спотворені дані;
    /// обрізаний пакет відхиляється без аварійного завершення.
    /// </summary>
    [Fact]
    public void Case11_WrongPasswordAndTruncatedPacket_AreRejectedGracefully()
    {
        byte[] packet = File.ReadAllBytes(DataKit.ReferencePath("ref_04_session.bin"));

        var wrongPassword = PacketReader.Read(packet, DataKit.Keys("неправильний"));
        Assert.Equal(RejectionCode.E_HMAC, wrongPassword.Code);
        Assert.Empty(wrongPassword.Frames);

        var truncated = PacketReader.Read(File.ReadAllBytes(DataKit.ReferencePath("bad_03_truncated.bin")), DataKit.Keys());
        Assert.Equal(RejectionCode.E_LENGTH, truncated.Code);
    }

    /// <summary>
    /// Кейс 12. Еталонні пакети приймаються; повний прогін на трьох сценаріях
    /// укладається в ліміт часу з team_config.json.
    /// </summary>
    [Fact]
    public void Case12_AllReferencePacketsAccepted_AndFullRunFitsTimeLimit()
    {
        foreach (string path in Directory.GetFiles(DataKit.Path("reference"), "ref_*.bin"))
            Assert.Equal(RejectionCode.None, PacketReader.Read(File.ReadAllBytes(path), DataKit.Keys()).Code);

        var session = DataKit.Session;
        var stopwatch = Stopwatch.StartNew();
        foreach (string scenario in new[] { "calm", "working", "emergency" })
            Run(scenario, session: session);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed.TotalSeconds < DataKit.Config.FullRunTimeLimitSeconds,
            $"повний прогін {stopwatch.Elapsed.TotalSeconds:F1} c, ліміт {DataKit.Config.FullRunTimeLimitSeconds} c");
    }

    // --- Крайні випадки, критерій C2 (5 пунктів по 1 балу) ---

    /// <summary>C2 §1. Валідація вхідної телеметрії: недопустимі значення відхиляються.</summary>
    [Fact]
    public void EdgeCase1_InvalidTelemetryIsRejectedOnLoad()
    {
        string path = Path.Combine(Path.GetTempPath(), $"bad_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "timestamp_ms,altitude_dm,temp_c10,vibration,mode,critical\n0,0,0,0,9,0\n");

        try
        {
            Assert.Throws<InvalidDataException>(() => TelemetryCsvReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>C2 §2. Вироджений алфавіт і рівноймовірні символи.</summary>
    [Fact]
    public void EdgeCase2_DegenerateAndUniformAlphabets()
    {
        var codec = new HuffmanCodec();

        Assert.Equal(1, codec.BuildTable(Enumerable.Repeat((byte)7, 50).ToArray()).SymbolCount);
        Assert.All(codec.BuildTable(Enumerable.Range(0, 256).Select(i => (byte)i).ToArray()).Entries,
                   entry => Assert.Equal(8, entry.Length));
    }

    /// <summary>C2 §3. Повний прогін не залишає необроблених винятків.</summary>
    [Fact]
    public void EdgeCase3_FullRunRaisesNoUnhandledException()
    {
        var exception = Record.Exception(() =>
        {
            foreach (string scenario in new[] { "calm", "working", "emergency" })
                foreach (string codec in AlgorithmCatalog.CodecNames)
                    Run(scenario, Pipeline(codec: codec));
        });

        Assert.Null(exception);
    }

    /// <summary>C2 §4. Межові значення: нульова місткість і кадр, більший за пакет.</summary>
    [Fact]
    public void EdgeCase4_ZeroCapacityAndOversizedFrame()
    {
        var packer = new DynamicProgrammingPacker();

        Assert.Empty(packer.Select([new FrameCandidate(0, 96, 1)], 0));
        Assert.Empty(packer.Select([new FrameCandidate(0, 100_000, 1)], 3384));
    }

    /// <summary>C2 §5. Свідома відмова від стиснення працює наскрізно.</summary>
    [Fact]
    public void EdgeCase5_ExplicitNoCompressionModeRoundTrips()
    {
        var report = Run("calm", Pipeline(codec: "none"));

        Assert.True(report.ExactRestore);
        Assert.Equal(report.FrameCount, report.RecoveredFrameCount);
    }
}

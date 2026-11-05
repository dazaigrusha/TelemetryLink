using TelemetryLink.Core.Domain;
using TelemetryLink.Core.InputOutput;

namespace TelemetryLink.Tests;

public class TelemetryReaderTests
{
    private const string Header = "timestamp_ms,altitude_dm,temp_c10,vibration,mode,critical,label\n";

    // Кожен тест створює власний файл і видаляє його навіть після помилки.
    private static void WithCsv(string contents, Action<string> check)
    {
        string path = Path.GetTempFileName();
        try { File.WriteAllText(path, contents); check(path); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Csv_ValidRow_RestoresFrameAndLabel()
        => WithCsv(Header + "54,1138,229,-3,4,1,0\n", path =>
        {
            var session = TelemetryCsvReader.Read(path);
            Assert.Equal(new TelemetryFrame(54, 1138, 229, -3, 4, 1), Assert.Single(session.Frames));
            Assert.Equal(0, Assert.Single(session.Labels!));
            Assert.Equal(96L, session.ReferenceBits);
        });

    [Theory]
    [InlineData("54,1138,229,-3,8,1,0")]
    [InlineData("54,1138,229,-3,4,2,0")]
    [InlineData("54,1138,229,-3,4,1,3")]
    [InlineData("text,1138,229,-3,4,1,0")]
    [InlineData("54,40000,229,-3,4,1,0")]
    [InlineData("54,1138,229,-3,4,1")]
    public void Csv_InvalidRow_ReturnsLineNumber(string row)
        => WithCsv(Header + row, path =>
            Assert.Contains("рядок 2", Assert.Throws<InvalidDataException>(
                () => TelemetryCsvReader.Read(path)).Message));

    [Fact]
    public void Csv_WrongHeader_IsRejected()
        => WithCsv("wrong,header\n", path =>
            Assert.Throws<InvalidDataException>(() => TelemetryCsvReader.Read(path)));

    [Fact]
    public void Binary_InvalidMode_IsRejected()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, TelemetryFrame.Serialize(new[] { new TelemetryFrame(0, 0, 0, 0, 8, 0) }));
            Assert.Throws<InvalidDataException>(() => TelemetryBinaryReader.Read(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Binary_WriteThenRead_RestoresFrames()
    {
        string path = Path.GetTempFileName();
        try
        {
            var frame = new TelemetryFrame(54, 1138, 229, -3, 4, 1);
            TelemetryBinaryReader.Write(path, new[] { frame });
            Assert.Equal(frame, Assert.Single(TelemetryBinaryReader.Read(path).Frames));
        }
        finally { File.Delete(path); }
    }
}

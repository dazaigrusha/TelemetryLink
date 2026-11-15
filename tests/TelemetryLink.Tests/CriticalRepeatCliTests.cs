using System.Text.Json;
using TelemetryLink.Cli;
using TelemetryLink.Cli.Commands;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.InputOutput;
namespace TelemetryLink.Tests;
public class CriticalRepeatCliTests
{
    [Fact] public void CompareExportsRepeatCostsAndUniqueRecovery()
    {
        string directory=Path.Combine(Path.GetTempPath(),"tlm-repeat-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            string input=Path.Combine(directory,"session.bin"),output=Path.Combine(directory,"repeat.csv");
            TelemetryBinaryReader.Write(input,Enumerable.Range(0,8).Select(i=>new TelemetryFrame((uint)i,0,0,0,0,1)).ToArray());
            Assert.Equal(0,CompareCommand.Execute(new CommandLineOptions(["compare","--config",DataKit.Path("team_config.json"),"--session",input,
                "--vary","delivery=critical-first,critical-repeat","--scenario","calm","--repeats","1","--warmup","0","--codec","none",
                "--critical-copies","2","--repeat-gap","1","--out",output])));
            var lines=File.ReadAllLines(output);Assert.Equal(3,lines.Length);var header=lines[0].Split(',');
            var a=header.Zip(lines[1].Split(',')).ToDictionary(x=>x.First,x=>x.Second);
            var b=header.Zip(lines[2].Split(',')).ToDictionary(x=>x.First,x=>x.Second);
            Assert.Equal("8",b["recovered_frames"]);Assert.Equal("8",b["recovered_critical"]);
            Assert.Equal(b["value"],b["delivered_value"]);Assert.Equal("1",b["duplicate_accepted_packets"]);
            Assert.Equal(2*long.Parse(a["sent_bits"]),long.Parse(b["sent_bits"]));Assert.Equal(a["sent_bits"],b["repeated_sent_bits"]);
            Assert.Equal("1",a["critical_copies"]);Assert.Equal("2",b["critical_copies"]);
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"repeat.manifest.json")));
            Assert.Equal(2,document.RootElement.GetProperty("variants")[1].GetProperty("criticalCopies").GetInt32());
            Assert.Equal(1,document.RootElement.GetProperty("variants")[1].GetProperty("repeatGapPackets").GetInt32());
        }finally{Directory.Delete(directory,true);}
    }
}

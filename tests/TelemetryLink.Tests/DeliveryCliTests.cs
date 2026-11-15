using System.Text.Json;
using TelemetryLink.Cli;
using TelemetryLink.Cli.Commands;
using TelemetryLink.Core.Pipeline;
using TelemetryLink.Core.InputOutput;
namespace TelemetryLink.Tests;
public class DeliveryCliTests
{
    [Fact] public void DeliveryChoiceIsExplicitAndRejectsTypos()
    {
        CommandContext Context(params string[] args)=>new(new CommandLineOptions(new[]{"run","--config",DataKit.Path("team_config.json")}.Concat(args).ToArray()));
        Assert.Equal(DeliveryPolicy.Balanced,Context().BuildPipeline().Delivery);
        Assert.Equal(DeliveryPolicy.CriticalFirst,Context("--delivery","critical-first").BuildPipeline().Delivery);
        Assert.Throws<ArgumentException>(()=>Context("--delivery","unknown").BuildPipeline());
        Assert.Throws<ArgumentException>(()=>Context("--delivery").BuildPipeline());
        var settings=ComparisonSettings.Parse(new CommandLineOptions(["compare","--vary","delivery=balanced,critical-first"]),1);
        Assert.Equal("delivery",settings.Dimension);Assert.Equal(2,settings.Variants.Length);
    }
    [Fact] public void CompareExportsDiagnosticCountsAndPolicyInManifest()
    {
        string directory=Path.Combine(Path.GetTempPath(),"tlm-delivery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            string input=Path.Combine(directory,"session.bin"),output=Path.Combine(directory,"delivery.csv");
            TelemetryBinaryReader.Write(input,DataKit.Session.Frames.Take(40).ToArray());
            Assert.Equal(0,CompareCommand.Execute(new CommandLineOptions(["compare","--config",DataKit.Path("team_config.json"),
                "--session",input,"--scenario","calm","--vary","delivery=balanced,critical-first","--repeats","1","--warmup","0","--out",output])));
            string[] lines=File.ReadAllLines(output);Assert.Equal(3,lines.Length);
            var header=lines[0].Split(',');
            foreach(string line in lines.Skip(1))
            {
                var fields=header.Zip(line.Split(',')).ToDictionary(x=>x.First,x=>x.Second);
                Assert.Equal(fields["variant"],fields["delivery"]);
                int accounted=int.Parse(fields["recovered_critical"])+new[]{"critical_over_budget","critical_channel_lost","critical_receiver_rejected","critical_receiver_failed","critical_content_mismatch","critical_not_packed"}.Sum(k=>int.Parse(fields[k]));
                Assert.Equal(int.Parse(fields["critical"]),accounted);Assert.Equal("0",fields["critical_over_budget"]);
            }
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"delivery.manifest.json")));
            Assert.Equal(new[]{"balanced","critical-first"},doc.RootElement.GetProperty("variants").EnumerateArray().Select(x=>x.GetProperty("delivery").GetString()).ToArray());
            string run=Path.Combine(directory,"run.csv");
            Assert.Equal(0,RunCommand.Execute(new CommandLineOptions(["run","--config",DataKit.Path("team_config.json"),"--session",input,"--scenario","calm","--delivery","critical-first","--out",run])));
            Assert.Contains("critical_channel_lost",File.ReadLines(run).First());
        }finally{Directory.Delete(directory,true);}
    }
}

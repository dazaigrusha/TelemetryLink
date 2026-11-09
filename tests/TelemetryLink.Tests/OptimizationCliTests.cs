using System.Text.Json;
using TelemetryLink.Cli;
using TelemetryLink.Cli.Commands;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.InputOutput;
namespace TelemetryLink.Tests;
public class OptimizationCliTests
{
    [Fact] public void ReportsContainEqualBudgetsAndReusableBestParameters()
    {
        string dir=Path.Combine(Path.GetTempPath(),"tlm-opt-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try
        {
            string session=Path.Combine(dir,"session.bin"),output=Path.Combine(dir,"opt.csv");
            TelemetryBinaryReader.Write(session,DataKit.Session.Frames.Take(3).ToArray());
            Assert.Equal(0,OptimizeCommand.Execute(new CommandLineOptions(["optimize","--config",DataKit.Path("team_config.json"),
                "--session",session,"--budget","3","--optimizer","random,ga","--out",output])));
            using var report=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"opt.summary.json")));
            Assert.Equal(3,report.RootElement.GetProperty("BudgetPerOptimizer").GetInt32());
            var results=report.RootElement.GetProperty("Results").EnumerateArray().ToArray();Assert.Equal(2,results.Length);
            Assert.All(results,x=>Assert.Equal(3,x.GetProperty("Evaluations").GetInt32()));
            Assert.Equal(5,File.ReadAllLines(output).Length);
            foreach(string name in new[]{"ga","random"})
            {
                string path=Path.Combine(dir,"opt."+name+".best.json");
                var tuning=JsonSerializer.Deserialize<Tuning>(File.ReadAllText(path));
                var context=new CommandContext(new CommandLineOptions(["run","--config",DataKit.Path("team_config.json"),"--tuning",path]));
                Assert.Equal(tuning,context.CurrentTuning);
                var overridden=new CommandContext(new CommandLineOptions(["run","--config",DataKit.Path("team_config.json"),"--tuning",path,"--window","32"]));
                Assert.Equal(32,overridden.CurrentTuning.WindowFrames);
            }
        }
        finally{Directory.Delete(dir,true);}
    }
    [Theory]
    [InlineData("--budget","0")] [InlineData("--optimizer","ga,ga")] [InlineData("--optimizer","unknown")]
    [InlineData("--population","0")] [InlineData("--mutation","2")] [InlineData("--out","input.bin")]
    public void InvalidOptionsRejectedBeforeSessionLoaded(string flag,string value)
        =>Assert.Throws<ArgumentException>(()=>OptimizeCommand.Execute(new CommandLineOptions([
            "optimize","--config",DataKit.Path("team_config.json"),"--session","missing.bin",flag,value])));
    [Fact] public void InputsCannotBeOverwrittenByHistoryOrBestFile()
    {
        string output=Path.Combine(Path.GetTempPath(),"tlm-protected.csv");
        foreach(string input in new[]{output,output[..^4]+".ga.best.json",output[..^4]+".summary.json"})
            Assert.Throws<ArgumentException>(()=>OptimizeCommand.Execute(new CommandLineOptions([
                "optimize","--config",DataKit.Path("team_config.json"),"--session",input,"--out",output])));
    }
}

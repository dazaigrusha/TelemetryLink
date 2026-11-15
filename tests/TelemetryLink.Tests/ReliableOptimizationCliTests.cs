using System.Text.Json;
using TelemetryLink.Cli;
using TelemetryLink.Cli.Commands;
using TelemetryLink.Core.InputOutput;
namespace TelemetryLink.Tests;
public class ReliableOptimizationCliTests
{
    [Theory]
    [InlineData("--search-seeds","1,1")] [InlineData("--search-seeds","1,01")] [InlineData("--search-seeds","1,x")]
    [InlineData("--search-seeds","1,")] [InlineData("--fitness-repeats","0")] [InlineData("--fitness-repeats","11")]
    [InlineData("--channel-seed","1")] [InlineData("--seeds","1")]
    public void InvalidSettingsRejected(string flag,string value)=>Assert.Throws<ArgumentException>(()=>ReliableOptimizationSettings.Parse(new CommandLineOptions(["optimize",flag,value])));
    [Fact] public void SearchAndValidationCannotOverlap()
        =>Assert.Throws<ArgumentException>(()=>ReliableOptimizationSettings.Parse(new CommandLineOptions(["optimize","--search-seeds","1,2","--validation-seeds","2,3"])));
    [Fact] public void DefaultsUseSeparateGridsAndAllScenarios()
    {
        var settings=ReliableOptimizationSettings.Parse(new CommandLineOptions(["optimize"]));
        Assert.Empty(settings.SearchSeeds.Intersect(settings.ValidationSeeds));Assert.Equal(9,settings.SearchRunsPerCandidate);Assert.Equal(15,settings.ValidationRunsPerCandidate);
    }
    [Fact] public void ReliableCommandExportsEveryRunAndReusableTuning()
    {
        string dir=Path.Combine(Path.GetTempPath(),"tlm-reliable-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try
        {
            string input=Path.Combine(dir,"session.bin"),output=Path.Combine(dir,"opt.csv");
            TelemetryBinaryReader.Write(input,DataKit.Session.Frames.Take(3).ToArray());
            Assert.Equal(0,OptimizeCommand.Execute(new CommandLineOptions(["optimize","--objective","reliable","--config",DataKit.Path("team_config.json"),
                "--session",input,"--scenario","calm","--search-seeds","1,2","--validation-seeds","3","--budget","2","--optimizer","random,ga","--strict","--out",output])));
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"opt.summary.json")));
            var root=doc.RootElement;Assert.False(root.GetProperty("Protocol").GetProperty("ValidationUsedForSelection").GetBoolean());
            Assert.Equal(4,root.GetProperty("Protocol").GetProperty("FitnessRunsPerOptimizer").GetInt64());
            Assert.Equal(3,root.GetProperty("Protocol").GetProperty("ValidationRuns").GetInt64());
            Assert.All(root.GetProperty("Results").EnumerateArray(),x=>{Assert.True(x.GetProperty("PassedValidation").GetBoolean());Assert.Equal(2,x.GetProperty("Evaluations").GetInt32());});
            Assert.Equal(14,File.ReadAllLines(Path.Combine(dir,"opt.runs.csv")).Length); // baseline2 + search8 + validation3 + header
            Assert.Equal(9,File.ReadAllLines(Path.Combine(dir,"opt.candidates.csv")).Length);
            var context=new CommandContext(new CommandLineOptions(["run","--config",DataKit.Path("team_config.json"),"--tuning",Path.Combine(dir,"opt.ga.best.json")]));
            Assert.NotNull(context.CurrentTuning);
        }finally{Directory.Delete(dir,true);}
    }
    [Fact] public void NewOutputFilesAreProtectedAgainstInputOverwrite()
    {
        string output=Path.Combine(Path.GetTempPath(),"tlm-reliable-protected.csv");
        foreach(string suffix in new[]{".candidates.csv",".runs.csv",".summary.json",".random.best.json"})
            Assert.Throws<ArgumentException>(()=>OptimizeCommand.Execute(new CommandLineOptions(["optimize","--objective","reliable","--config",DataKit.Path("team_config.json"),"--session",output[..^4]+suffix,"--out",output])));
    }
    [Fact] public void ReliabilityFlagsCannotBeSilentlyIgnoredByLegacyMode()
    {
        foreach(string flag in new[]{"--search-seeds","--validation-seeds","--fitness-repeats"})
            Assert.Throws<ArgumentException>(()=>OptimizeCommand.Execute(new CommandLineOptions(["optimize",flag,"1"])));
    }
    [Fact] public void StrictReturnsFailureAndLabelsInfeasibleBestHonestly()
    {
        string dir=Path.Combine(Path.GetTempPath(),"tlm-infeasible-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try
        {
            string config=Path.Combine(dir,"config.json"),input=Path.Combine(dir,"session.bin"),output=Path.Combine(dir,"opt.csv");
            var node=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(DataKit.Path("team_config.json")))!;
            node["канал"]!["сценарії"]!["аварійний"]!["transmission_budget_s"]=0;
            File.WriteAllText(config,node.ToJsonString());
            TelemetryBinaryReader.Write(input,[new(0,0,0,0,0,1)]);
            Assert.Equal(1,OptimizeCommand.Execute(new CommandLineOptions(["optimize","--objective","reliable","--config",config,"--session",input,
                "--scenario","emergency","--search-seeds","1","--validation-seeds","2","--budget","1","--optimizer","random","--strict","--out",output])));
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"opt.summary.json")));
            Assert.False(doc.RootElement.GetProperty("Results")[0].GetProperty("FoundFeasibleOnSearch").GetBoolean());
            Assert.False(doc.RootElement.GetProperty("Results")[0].GetProperty("PassedValidation").GetBoolean());
            Assert.True(File.Exists(Path.Combine(dir,"opt.random.best.json")));
        }finally{Directory.Delete(dir,true);}
    }
    [Fact] public void UnknownObjectiveRejected()
        =>Assert.Throws<ArgumentException>(()=>OptimizeCommand.Execute(new CommandLineOptions(["optimize","--objective","oops"])));
}

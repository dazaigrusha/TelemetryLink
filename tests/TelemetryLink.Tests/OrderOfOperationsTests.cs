using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Metrics;
using TelemetryLink.Cli;
using TelemetryLink.Cli.Commands;
namespace TelemetryLink.Tests;
public class OrderOfOperationsTests
{
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(64)]
    public void BothOrdersRestoreAndAccountForTableAndPadding(int count)
    {
        var frames=Enumerable.Range(0,count).Select(i=>new TelemetryFrame((uint)i,10,20,-3,0,1)).ToArray();
        var result=OrderOfOperationsExperiment.Run(frames,"test");Assert.Equal(2,result.Count);
        Assert.Equal(new[]{"compress-first","encrypt-first"},result.Select(x=>x.Order).ToArray());
        foreach(var m in result){ Assert.True(m.ExactRestore);Assert.Equal(count*96L,m.RawBits);Assert.Equal(m.TableBits+m.BitstreamBits,m.ResultBits);Assert.Equal(0,m.ResultBits%8);Assert.True(double.IsFinite(m.Ratio)); }
        if(count==0)Assert.All(result,m=>Assert.Equal(0,m.ResultBits));
    }
    [Fact] public void RepetitiveTelemetryCompressesBeforeEncryption()
    {
        var frames=Enumerable.Repeat(new TelemetryFrame(0,0,0,0,0,0),1000).ToArray();
        var result=OrderOfOperationsExperiment.Run(frames,"test");
        Assert.Equal(16,result[0].TableBits);Assert.Equal(12000,result[0].BitstreamBits);
        Assert.True(result[0].ResultBits < result[1].ResultBits);
    }
    [Fact] public void InvalidFramesRejected()=>Assert.Throws<ArgumentException>(()=>OrderOfOperationsExperiment.Run([new(0,0,0,0,8,0)],"test"));
    [Fact] public void CliWritesCsvAndProtectsSource()
    {
        string path=Path.Combine(Path.GetTempPath(),"tlm-order-"+Guid.NewGuid().ToString("N")+".csv");
        try{
            Assert.Equal(0,ExperimentCommand.Execute(new CommandLineOptions(["experiment","--config",DataKit.Path("team_config.json"),"--session",DataKit.Path("session.csv"),"--out",path])));
            Assert.Equal(3,File.ReadAllLines(path).Length);
            Assert.Throws<ArgumentException>(()=>ExperimentCommand.Execute(new CommandLineOptions(["experiment","--config",DataKit.Path("team_config.json"),"--session",path,"--out",path])));
        }finally{File.Delete(path);}
    }
}

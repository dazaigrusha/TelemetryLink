using TelemetryLink.Core.Metrics;
namespace TelemetryLink.Tests;
public class MetricsTests
{
    [Fact] public void PerfectPrediction() => Assert.Equal(1, new ClassificationReport([0,1,2], [0,1,2]).MacroF1);
    [Fact] public void MajorityPredictionPenalizesMissingClasses()
    {
        var report = new ClassificationReport([0,0,0,1,2], [0,0,0,0,0]);
        Assert.Equal(.6, report.Accuracy); Assert.Equal(.75, report.F1(0)); Assert.Equal(0, report.F1(1));
        Assert.Equal(.25, report.MacroF1); Assert.Equal(1, report.Confusion(2,0));
    }
    [Fact] public void EmptyAndMissingClassesStayFinite()
    { Assert.Equal(0, new ClassificationReport([], []).MacroF1); Assert.Equal(1.0/3, new ClassificationReport([0], [0]).MacroF1); }
    [Fact] public void InvalidLabelsRejected()
    { Assert.Throws<ArgumentException>(() => new ClassificationReport([3], [0])); Assert.Throws<ArgumentException>(() => new ClassificationReport([0], [])); }
}

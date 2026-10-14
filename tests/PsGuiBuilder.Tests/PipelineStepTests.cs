// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.Building;

namespace PsGuiBuilder.Tests;

public class PipelineStepTests
{
    [Fact]
    public void Appends_a_single_line_pipeline_step()
    {
        Assert.True(ScriptComposer.TryApplyPipeline("Get-ChildItem -Path \"C:\\Users\"", " Select-Object Name | Out-GridView ", out var command, out var error));
        Assert.Null(error);
        Assert.Equal("Get-ChildItem -Path \"C:\\Users\" | Select-Object Name | Out-GridView", command);

        Assert.True(ScriptComposer.TryApplyPipeline("Get-Date", "   ", out var unchanged, out _));
        Assert.Equal("Get-Date", unchanged);
    }

    [Fact]
    public void Rejects_a_pipeline_step_that_spans_lines()
    {
        Assert.False(ScriptComposer.TryApplyPipeline("Get-Date", "Select-Object\nName", out var command, out var error));
        Assert.Equal("Get-Date", command);
        Assert.Equal("The pipeline step must be a single line.", error);
    }
}

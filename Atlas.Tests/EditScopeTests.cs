using Atlas.Core;

namespace Atlas.Tests;

public class EditScopeTests
{
    private static AppModel Model => AppModelJson.Deserialize(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "rounds-app-model.json")));

    [Fact]
    public void Scope_collects_immediate_flow_and_files()
    {
        var scope = EditScope.For(Model, "vitals");

        Assert.Equal("VitalsEntry", scope.Focus.Name);
        // Inbound: pdetail (observed) and notes (unreachable) both target vitals.
        Assert.Equal(new[] { "pdetail", "notes" }.OrderBy(x => x),
            scope.Inbound.Select(n => n.Id).OrderBy(x => x));
        Assert.Empty(scope.Outbound);
        Assert.Contains("Views/VitalsEntryPage.xaml", scope.Files);
    }

    [Fact]
    public void Scope_prompt_context_names_focus_and_flow()
    {
        var context = EditScope.For(Model, "pdetail").ToPromptContext();

        Assert.Contains("PatientDetail", context);
        Assert.Contains("Navigates to:", context);
        Assert.Contains("MedAdministration", context);
    }

    [Fact]
    public void Unknown_node_throws()
    {
        Assert.Throws<ArgumentException>(() => EditScope.For(Model, "nope"));
    }
}

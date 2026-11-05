using Memshare.Configuration;
using Memshare.Status;
using Memshare.Tests.Support;

namespace Memshare.Tests;

public sealed class StatusReportTests
{
    [Fact]
    public async Task The_report_shows_the_machine_its_stores_and_the_project_link()
    {
        using var sandbox = new Sandbox();
        var remote = await StoreRemote.CreateAsync(sandbox);
        var store = await remote.CloneAsync("laptop");
        File.WriteAllText(Path.Combine(store, "new.md"), "x\n");
        var project = sandbox.Directory("project");
        Directory.CreateDirectory(Path.Combine(project, ".claude"));
        File.WriteAllText(Path.Combine(project, ".claude", "memshare"), "notes my-app\n");
        var environment = sandbox.Environment(projectDirectory: project);
        ConfigFile.Save(environment, new MemshareConfig { Machine = "laptop", StoresRoot = remote.StoresRoot("laptop") });

        var report = await StatusReport.GatherAsync(environment, sandbox.Git, CancellationToken.None);

        Assert.Equal("laptop", report.Machine);
        var only = Assert.Single(report.Stores);
        Assert.Equal("notes", only.Store.Name);
        Assert.Equal(1, only.Unpublished);
        Assert.Equal(("notes", "my-app"), (report.Link?.StoreName, report.Link?.Slug));
    }
}

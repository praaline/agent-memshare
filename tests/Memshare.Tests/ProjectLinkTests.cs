using Memshare.Projects;
using Memshare.Tests.Support;

namespace Memshare.Tests;

public sealed class ProjectLinkTests
{
    [Theory]
    [InlineData("agent-memshare", true)]
    [InlineData("a", true)]
    [InlineData("v2", true)]
    [InlineData("-leading-hyphen", false)]
    [InlineData("Upper", false)]
    [InlineData("with space", false)]
    [InlineData("dots.not.allowed", false)]
    [InlineData("", false)]
    public void Slugs_are_lower_case_letters_digits_and_hyphens(string slug, bool valid) =>
        Assert.Equal(valid, ProjectLink.IsValidSlug(slug));

    [Fact]
    public void A_marker_links_the_project()
    {
        using var sandbox = new Sandbox();
        var project = sandbox.Directory("project");
        WriteMarker(project, "personal my-app\n");

        Assert.Equal(new ProjectLink(project, "personal", "my-app"), ProjectLink.Read(project));
    }

    [Theory]
    [InlineData("")]
    [InlineData("personal")]
    [InlineData("personal My App")]
    [InlineData("personal my-app extra")]
    public void A_malformed_marker_means_not_linked(string content)
    {
        using var sandbox = new Sandbox();
        var project = sandbox.Directory("project");
        WriteMarker(project, content);

        Assert.Null(ProjectLink.Read(project));
    }

    [Fact]
    public void Without_a_marker_the_project_is_not_linked()
    {
        using var sandbox = new Sandbox();

        Assert.Null(ProjectLink.Read(sandbox.Directory("project")));
    }

    [Fact]
    public async Task The_project_root_is_the_top_of_the_git_working_tree()
    {
        using var sandbox = new Sandbox();
        var project = sandbox.Directory("project");
        await sandbox.GitOk(project, "init", "--quiet");
        var nested = sandbox.Directory("project", "src", "deep");

        var root = await ProjectLink.FindProjectRootAsync(sandbox.Git, nested, CancellationToken.None);

        Assert.Equal(Path.GetFullPath(project), root);
    }

    [Fact]
    public async Task Outside_git_the_directory_itself_is_the_project_root()
    {
        using var sandbox = new Sandbox();
        var directory = sandbox.Directory("plain");

        var root = await ProjectLink.FindProjectRootAsync(sandbox.Git, directory, CancellationToken.None);

        Assert.Equal(Path.GetFullPath(directory), root);
    }

    private static void WriteMarker(string project, string content)
    {
        Directory.CreateDirectory(Path.Combine(project, ".claude"));
        File.WriteAllText(Path.Combine(project, ProjectLink.MarkerRelativePath), content);
    }
}

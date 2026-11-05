using Memshare.Stores;

namespace Memshare.Tests;

public sealed class StoreUrlTests
{
    [Theory]
    [InlineData("ssh://git@git.example.com:2222/me/notes.git", "git.example.com", 2222, "me/notes.git", "[git.example.com]:2222")]
    [InlineData("ssh://git@git.example.com/me/notes.git", "git.example.com", null, "me/notes.git", "git.example.com")]
    [InlineData("ssh://git@git.example.com:22/me/notes.git", "git.example.com", null, "me/notes.git", "git.example.com")]
    [InlineData("git@github.com:me/notes.git", "github.com", null, "me/notes.git", "github.com")]
    [InlineData("example.com:notes", "example.com", null, "notes", "example.com")]
    public void SSH_remotes_are_recognised(string url, string host, int? port, string path, string knownHostsName)
    {
        var parsed = StoreUrl.Parse(url);

        Assert.True(parsed.IsSsh);
        Assert.Equal((host, port, path, knownHostsName), (parsed.Host, parsed.Port, parsed.RepositoryPath, parsed.KnownHostsName));
        Assert.Equal("notes", parsed.DefaultName);
    }

    [Theory]
    [InlineData("https://github.com/me/notes.git")]
    [InlineData("/srv/git/notes.git")]
    [InlineData(@"C:\git\notes.git")]
    [InlineData("C:/git/notes")]
    [InlineData("file:///srv/git/notes.git")]
    public void Other_remotes_use_the_users_own_git_setup(string url)
    {
        var parsed = StoreUrl.Parse(url);

        Assert.False(parsed.IsSsh);
        Assert.Equal("notes", parsed.DefaultName);
    }

    [Fact]
    public void An_empty_url_is_refused() => Assert.Throws<MemshareException>(() => StoreUrl.Parse(" "));

    [Fact]
    public void The_ssh_command_uses_only_the_deploy_key_and_the_pinned_host_keys()
    {
        var command = SshAccess.SshCommand(@"C:\Users\me\.ssh\memshare-notes_ed25519", "/home/me/.config/memshare/known_hosts");

        Assert.Equal(
            "ssh -F none -i \"C:/Users/me/.ssh/memshare-notes_ed25519\" -o IdentitiesOnly=yes -o IdentityAgent=none"
            + " -o \"UserKnownHostsFile=/home/me/.config/memshare/known_hosts\" -o StrictHostKeyChecking=yes -o BatchMode=yes -o ConnectTimeout=8",
            command);
    }
}

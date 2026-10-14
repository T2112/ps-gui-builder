// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Net;
using PsGuiBuilder.Core.Updates;
using PsGuiBuilder.Tests.Support;

namespace PsGuiBuilder.Tests;

public class UpdateTests
{
    [Fact]
    public void Versions_compare_numerically_and_ignore_a_build_suffix()
    {
        Assert.True(UpdateVersion.Parse("1.10.0") > UpdateVersion.Parse("1.9.0"));
        Assert.Equal(0, UpdateVersion.Parse("0.3.0").CompareTo(UpdateVersion.Parse("v0.3.0.0")));
        Assert.True(UpdateVersion.Parse("1.0.0-beta") < UpdateVersion.Parse("1.0.0"));
        Assert.Equal("0.3.0", UpdateVersion.PublishedText("0.3.0+abc"));
    }

    [Fact]
    public void Release_offers_the_shellmason_installer_without_a_token()
    {
        var headers = UpdateRelease.Headers("0.3.0");
        Assert.Equal("Shellmason/0.3.0", headers["User-Agent"]);
        Assert.DoesNotContain(headers.Keys, key => key.Equals("Authorization", StringComparison.OrdinalIgnoreCase));

        var status = UpdateRelease.Parse(ReleaseJson("0.4.0"), "0.3.0", skipped: null, installed: true);
        Assert.True(status.Newer);
        Assert.True(status.CanInstall);
        Assert.Equal("Shellmason-Setup-0.4.0.exe", status.AssetName);
        Assert.Equal(UpdatePromptKind.Prompt, UpdateRelease.PromptKind(status, manual: false));

        var skipped = UpdateRelease.Parse(ReleaseJson("0.4.0"), "0.3.0", skipped: "0.4.0", installed: true);
        Assert.Equal(UpdatePromptKind.Quiet, UpdateRelease.PromptKind(skipped, manual: false));
        Assert.Equal(UpdatePromptKind.Prompt, UpdateRelease.PromptKind(skipped, manual: true));

        var current = UpdateRelease.Parse(ReleaseJson("0.3.0"), "0.3.0", skipped: null, installed: true);
        Assert.Equal(UpdatePromptKind.Quiet, UpdateRelease.PromptKind(current, manual: false));
        Assert.Equal(UpdatePromptKind.Current, UpdateRelease.PromptKind(current, manual: true));

        var source = UpdateRelease.Parse(ReleaseJson("0.4.0"), "0.3.0", skipped: null, installed: false);
        Assert.False(source.CanInstall);
        Assert.Null(source.Error);
        Assert.Equal(UpdatePromptKind.Prompt, UpdateRelease.PromptKind(source, manual: false));
    }

    [Fact]
    public void Draft_and_missing_installer_are_not_offered()
    {
        var draft = Assert.Throws<UpdateCheckException>(() => UpdateRelease.Parse(
            ReleaseJson("0.4.0", draft: true),
            "0.3.0",
            skipped: null,
            installed: true));
        Assert.Contains("draft", draft.Message, StringComparison.OrdinalIgnoreCase);

        var missing = UpdateRelease.Parse(ReleaseJson("0.4.0", includeAsset: false), "0.3.0", skipped: null, installed: true);
        Assert.False(missing.CanInstall);
        Assert.Contains("Shellmason-Setup-0.4.0.exe", missing.Error, StringComparison.Ordinal);
        Assert.Equal(UpdatePromptKind.Quiet, UpdateRelease.PromptKind(missing, manual: false));
        Assert.Equal(UpdatePromptKind.Error, UpdateRelease.PromptKind(missing, manual: true));
    }

    [Fact]
    public async Task Check_sends_no_authorization_header()
    {
        var handler = new ScriptedHandler(_ => JsonResponse(ReleaseJson("0.4.0")));
        using var client = new GitHubUpdateClient(handler, "0.3.0");
        var status = await client.CheckAsync("0.3.0", skipped: null, installed: true, CancellationToken.None);
        Assert.True(status.Newer);
        Assert.Null(handler.Authorization);
        Assert.Equal("Shellmason/0.3.0", handler.UserAgent);
    }

    [Fact]
    public async Task Download_checks_size_and_checksum_and_refuses_another_host()
    {
        var body = "installer-bytes"u8.ToArray();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(body)).ToLowerInvariant();
        var handler = new ScriptedHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url.EndsWith(".sha256", StringComparison.Ordinal))
                return Text(hash + "  Shellmason-Setup-0.4.0.exe\n");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(body)
            };
        });
        using var client = new GitHubUpdateClient(handler, "0.3.0");
        var status = UpdateRelease.Parse(ReleaseJson("0.4.0", size: body.Length), "0.3.0", skipped: null, installed: true);
        var root = Path.Combine(Path.GetTempPath(), "shellmason-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = await client.DownloadAsync(status, Path.Combine(root, status.AssetName!), null, CancellationToken.None);
            Assert.Equal(body.Length, new FileInfo(path).Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        Assert.False(UpdateUrls.IsGitHub("http://github.com/T2112/ps-gui-builder/releases/download/v0.4.0/Shellmason-Setup-0.4.0.exe"));
        Assert.False(UpdateUrls.IsGitHub("https://example.com/Shellmason-Setup-0.4.0.exe"));
        Assert.Equal(new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-", "/CLOSEAPPLICATIONS" }, UpdateInstaller.Arguments());
    }

    [Fact]
    public void Skip_remembers_one_version_and_the_workflow_publishes_a_release()
    {
        var root = Path.Combine(Path.GetTempPath(), "shellmason-skip-" + Guid.NewGuid().ToString("N"));
        var path = UpdateSkipStore.PathFor(root);
        UpdateSkipStore.Save(path, "v0.4.0");
        Assert.Equal("0.4.0", UpdateSkipStore.Load(path));
        Assert.Contains("Shellmason", path, StringComparison.Ordinal);

        var workflow = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Repo.CommandTreePath())!, ".github", "workflows", "ci.yml"));
        Assert.Contains("\"draft\":false", workflow, StringComparison.Ordinal);
        Assert.Contains("\"prerelease\":false", workflow, StringComparison.Ordinal);
        Assert.Contains("Shellmason-Setup-$version.exe", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--draft", workflow, StringComparison.Ordinal);
    }

    private static string ReleaseJson(string version, bool draft = false, bool includeAsset = true, int size = 12)
    {
        var asset = includeAsset
            ? """
              , "assets": [
                  {
                    "name": "Shellmason-Setup-VERSION.exe",
                    "browser_download_url": "https://github.com/T2112/ps-gui-builder/releases/download/vVERSION/Shellmason-Setup-VERSION.exe",
                    "size": SIZE
                  },
                  {
                    "name": "Shellmason-Setup-VERSION.exe.sha256",
                    "browser_download_url": "https://github.com/T2112/ps-gui-builder/releases/download/vVERSION/Shellmason-Setup-VERSION.exe.sha256",
                    "size": 80
                  }
                ]
              """
            : ", \"assets\": []";
        return ("""
            {
              "tag_name": "vVERSION",
              "draft": DRAFT,
              "prerelease": false,
              "html_url": "https://github.com/T2112/ps-gui-builder/releases/tag/vVERSION",
              "body": "Notes"
            """ + asset + "}")
            .Replace("VERSION", version, StringComparison.Ordinal)
            .Replace("DRAFT", draft ? "true" : "false", StringComparison.Ordinal)
            .Replace("SIZE", size.ToString(), StringComparison.Ordinal);
    }

    private static HttpResponseMessage JsonResponse(string json) => Text(json);

    private static HttpResponseMessage Text(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        public string? Authorization { get; private set; }

        public string UserAgent { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(_respond(request));
        }
    }
}

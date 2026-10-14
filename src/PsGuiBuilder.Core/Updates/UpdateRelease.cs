// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text.Json;

namespace PsGuiBuilder.Core.Updates;

public enum UpdatePromptKind
{
    Quiet,
    Current,
    Error,
    Prompt
}

public sealed class UpdateCheckException : Exception
{
    public UpdateCheckException(string message)
        : base(message)
    {
    }
}

public sealed class UpdateStatus
{
    public UpdateStatus(
        string current,
        string? latest = null,
        string notes = "",
        string pageUrl = UpdateRelease.ReleasesPageUrl,
        bool newer = false,
        bool skipped = false,
        string? error = null,
        bool canInstall = false,
        string? assetName = null,
        string? assetUrl = null,
        long? assetSize = null,
        string? checksumUrl = null)
    {
        Current = current;
        Latest = latest;
        Notes = notes;
        PageUrl = pageUrl;
        Newer = newer;
        Skipped = skipped;
        Error = error;
        CanInstall = canInstall;
        AssetName = assetName;
        AssetUrl = assetUrl;
        AssetSize = assetSize;
        ChecksumUrl = checksumUrl;
    }

    public string Current { get; }

    public string? Latest { get; }

    public string Notes { get; }

    public string PageUrl { get; }

    public bool Newer { get; }

    public bool Skipped { get; }

    public string? Error { get; }

    public bool CanInstall { get; }

    public string? AssetName { get; }

    public string? AssetUrl { get; }

    public long? AssetSize { get; }

    public string? ChecksumUrl { get; }
}

public static class UpdateRelease
{
    public const string LatestReleaseUrl = "https://api.github.com/repos/T2112/ps-gui-builder/releases/latest";

    public const string ReleasesPageUrl = "https://github.com/T2112/ps-gui-builder/releases/latest";

    public const string InstallerPrefix = "Shellmason-Setup-";

    public static string InstallerName(string publishedVersion) => InstallerPrefix + publishedVersion + ".exe";

    public static string ChecksumName(string installerName) => installerName + ".sha256";

    public static IReadOnlyDictionary<string, string> Headers(string currentVersion)
    {
        var version = UpdateVersion.PublishedText(currentVersion);
        if (version.Length == 0)
            version = "0";

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Accept"] = "application/vnd.github+json",
            ["User-Agent"] = "Shellmason/" + version,
            ["X-GitHub-Api-Version"] = "2022-11-28"
        };
    }

    public static UpdatePromptKind PromptKind(UpdateStatus status, bool manual)
    {
        if (status.Error is not null)
            return manual ? UpdatePromptKind.Error : UpdatePromptKind.Quiet;
        if (!status.Newer)
            return manual ? UpdatePromptKind.Current : UpdatePromptKind.Quiet;
        if (status.Skipped && !manual)
            return UpdatePromptKind.Quiet;

        return UpdatePromptKind.Prompt;
    }

    public static UpdateStatus Parse(string json, string current, string? skipped, bool installed, bool windows = true)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new UpdateCheckException("GitHub returned something that is not a release.");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new UpdateCheckException("GitHub returned something that is not a release.");

            return Parse(document.RootElement, current, skipped, installed, windows);
        }
    }

    public static UpdateStatus Parse(JsonElement payload, string current, string? skipped, bool installed, bool windows = true)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            throw new UpdateCheckException("GitHub returned something that is not a release.");

        if (Flag(payload, "draft"))
            throw new UpdateCheckException("The latest GitHub release is still a draft.");
        if (Flag(payload, "prerelease"))
            throw new UpdateCheckException("The latest GitHub release is a prerelease.");

        var tag = Text(payload, "tag_name");
        if (tag.Length == 0)
            throw new UpdateCheckException("GitHub returned a release without a version tag.");

        UpdateVersion latestVersion;
        UpdateVersion currentVersion;
        try
        {
            latestVersion = UpdateVersion.Parse(tag);
            currentVersion = UpdateVersion.Parse(current);
        }
        catch (FormatException ex)
        {
            throw new UpdateCheckException("Could not compare versions: " + ex.Message);
        }

        var published = latestVersion.Published;
        var page = Text(payload, "html_url");
        if (!UpdateUrls.IsGitHub(page))
            page = "https://github.com/T2112/ps-gui-builder/releases/tag/v" + published;

        var notes = Text(payload, "body");
        var assets = Assets(payload);
        var name = InstallerName(published);
        var foundInstaller = assets.TryGetValue(name, out var installer);
        string? assetUrl = null;
        long? assetSize = null;
        if (foundInstaller)
        {
            var url = Text(installer, "browser_download_url");
            if (UpdateUrls.IsGitHub(url))
                assetUrl = url;

            if (installer.TryGetProperty("size", out var sizeProperty) && sizeProperty.TryGetInt64(out var size) && size >= 0)
                assetSize = size;
        }

        string? checksumUrl = null;
        if (foundInstaller && assets.TryGetValue(ChecksumName(name), out var checksum))
        {
            var url = Text(checksum, "browser_download_url");
            if (UpdateUrls.IsGitHub(url))
                checksumUrl = url;
        }

        var newer = latestVersion > currentVersion;
        var skippedThis = false;
        if (!string.IsNullOrWhiteSpace(skipped) && newer && UpdateVersion.TryParse(skipped, out var skippedVersion))
            skippedThis = skippedVersion.CompareTo(latestVersion) == 0;

        var canInstall = installed && windows && assetUrl is not null && assetSize is not null;
        string? error = null;
        if (installed && newer && !canInstall)
        {
            if (!windows)
                error = "Updates install only on Windows.";
            else if (!foundInstaller)
                error = "Release " + tag + " has no " + name + " to install.";
            else if (assetUrl is null)
                error = "Release " + tag + " does not point at a GitHub download for " + name + ".";
            else
                error = "Release " + tag + " is missing the download size for " + name + ".";
        }

        return new UpdateStatus(
            UpdateVersion.PublishedText(current),
            published,
            notes,
            page,
            newer,
            skippedThis,
            error,
            canInstall,
            foundInstaller ? name : null,
            assetUrl,
            assetSize,
            checksumUrl);
    }

    public static UpdateStatus ErrorStatus(string current, string message)
    {
        var shown = UpdateVersion.TryParse(current, out var version) ? version.Published : current;
        return new UpdateStatus(shown, error: message);
    }

    private static Dictionary<string, JsonElement> Assets(JsonElement payload)
    {
        var found = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (!payload.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return found;

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.ValueKind != JsonValueKind.Object)
                continue;

            var name = Text(asset, "name");
            if (name.Length > 0)
                found[name] = asset;
        }

        return found;
    }

    private static bool Flag(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var property)
        && property.ValueKind == JsonValueKind.True;

    private static string Text(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return "";

        return property.GetString()?.Trim() ?? "";
    }
}

public static class UpdateUrls
{
    public static bool IsGitHub(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            return false;

        var host = uri.IdnHost;
        return host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }
}

// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.Updates;

public static class UpdateChecksum
{
    public static string Parse(string text)
    {
        var line = "";
        foreach (var candidate in text.Split('\n', '\r'))
        {
            if (candidate.Trim().Length > 0)
            {
                line = candidate.Trim();
                break;
            }
        }

        if (line.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            line = line["sha256:".Length..].Trim();

        var token = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var digest = token.ToLowerInvariant();
        if (digest.Length != 64 || digest.Any(character => !Uri.IsHexDigit(character)))
            throw new UpdateCheckException("The published checksum is not a SHA-256 hex digest.");

        return digest;
    }

    public static string File(string path)
    {
        using var stream = System.IO.File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static void Verify(string path, long expectedSize, string? expectedSha256)
    {
        var size = new FileInfo(path).Length;
        if (size != expectedSize)
            throw new UpdateCheckException("Download is " + size + " bytes, but the release says " + expectedSize + ".");

        if (string.IsNullOrWhiteSpace(expectedSha256))
            return;

        var actual = File(path);
        if (!actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new UpdateCheckException("Download checksum does not match the release.");
    }
}

public static class UpdateSkipStore
{
    public static string PathFor(string applicationData) =>
        Path.Combine(applicationData, CommandTreeLocations.UserFolderName, "skipped-update.json");

    public static string? Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            return null;

        try
        {
            using var document = JsonDocument.Parse(System.IO.File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("skippedVersion", out var property) || property.ValueKind != JsonValueKind.String)
                return null;

            var value = property.GetString()?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    public static void Save(string path, string version)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A skip file path is required.", nameof(path));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(new { skippedVersion = UpdateVersion.PublishedText(version) });
        System.IO.File.WriteAllText(path, json);
    }
}

public static class UpdateInstaller
{
    public static IReadOnlyList<string> Arguments() =>
        new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-", "/CLOSEAPPLICATIONS" };
}

public sealed class GitHubUpdateClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly HttpMessageHandler _handler;
    private readonly bool _disposeHandler;

    public GitHubUpdateClient(HttpMessageHandler handler, string currentVersion, bool disposeHandler = false)
    {
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        _handler = handler;
        _disposeHandler = disposeHandler;
        _http = new HttpClient(handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        foreach (var header in UpdateRelease.Headers(currentVersion))
            _http.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
    }

    public static GitHubUpdateClient Create(string currentVersion)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        return new GitHubUpdateClient(handler, currentVersion, disposeHandler: true);
    }

    public async Task<UpdateStatus> CheckAsync(
        string current,
        string? skipped,
        bool installed,
        CancellationToken cancellationToken,
        string? url = null)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            using var response = await SendAsync(new Uri(url ?? UpdateRelease.LatestReleaseUrl), HttpCompletionOption.ResponseContentRead, timeout.Token)
                .ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return UpdateRelease.ErrorStatus(current, "No GitHub release has been published yet.");
            if (IsRateLimited(response.StatusCode, body))
                return UpdateRelease.ErrorStatus(current, "GitHub rate limit reached. Try again in a little while.");
            if (response.StatusCode != HttpStatusCode.OK)
                return UpdateRelease.ErrorStatus(current, "GitHub returned HTTP " + (int)response.StatusCode + " while checking for updates.");

            return UpdateRelease.Parse(body, current, skipped, installed);
        }
        catch (UpdateCheckException ex)
        {
            return UpdateRelease.ErrorStatus(current, ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;

            return UpdateRelease.ErrorStatus(current, "Could not reach GitHub to check for updates.");
        }
    }

    public async Task<string> DownloadAsync(
        UpdateStatus status,
        string destinationPath,
        Action<long, long>? progress,
        CancellationToken cancellationToken)
    {
        if (status.AssetUrl is null || status.AssetName is null || status.AssetSize is null)
            throw new UpdateCheckException("This release has no installer to download.");
        if (!UpdateUrls.IsGitHub(status.AssetUrl))
            throw new UpdateCheckException("The download URL is not a GitHub release URL.");

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var partial = destinationPath + ".part";
        try
        {
            await DownloadFileAsync(status.AssetUrl, partial, progress, cancellationToken).ConfigureAwait(false);
            string? checksum = null;
            if (status.ChecksumUrl is not null)
            {
                var text = await DownloadTextAsync(status.ChecksumUrl, cancellationToken).ConfigureAwait(false);
                checksum = UpdateChecksum.Parse(text);
            }

            UpdateChecksum.Verify(partial, status.AssetSize.Value, checksum);
            System.IO.File.Move(partial, destinationPath, overwrite: true);
            return destinationPath;
        }
        catch
        {
            if (System.IO.File.Exists(partial))
                System.IO.File.Delete(partial);

            throw;
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        if (_disposeHandler)
            _handler.Dispose();
    }

    private async Task DownloadFileAsync(string url, string path, Action<long, long>? progress, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        using var response = await SendAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            if (IsRateLimited(response.StatusCode, body))
                throw new UpdateCheckException("GitHub rate limit reached. Try again in a little while.");

            throw new UpdateCheckException("GitHub returned HTTP " + (int)response.StatusCode + " while downloading the installer.");
        }

        var total = response.Content.Headers.ContentLength ?? 0;
        await using var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        await using var output = System.IO.File.Create(path);
        var buffer = new byte[64 * 1024];
        long done = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, timeout.Token).ConfigureAwait(false);
            if (read == 0)
                break;

            await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token).ConfigureAwait(false);
            done += read;
            progress?.Invoke(done, total);
        }
    }

    private async Task<string> DownloadTextAsync(string url, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await SendAsync(new Uri(url), HttpCompletionOption.ResponseContentRead, timeout.Token).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new UpdateCheckException("Could not download the release checksum. The update was not installed.");

        return await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(Uri uri, HttpCompletionOption option, CancellationToken cancellationToken)
    {
        for (var hop = 0; hop < 5; hop++)
        {
            if (!UpdateUrls.IsGitHub(uri.AbsoluteUri))
                throw new UpdateCheckException("The download URL is not a GitHub release URL.");

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await _http.SendAsync(request, option, cancellationToken).ConfigureAwait(false);
            var code = (int)response.StatusCode;
            if (code is not (301 or 302 or 303 or 307 or 308))
                return response;

            var next = response.Headers.Location;
            response.Dispose();
            if (next is null)
                throw new UpdateCheckException("GitHub redirected without a location.");

            uri = next.IsAbsoluteUri ? next : new Uri(uri, next);
        }

        throw new UpdateCheckException("Too many redirects while contacting GitHub.");
    }

    private static bool IsRateLimited(HttpStatusCode status, string body)
    {
        if (status == HttpStatusCode.TooManyRequests)
            return true;

        if (status != HttpStatusCode.Forbidden)
            return false;

        var text = body.ToLowerInvariant();
        return text.Contains("rate limit", StringComparison.Ordinal) || text.Contains("secondary rate", StringComparison.Ordinal);
    }
}

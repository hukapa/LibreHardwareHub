// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LibreHardwareMonitor.Windows.Forms.Utilities;

/// <summary>A published GitHub release: version, notes and the portable package to download.</summary>
public sealed class UpdateReleaseInfo
{
    public string Tag { get; init; } = string.Empty;

    public Version Version { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public DateTime? PublishedAt { get; init; }

    public string AssetUrl { get; init; } = string.Empty;

    public long AssetSize { get; init; }

    /// <summary>SHA-256 (hex) of the package as published by GitHub; empty for releases that predate the digest.</summary>
    public string AssetSha256 { get; init; } = string.Empty;

    /// <summary>"0.11.0": the version as shown to the user.</summary>
    public string VersionText => Version?.ToString(3) ?? Tag;
}

/// <summary>
/// Looks for new releases of LibreHardwareHub on GitHub and downloads them. Uses only HttpClient and
/// System.Text.Json: one request per check, nothing runs in the background between checks.
/// </summary>
public static class AppUpdater
{
    private const string Repository = "hukapa/LibreHardwareHub";
    private const long MinimumExecutableBytes = 1024 * 1024;
    private const string ReadyMarker = "ready.txt";

    private static readonly HttpClient Http = CreateClient();

    /// <summary>Folder (under %TEMP%) that holds downloaded updates; the apply step and the cleanup step both check it.</summary>
    public static string UpdateRoot => Path.Combine(Path.GetTempPath(), "LibreHardwareHub_Update");

    public static Version CurrentVersion => Version.TryParse(AppInfo.Version, out Version version) ? version : new Version(0, 0, 0);

    public static bool IsNewer(UpdateReleaseInfo release) => release?.Version != null && release.Version > CurrentVersion;

    private static HttpClient CreateClient()
    {
        HttpClient client = new() { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.Id);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>The latest published (non-draft, non-prerelease) release, or null when there is none.</summary>
    public static async Task<UpdateReleaseInfo> GetLatestAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await Http.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest", ct).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        return ReadRelease(document.RootElement);
    }

    /// <summary>The most recent releases, newest first, for the patch notes page.</summary>
    public static async Task<List<UpdateReleaseInfo>> GetHistoryAsync(int count, CancellationToken ct)
    {
        List<UpdateReleaseInfo> releases = new();
        using HttpResponseMessage response = await Http.GetAsync($"https://api.github.com/repos/{Repository}/releases?per_page={count}", ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return releases;

        foreach (JsonElement element in document.RootElement.EnumerateArray())
        {
            if (element.TryGetProperty("draft", out JsonElement draft) && draft.ValueKind == JsonValueKind.True)
                continue;

            UpdateReleaseInfo release = ReadRelease(element);
            if (release != null)
                releases.Add(release);
        }

        return releases;
    }

    /// <summary>"v0.11.0", "0.11.0+abc" and "0.11.0-beta" all become 0.11.0; anything else is null.</summary>
    public static Version ParseVersion(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;

        string text = tag.Trim().TrimStart('v', 'V');
        int cut = text.IndexOfAny(new[] { '+', '-' });
        if (cut >= 0)
            text = text.Substring(0, cut);

        return Version.TryParse(text, out Version version) ? version : null;
    }

    private static UpdateReleaseInfo ReadRelease(JsonElement element)
    {
        string tag = GetString(element, "tag_name");
        Version version = ParseVersion(tag);
        if (version == null)
            return null;

        DateTime? published = DateTime.TryParse(GetString(element, "published_at"), null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed)
            ? parsed.ToLocalTime()
            : null;

        string assetUrl = string.Empty;
        string assetSha256 = string.Empty;
        long assetSize = 0;
        if (element.TryGetProperty("assets", out JsonElement assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement asset in assets.EnumerateArray())
            {
                string name = GetString(asset, "name");
                if (!name.EndsWith("-Portable.zip", StringComparison.OrdinalIgnoreCase))
                    continue;

                assetUrl = GetString(asset, "browser_download_url");
                string digest = GetString(asset, "digest");
                if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    assetSha256 = digest.Substring(7);

                if (asset.TryGetProperty("size", out JsonElement size) && size.TryGetInt64(out long bytes))
                    assetSize = bytes;
                break;
            }
        }

        return new UpdateReleaseInfo
        {
            Tag = tag,
            Version = version,
            Title = GetString(element, "name"),
            Body = GetString(element, "body"),
            PublishedAt = published,
            AssetUrl = assetUrl,
            AssetSize = assetSize,
            AssetSha256 = assetSha256
        };
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    /// <summary>Path of the already downloaded and validated executable of <paramref name="release" />, if any.</summary>
    public static bool TryGetDownloaded(UpdateReleaseInfo release, out string executable)
    {
        executable = Path.Combine(PackageDirectory(release), AppInfo.Id + ".exe");
        return File.Exists(Path.Combine(PackageDirectory(release), ReadyMarker)) && IsValidExecutable(executable);
    }

    private static string PackageDirectory(UpdateReleaseInfo release) => Path.Combine(UpdateRoot, release.VersionText, "pkg");

    private static bool IsValidExecutable(string path)
    {
        try
        {
            return File.Exists(path) && new FileInfo(path).Length > MinimumExecutableBytes;
        }
        catch (IOException)
        {
            return false;
        }
    }

    // Only the project's own release assets on GitHub over HTTPS are downloaded.
    private static bool IsTrustedDownload(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps)
            return false;

        return uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Downloads the portable zip into %TEMP%\LibreHardwareHub_Update, extracts it into a clean folder and checks that the
    /// new executable is there. Returns the path of that executable.
    /// </summary>
    public static async Task<string> DownloadUpdateAsync(UpdateReleaseInfo release, IProgress<int> progress, CancellationToken ct)
    {
        if (release == null || !IsTrustedDownload(release.AssetUrl))
            throw new InvalidOperationException("A versão não tem um pacote portátil para descarregar.");

        if (TryGetDownloaded(release, out string existing))
        {
            progress?.Report(100);
            return existing;
        }

        string versionDirectory = Path.Combine(UpdateRoot, release.VersionText);
        if (Directory.Exists(versionDirectory))
            Directory.Delete(versionDirectory, true);

        Directory.CreateDirectory(versionDirectory);
        string zipPath = Path.Combine(versionDirectory, "update.zip");
        string packageDirectory = PackageDirectory(release);

        // The package is large: the 15 s of the shared client apply to the headers only, not to the body.
        using (HttpResponseMessage response = await Http.GetAsync(release.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? release.AssetSize;
            await using Stream source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using FileStream target = new(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            byte[] buffer = new byte[81920];
            long done = 0;
            int lastPercent = -1;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                done += read;
                int percent = total > 0 ? (int)Math.Min(99, done * 100 / total) : 0;
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    progress?.Report(percent);
                }
            }
        }

        ct.ThrowIfCancellationRequested();

        // GitHub publishes the SHA-256 of every asset: a package that does not match it is never extracted.
        if (release.AssetSha256.Length > 0)
        {
            string actual;
            await using (FileStream stream = new(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false));

            if (!actual.Equals(release.AssetSha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(zipPath);
                throw new InvalidDataException("A verificação de integridade do pacote falhou. Tente novamente mais tarde.");
            }
        }

        ZipFile.ExtractToDirectory(zipPath, packageDirectory, true);
        File.Delete(zipPath);

        string executable = Path.Combine(packageDirectory, AppInfo.Id + ".exe");
        if (!IsValidExecutable(executable))
            throw new InvalidDataException("O pacote descarregado não contém um " + AppInfo.Id + ".exe válido.");

        File.WriteAllText(Path.Combine(packageDirectory, ReadyMarker), release.VersionText);
        progress?.Report(100);
        return executable;
    }
}

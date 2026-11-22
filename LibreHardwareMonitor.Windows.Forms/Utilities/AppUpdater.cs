// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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
public static partial class AppUpdater
{
    private const string Repository = "hukapa/LibreHardwareHub";

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
}

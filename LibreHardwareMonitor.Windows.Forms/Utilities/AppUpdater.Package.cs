// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace LibreHardwareMonitor.Windows.Forms.Utilities;

/// <summary>
/// Download, verification and extraction of the update package. The result is started with administrator rights, so
/// every step distrusts the network and the temp folder: GitHub only (redirects included), the published SHA-256, an
/// extraction that cannot leave its folder, and a hash of the new executable taken in memory that is checked again
/// right before it is started.
/// </summary>
public static partial class AppUpdater
{
    private const long MinimumExecutableBytes = 1024 * 1024;
    private const long MaxDownloadBytes = 300L * 1024 * 1024;
    private const long MaxUnpackedBytes = 400L * 1024 * 1024;
    private const int MaxEntries = 200;
    private const int MaxRedirects = 5;

    // Redirects are followed by hand so that every hop is checked, not only the first address.
    private static readonly HttpClient DownloadHttp = CreateDownloadClient();

    // SHA-256 of the executables extracted by this session. Anything on disk that is not in here is not trusted.
    private static readonly Dictionary<string, byte[]> StagedHashes = new(StringComparer.OrdinalIgnoreCase);

    private static HttpClient CreateDownloadClient()
    {
        HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.Id);
        return client;
    }

    /// <summary>Path of the executable that this session already downloaded and extracted for <paramref name="release" />, if any.</summary>
    public static bool TryGetDownloaded(UpdateReleaseInfo release, out string executable)
    {
        executable = Path.Combine(PackageDirectory(release), AppInfo.Id + ".exe");
        lock (StagedHashes)
            return StagedHashes.ContainsKey(executable) && IsValidExecutable(executable);
    }

    /// <summary>SHA-256 recorded when <paramref name="executable" /> was extracted; false for any other file.</summary>
    public static bool TryGetStagedHash(string executable, out byte[] hash)
    {
        lock (StagedHashes)
            return StagedHashes.TryGetValue(executable, out hash);
    }

    /// <summary>Drops the trust in an extracted executable that turned out to have changed: the next attempt downloads it again.</summary>
    public static void ForgetStaged(string executable)
    {
        lock (StagedHashes)
            StagedHashes.Remove(executable);
    }

    private static string PackageDirectory(UpdateReleaseInfo release) => Path.Combine(UpdateRoot, release.VersionText, "pkg");

    /// <summary>True when <paramref name="path" /> is inside the update folder (compared as full paths, with the separator).</summary>
    public static bool IsUnderUpdateRoot(string path)
    {
        string root = Path.GetFullPath(UpdateRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Deletes a folder; a link (symlink or junction) is removed itself, never what it points to.</summary>
    public static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        Directory.Delete(path, (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0);
    }

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

    // HTTPS on the default port, no credentials in the address, and only GitHub itself or its content hosts.
    private static bool IsTrustedUri(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) &&
        (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));

    /// <summary>GETs <paramref name="url" />, following redirects only while every address stays on a trusted host.</summary>
    private static async Task<HttpResponseMessage> GetTrustedAsync(Uri uri, CancellationToken ct)
    {
        for (int hop = 0; hop <= MaxRedirects; hop++)
        {
            if (!IsTrustedUri(uri))
                throw new InvalidOperationException("O endereço de descarregamento não é do GitHub.");

            HttpResponseMessage response = await DownloadHttp.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            try
            {
                if (response.StatusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.SeeOther or HttpStatusCode.RedirectKeepVerb or HttpStatusCode.PermanentRedirect &&
                    response.Headers.Location is { } location)
                {
                    uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                if (response.RequestMessage?.RequestUri is not { } final || !IsTrustedUri(final))
                    throw new InvalidOperationException("A resposta não veio de um endereço do GitHub.");

                return response;
            }
            catch
            {
                response.Dispose();
                throw;
            }
            finally
            {
                // A redirect was followed: this response is no longer needed (the final one is returned, not disposed).
                if (response.StatusCode is >= HttpStatusCode.Moved and < HttpStatusCode.BadRequest)
                    response.Dispose();
            }
        }

        throw new InvalidOperationException("Redirecionamentos a mais ao descarregar a atualização.");
    }

    /// <summary>
    /// Downloads the portable zip into %TEMP%\LibreHardwareHub_Update, checks it against the SHA-256 published by GitHub,
    /// extracts it into a clean folder and checks that the new executable is there. Returns the path of that executable.
    /// Whatever fails or is cancelled leaves nothing partial behind.
    /// </summary>
    public static async Task<string> DownloadUpdateAsync(UpdateReleaseInfo release, IProgress<int> progress, CancellationToken ct)
    {
        if (release == null || !Uri.TryCreate(release.AssetUrl, UriKind.Absolute, out Uri assetUri) || !IsTrustedUri(assetUri))
            throw new InvalidOperationException("A versão não tem um pacote portátil para descarregar.");

        if (TryGetDownloaded(release, out string existing))
        {
            progress?.Report(100);
            return existing;
        }

        PrepareUpdateRoot();
        string versionDirectory = Path.Combine(UpdateRoot, release.VersionText);
        string zipPath = Path.Combine(versionDirectory, "update.zip");
        string packageDirectory = PackageDirectory(release);
        byte[] expected = ParseDigest(release.AssetSha256);

        try
        {
            // A package left by an earlier session is reused only if it is exactly the file GitHub publishes for the release.
            bool cached = expected != null && File.Exists(zipPath) && await HashEqualsAsync(zipPath, expected, ct).ConfigureAwait(false);
            if (!cached)
            {
                DeleteDirectory(versionDirectory);
                Directory.CreateDirectory(versionDirectory);
                await DownloadFileAsync(assetUri, release.AssetSize, zipPath, progress, ct).ConfigureAwait(false);

                // GitHub publishes the SHA-256 of every asset: a package that does not match it is never extracted.
                if (expected != null && !await HashEqualsAsync(zipPath, expected, ct).ConfigureAwait(false))
                    throw new InvalidDataException("A verificação de integridade do pacote falhou. Tente novamente mais tarde.");
            }

            DeleteDirectory(packageDirectory);
            ExtractSafely(zipPath, packageDirectory);

            string executable = Path.Combine(packageDirectory, AppInfo.Id + ".exe");
            if (!IsValidExecutable(executable))
                throw new InvalidDataException("O pacote descarregado não contém um " + AppInfo.Id + ".exe válido.");

            byte[] hash = await VerifiedFile.Sha256Async(executable, ct).ConfigureAwait(false);
            lock (StagedHashes)
                StagedHashes[executable] = hash;

            // Without a published digest the zip could not be checked again later, so it is not kept.
            if (expected == null)
                File.Delete(zipPath);

            progress?.Report(100);
            return executable;
        }
        catch
        {
            DeleteDirectory(versionDirectory);
            throw;
        }
    }

    // The update folder must be a real folder: a link planted in its place would send the download somewhere else.
    private static void PrepareUpdateRoot()
    {
        string root = UpdateRoot;
        if (Directory.Exists(root) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            DeleteDirectory(root);

        Directory.CreateDirectory(root);
    }

    private static byte[] ParseDigest(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return null;

        try
        {
            return Convert.FromHexString(hex);
        }
        catch (FormatException)
        {
            // A digest that is not hexadecimal can never match: the package will be refused.
            return new byte[SHA256.HashSizeInBytes];
        }
    }

    private static async Task<bool> HashEqualsAsync(string path, byte[] expected, CancellationToken ct) =>
        CryptographicOperations.FixedTimeEquals(await VerifiedFile.Sha256Async(path, ct).ConfigureAwait(false), expected);

    private static async Task DownloadFileAsync(Uri uri, long advertisedSize, string target, IProgress<int> progress, CancellationToken ct)
    {
        // The package is large: the 15 s of the shared client apply to the headers only, not to the body.
        using HttpResponseMessage response = await GetTrustedAsync(uri, ct).ConfigureAwait(false);
        long total = response.Content.Headers.ContentLength ?? advertisedSize;
        if (total > MaxDownloadBytes)
            throw new InvalidDataException("O pacote é maior do que o esperado.");

        await using Stream source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using FileStream file = new(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);

        byte[] buffer = new byte[81920];
        long done = 0;
        int lastPercent = -1;
        int read;
        while ((read = await source.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) > 0)
        {
            done += read;
            if (done > MaxDownloadBytes)
                throw new InvalidDataException("O pacote é maior do que o esperado.");

            await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            int percent = total > 0 ? (int)Math.Min(99, done * 100 / total) : 0;
            if (percent != lastPercent)
            {
                lastPercent = percent;
                progress?.Report(percent);
            }
        }
    }

    /// <summary>
    /// Extracts entry by entry into <paramref name="destination" />. Every entry is checked before a byte is written: no drive,
    /// no stream name, no absolute path, and the full path must stay inside the folder; the number of entries and the real
    /// unpacked size are limited; a repeated name is an error.
    /// </summary>
    private static void ExtractSafely(string zipPath, string destination)
    {
        string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(root);

        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > MaxEntries)
            throw new InvalidDataException("O pacote tem entradas a mais.");

        long unpacked = 0;
        byte[] buffer = new byte[81920];
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string name = entry.FullName;
            if (name.Contains(':') || Path.IsPathRooted(name))
                throw new InvalidDataException("Entrada inválida no pacote: " + name);

            string target = Path.GetFullPath(Path.Combine(root, name));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Entrada fora da pasta de destino no pacote: " + name);

            if (entry.Name.Length == 0)
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target));
            using Stream input = entry.Open();
            using FileStream output = new(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                unpacked += read;
                if (unpacked > MaxUnpackedBytes)
                    throw new InvalidDataException("O pacote descomprimido é maior do que o esperado.");

                output.Write(buffer, 0, read);
            }
        }
    }
}

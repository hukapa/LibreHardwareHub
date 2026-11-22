// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace LibreHardwareMonitor.Windows.Forms.Utilities;

/// <summary>
/// An executable that this elevated application is about to start from a folder anyone running as the same user can
/// write to (the temp folder). The file is opened without write or delete sharing and hashed while it is held, and the
/// caller keeps the handle until the process has started: nothing can swap the file between the check and the start.
/// </summary>
public static class VerifiedFile
{
    /// <summary>
    /// Opens <paramref name="path" /> read-only, denying everyone else write and delete access, and checks that its SHA-256 is
    /// <paramref name="expectedSha256" />. Dispose the returned stream only after the process has started.
    /// </summary>
    /// <exception cref="InvalidDataException">The file is not the one that was expected.</exception>
    public static FileStream OpenVerified(string path, byte[] expectedSha256)
    {
        FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            byte[] actual = SHA256.HashData(stream);
            if (expectedSha256 == null || !CryptographicOperations.FixedTimeEquals(actual, expectedSha256))
                throw new InvalidDataException("O ficheiro mudou desde que foi verificado: " + Path.GetFileName(path));

            stream.Position = 0;
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public static async Task<byte[]> Sha256Async(string path, CancellationToken ct)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        return await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
    }
}

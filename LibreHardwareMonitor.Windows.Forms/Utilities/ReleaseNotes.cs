// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace LibreHardwareMonitor.Windows.Forms.Utilities;

/// <summary>One line of release notes: a section heading or a bullet, already stripped of Markdown.</summary>
public readonly record struct NoteLine(string Text, bool IsHeading);

/// <summary>The notes of one version.</summary>
public sealed class ReleaseNoteEntry
{
    public string Version { get; init; } = string.Empty;

    public DateTime? Date { get; init; }

    public List<NoteLine> Lines { get; } = new();
}

/// <summary>
/// Turns Markdown (GitHub release bodies and the embedded CHANGELOG.md) into plain lines the UI can draw
/// without a Markdown renderer, and reads the embedded changelog used when GitHub cannot be reached.
/// </summary>
public static class ReleaseNotes
{
    private const string ChangelogResource = "CHANGELOG.md";

    /// <summary>The changelog embedded in the executable; empty when the resource is missing.</summary>
    public static List<ReleaseNoteEntry> LoadEmbedded()
    {
        try
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ChangelogResource);
            if (stream == null)
                return new List<ReleaseNoteEntry>();

            using StreamReader reader = new(stream, Encoding.UTF8);
            return ParseChangelog(reader.ReadToEnd());
        }
        catch (Exception e) when (e is IOException or InvalidOperationException)
        {
            return new List<ReleaseNoteEntry>();
        }
    }

    /// <summary>Splits a changelog on its "## vX.Y.Z - yyyy-MM-dd" headings.</summary>
    public static List<ReleaseNoteEntry> ParseChangelog(string markdown)
    {
        List<ReleaseNoteEntry> entries = new();
        if (string.IsNullOrEmpty(markdown))
            return entries;

        ReleaseNoteEntry current = null;
        foreach (string raw in markdown.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                (string version, DateTime? date) = ParseHeading(line.Substring(3));
                current = new ReleaseNoteEntry { Version = version, Date = date };
                entries.Add(current);
            }
            else if (current != null && TryConvertLine(line, out NoteLine note))
            {
                current.Lines.Add(note);
            }
        }

        return entries;
    }

    /// <summary>Notes of a GitHub release body; the title line is not repeated, the version heading is added by the caller.</summary>
    public static ReleaseNoteEntry FromBody(string version, DateTime? date, string body)
    {
        ReleaseNoteEntry entry = new() { Version = version, Date = date };
        if (string.IsNullOrWhiteSpace(body))
            return entry;

        foreach (string raw in body.Split('\n'))
        {
            if (TryConvertLine(raw.TrimEnd('\r'), out NoteLine note))
                entry.Lines.Add(note);
        }

        return entry;
    }

    private static (string Version, DateTime? Date) ParseHeading(string heading)
    {
        string version = heading.Trim();
        DateTime? date = null;
        int separator = version.IndexOf(" - ", StringComparison.Ordinal);
        if (separator >= 0)
        {
            if (DateTime.TryParseExact(version.Substring(separator + 3).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                date = parsed;

            version = version.Substring(0, separator).Trim();
        }

        return (version, date);
    }

    private static bool TryConvertLine(string line, out NoteLine note)
    {
        note = default;
        string text = line.Trim();
        if (text.Length == 0 || text.StartsWith("<", StringComparison.Ordinal) || text.StartsWith("---", StringComparison.Ordinal))
            return false;

        bool heading = false;
        if (text[0] == '#')
        {
            heading = true;
            text = text.TrimStart('#').Trim();
        }
        else if (text.StartsWith("- ", StringComparison.Ordinal) || text.StartsWith("* ", StringComparison.Ordinal))
        {
            text = text.Substring(2).Trim();
        }

        text = StripInline(text);
        if (text.Length == 0)
            return false;

        note = new NoteLine(text, heading);
        return true;
    }

    // Drops emphasis and code marks and turns "[text](url)" into "text".
    private static string StripInline(string text)
    {
        StringBuilder result = new(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '*' || c == '`' || c == '_' && (i == 0 || text[i - 1] == ' ' || i == text.Length - 1 || text[i + 1] == ' '))
                continue;

            if (c == '[')
            {
                int close = text.IndexOf("](", i, StringComparison.Ordinal);
                int end = close < 0 ? -1 : text.IndexOf(')', close + 2);
                if (close > i && end > close)
                {
                    result.Append(text, i + 1, close - i - 1);
                    i = end;
                    continue;
                }
            }

            result.Append(c);
        }

        return result.ToString().Trim();
    }
}

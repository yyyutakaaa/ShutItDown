using System.Text.RegularExpressions;

namespace ShutItDown.Core.Monitoring;

public sealed record UnsavedWarning(string ProcessName, string Title, string Reason);

/// <summary>
/// Guesses which windows hold unsaved work, from their titles. Most editors mark unsaved files with
/// a "*" or "●", and new documents keep a default name like "Untitled" or "Document1".
/// It's a best guess: some apps show nothing in the title.
/// </summary>
public static partial class UnsavedWorkDetector
{
    public static IReadOnlyList<UnsavedWarning> Find(IEnumerable<OpenApp> apps)
    {
        var warnings = new List<UnsavedWarning>();
        foreach (var app in apps)
        {
            if (Reason(app.Title) is { } reason)
                warnings.Add(new UnsavedWarning(app.ProcessName, app.Title, reason));
        }
        return warnings;
    }

    public static string? Reason(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        if (UnsavedMarker().IsMatch(title)) return "has unsaved changes";
        if (DefaultName().IsMatch(title)) return "has a document that was never saved";
        return null;
    }

    // "*notes.txt - Notepad", "notes.txt* - App", "● main.cs - Visual Studio Code", "image.psd @ 50% (RGB/8*)"
    [GeneratedRegex(@"^\s*[*●•]|\*\s*[-–—|]\s|\*\)\s*$|\*$")]
    private static partial Regex UnsavedMarker();

    // New documents that still have their default name (English and Dutch Office/Notepad names).
    [GeneratedRegex(@"\b(Untitled|Unsaved|Naamloos|Document\d+|Book\d+|Map\d+|Presentation\d+|Presentatie\d+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DefaultName();
}

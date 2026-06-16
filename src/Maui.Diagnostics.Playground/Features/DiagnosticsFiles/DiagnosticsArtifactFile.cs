using System.Globalization;

namespace Maui.Diagnostics.Playground.Features.DiagnosticsFiles;

public sealed record DiagnosticsArtifactFile(
    string FullPath,
    string RootLabel,
    string RelativePath,
    long SizeBytes,
    DateTimeOffset LastModified)
{
    public string Name => Path.GetFileName(FullPath);

    public string Kind => GetKind(Name);

    public string SizeText => FormatSize(SizeBytes);

    public string ModifiedText => LastModified.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);

    public string SummaryText => $"{Kind} • {RootLabel} • {SizeText} • {ModifiedText}";

    private static string GetKind(string name)
    {
        if (name.EndsWith(".crashreport.json", StringComparison.OrdinalIgnoreCase))
        {
            return ".NET crash report";
        }

        if (name.StartsWith("tombstone", StringComparison.OrdinalIgnoreCase))
        {
            return "Android tombstone";
        }

        var extension = Path.GetExtension(name);
        return string.IsNullOrWhiteSpace(extension) ? "Diagnostics file" : extension.TrimStart('.').ToUpperInvariant();
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var size = (double)Math.Max(bytes, 0);
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0
            ? string.Create(CultureInfo.CurrentCulture, $"{bytes} {units[unit]}")
            : string.Create(CultureInfo.CurrentCulture, $"{size:0.##} {units[unit]}");
    }
}

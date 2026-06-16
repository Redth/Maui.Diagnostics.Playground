using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

namespace Maui.Diagnostics.Playground.Features.DiagnosticsFiles;

public sealed class DiagnosticsArtifactService : IDiagnosticsArtifactService
{
    public const int DefaultPreviewBytes = 512 * 1024;

    private static readonly string[] DiagnosticExtensions =
    [
        ".crashreport.json",
        ".dmp",
        ".mdmp",
        ".log",
        ".txt"
    ];

    private static readonly JsonSerializerOptions JsonPreviewOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public Task<IReadOnlyList<DiagnosticsArtifactFile>> ListAsync(CancellationToken cancellationToken = default)
    {
        var files = GetRoots()
            .Where(root => Directory.Exists(root.Path))
            .SelectMany(root => EnumerateDiagnosticFiles(root, cancellationToken))
            .GroupBy(file => file.FullPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(file => file.LastModified)
            .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<DiagnosticsArtifactFile>>(files);
    }

    public Task<DiagnosticsArtifactFile> GetAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = Path.GetFullPath(fullPath);
        var root = GetRoots().FirstOrDefault(candidate => IsUnderRoot(normalized, candidate.Path));
        if (root is null || !File.Exists(normalized) || !IsDiagnosticFile(normalized))
        {
            throw new FileNotFoundException("The diagnostics artifact is not available.", fullPath);
        }

        return Task.FromResult(CreateFile(root, normalized));
    }

    public async Task<string> ReadTextAsync(
        DiagnosticsArtifactFile file,
        int maxBytes = DefaultPreviewBytes,
        CancellationToken cancellationToken = default)
    {
        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), maxBytes, "Preview length must be positive.");
        }

        await using var stream = File.OpenRead(file.FullPath);
        var bytesToRead = (int)Math.Min(stream.Length, maxBytes);
        var buffer = new byte[bytesToRead];
        var totalRead = 0;

        while (totalRead < bytesToRead)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, bytesToRead - totalRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        var text = Encoding.UTF8.GetString(buffer, 0, totalRead);
        var truncated = stream.Length > totalRead;

        if (!truncated && IsJsonFile(file.Name))
        {
            text = FormatJson(text);
        }

        if (truncated)
        {
            text += $"{Environment.NewLine}{Environment.NewLine}--- Preview truncated at {totalRead} bytes. Use Share to export the full file. ---";
        }

        return text;
    }

    public Task ShareAsync(DiagnosticsArtifactFile file)
    {
        return Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = file.Name,
            File = new ShareFile(file.FullPath)
        });
    }

    public Task ShareAsync(IReadOnlyList<DiagnosticsArtifactFile> files)
    {
        if (files.Count == 0)
        {
            throw new ArgumentException("At least one diagnostics artifact is required.", nameof(files));
        }

        return Share.Default.RequestAsync(new ShareMultipleFilesRequest
        {
            Title = "Diagnostics artifacts",
            Files = files.Select(file => new ShareFile(file.FullPath)).ToList()
        });
    }

    private static IEnumerable<DiagnosticsArtifactFile> EnumerateDiagnosticFiles(
        DiagnosticsRoot root,
        CancellationToken cancellationToken)
    {
        foreach (var path in Directory.EnumerateFiles(root.Path, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsDiagnosticFile(path))
            {
                yield return CreateFile(root, path);
            }
        }
    }

    private static DiagnosticsArtifactFile CreateFile(DiagnosticsRoot root, string fullPath)
    {
        var info = new FileInfo(fullPath);
        var relativePath = Path.GetRelativePath(root.Path, fullPath);

        return new DiagnosticsArtifactFile(
            info.FullName,
            root.Label,
            relativePath,
            info.Length,
            info.LastWriteTime);
    }

    private static DiagnosticsRoot[] GetRoots()
    {
        return
        [
            new DiagnosticsRoot("App data", FileSystem.Current.AppDataDirectory),
            new DiagnosticsRoot(".NET crash reports", Path.Combine(FileSystem.Current.AppDataDirectory, ".dotnet", "crash-reports")),
            new DiagnosticsRoot("Cache", FileSystem.Current.CacheDirectory)
        ];
    }

    private static bool IsDiagnosticFile(string path)
    {
        var name = Path.GetFileName(path);
        if (name.StartsWith("dotnet_crash_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("tombstone", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) &&
            path.Contains($"{Path.DirectorySeparatorChar}.dotnet{Path.DirectorySeparatorChar}crash-reports{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return DiagnosticExtensions.Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsJsonFile(string name)
    {
        return name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatJson(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return JsonSerializer.Serialize(document.RootElement, JsonPreviewOptions);
        }
        catch (JsonException)
        {
            return text;
        }
    }

    private static bool IsUnderRoot(string fullPath, string rootPath)
    {
        var normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
            fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record DiagnosticsRoot(string Label, string Path);
}

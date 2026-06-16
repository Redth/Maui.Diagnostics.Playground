namespace Maui.Diagnostics.Playground.Features.DiagnosticsFiles;

public interface IDiagnosticsArtifactService
{
    Task<IReadOnlyList<DiagnosticsArtifactFile>> ListAsync(CancellationToken cancellationToken = default);

    Task<DiagnosticsArtifactFile> GetAsync(string fullPath, CancellationToken cancellationToken = default);

    Task<string> ReadTextAsync(
        DiagnosticsArtifactFile file,
        int maxBytes = DiagnosticsArtifactService.DefaultPreviewBytes,
        CancellationToken cancellationToken = default);

    Task ShareAsync(DiagnosticsArtifactFile file);

    Task ShareAsync(IReadOnlyList<DiagnosticsArtifactFile> files);
}

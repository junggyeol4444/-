using MyVocalStudio.Models;

namespace MyVocalStudio.Services;

public sealed record RecoveryInfo(string Path, DateTimeOffset SavedAt, long Size);

/// <summary>Maintains one atomic per-user crash-recovery snapshot.</summary>
public sealed class RecoveryService
{
    private readonly ProjectService _projects;
    private readonly string _recoveryPath;

    public RecoveryService(ProjectService projects, string? dataDirectory = null)
    {
        _projects = projects;
        var root = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MYVOCAL Studio",
            "Recovery");
        _recoveryPath = Path.Combine(root, "autosave.myvocal");
    }

    public RecoveryInfo? GetRecoveryInfo()
    {
        var file = new FileInfo(_recoveryPath);
        return file.Exists
            ? new RecoveryInfo(file.FullName, file.LastWriteTimeUtc, file.Length)
            : null;
    }

    public Task SaveAsync(StudioProject project, CancellationToken token = default) =>
        _projects.SaveAsync(project, _recoveryPath, token);

    public Task<StudioProject> LoadAsync(CancellationToken token = default) =>
        _projects.LoadAsync(_recoveryPath, token);

    public void Clear()
    {
        TryDelete(_recoveryPath);
        TryDelete(_recoveryPath + ".tmp");
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

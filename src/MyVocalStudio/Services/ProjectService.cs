using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using MyVocalStudio.Models;

namespace MyVocalStudio.Services;

/// <summary>Saves portable .myvocal packages and opens both package and legacy JSON projects.</summary>
public sealed class ProjectService
{
    private const string ManifestName = "project.json";
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private readonly string _cacheRoot;

    public ProjectService(string? cacheRoot = null)
    {
        _cacheRoot = cacheRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MYVOCAL Studio",
            "ProjectCache");
    }

    public async Task SaveAsync(StudioProject project, string path, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        try
        {
            {
                await using var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 65_536, true);
                using var archive = new ZipArchive(output, ZipArchiveMode.Create, true);
                var portable = Clone(project);
                foreach (var clip in portable.Tracks.SelectMany(track => track.Clips)
                             .Where(clip => !string.IsNullOrWhiteSpace(clip.SourcePath)))
                {
                    token.ThrowIfCancellationRequested();
                    var original = project.Tracks.SelectMany(track => track.Clips).Single(item => item.Id == clip.Id);
                    if (!File.Exists(original.SourcePath))
                        throw new FileNotFoundException($"프로젝트에 포함할 오디오를 찾을 수 없습니다: {original.SourcePath}", original.SourcePath);
                    var extension = Path.GetExtension(original.SourcePath).ToLowerInvariant();
                    var assetName = $"assets/{clip.Id:N}{extension}";
                    var asset = archive.CreateEntry(assetName, CompressionLevel.Optimal);
                    await using var source = new FileStream(original.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
                    await using var destination = asset.Open();
                    await source.CopyToAsync(destination, token);
                    clip.SourcePath = assetName;
                }
                var manifest = archive.CreateEntry(ManifestName, CompressionLevel.Optimal);
                await using (var manifestStream = manifest.Open())
                    await JsonSerializer.SerializeAsync(manifestStream, portable, Options, token);
            }
            File.Move(temporary, path, true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    public async Task<StudioProject> LoadAsync(string path, CancellationToken token = default)
    {
        await using var input = File.OpenRead(path);
        var signature = new byte[4];
        if (await input.ReadAsync(signature, token) != signature.Length)
            throw new InvalidDataException("비어 있거나 손상된 MYVOCAL 프로젝트입니다.");
        input.Position = 0;
        if (signature[0] != (byte)'P' || signature[1] != (byte)'K')
            return await LoadLegacyJsonAsync(input, token);

        using var archive = new ZipArchive(input, ZipArchiveMode.Read, true);
        var manifest = archive.GetEntry(ManifestName)
                       ?? throw new InvalidDataException("프로젝트 manifest가 없습니다.");
        StudioProject project;
        await using (var manifestStream = manifest.Open())
            project = await DeserializeAsync(manifestStream, token);

        var cache = Path.Combine(_cacheRoot, ComputeProjectKey(path));
        Directory.CreateDirectory(cache);
        foreach (var clip in project.Tracks.SelectMany(track => track.Clips)
                     .Where(clip => !string.IsNullOrWhiteSpace(clip.SourcePath)))
        {
            token.ThrowIfCancellationRequested();
            var normalized = clip.SourcePath!.Replace('\\', '/');
            if (!normalized.StartsWith("assets/", StringComparison.Ordinal) || normalized.Contains("..", StringComparison.Ordinal))
                throw new InvalidDataException("안전하지 않은 프로젝트 에셋 경로입니다.");
            var entry = archive.GetEntry(normalized)
                        ?? throw new InvalidDataException($"프로젝트 에셋이 없습니다: {normalized}");
            var destination = Path.Combine(cache, Path.GetFileName(normalized));
            await using var source = entry.Open();
            await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 65_536, true);
            await source.CopyToAsync(target, token);
            clip.SourcePath = destination;
        }
        return project;
    }

    private static async Task<StudioProject> LoadLegacyJsonAsync(Stream input, CancellationToken token) =>
        await DeserializeAsync(input, token);

    private static async Task<StudioProject> DeserializeAsync(Stream input, CancellationToken token)
    {
        var project = await JsonSerializer.DeserializeAsync<StudioProject>(input, Options, token);
        if (project is null || project.SchemaVersion != 1)
            throw new InvalidDataException("지원하지 않거나 손상된 MYVOCAL 프로젝트입니다.");
        return project;
    }

    private static StudioProject Clone(StudioProject project) =>
        JsonSerializer.Deserialize<StudioProject>(JsonSerializer.Serialize(project, Options), Options)
        ?? throw new InvalidDataException("프로젝트를 패키징할 수 없습니다.");

    private static string ComputeProjectKey(string path)
    {
        var identity = $"{Path.GetFullPath(path)}|{File.GetLastWriteTimeUtc(path).Ticks}";
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity)))[..20];
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

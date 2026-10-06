using System.Text.Json;
using MyVocalStudio.Models;
namespace MyVocalStudio.Services;
public sealed class ProjectService
{
    private static readonly JsonSerializerOptions Options=new(){WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    public async Task SaveAsync(StudioProject project,string path,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(project); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary=path+".tmp"; await using(var stream=File.Create(temporary)) await JsonSerializer.SerializeAsync(stream,project,Options,token);
        File.Move(temporary,path,true);
    }
    public async Task<StudioProject> LoadAsync(string path,CancellationToken token=default)
    {
        await using var stream=File.OpenRead(path); var result=await JsonSerializer.DeserializeAsync<StudioProject>(stream,Options,token);
        if(result is null||result.SchemaVersion!=1) throw new InvalidDataException("지원하지 않거나 손상된 MYVOCAL 프로젝트입니다."); return result;
    }
}

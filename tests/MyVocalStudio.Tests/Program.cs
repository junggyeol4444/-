using System.Text;
using System.Text.Json;
using MyVocalStudio.Models;
using MyVocalStudio.Services;

var temp=Path.Combine(Path.GetTempPath(),"myvocal-tests-"+Guid.NewGuid());Directory.CreateDirectory(temp);
try
{
    var renderer=new WaveRenderer();var wave=Path.Combine(temp,"test.wav");await renderer.RenderAsync(wave,120,1);
    await using(var file=File.OpenRead(wave)){var header=new byte[44];await file.ReadExactlyAsync(header);Assert(Encoding.ASCII.GetString(header,0,4)=="RIFF","RIFF header");Assert(Encoding.ASCII.GetString(header,8,4)=="WAVE","WAVE format");Assert(BitConverter.ToInt16(header,22)==2,"stereo channels");Assert(BitConverter.ToInt32(header,24)==WaveRenderer.SampleRate,"sample rate");Assert(file.Length>WaveRenderer.SampleRate*4,"audio frames");}
    var imported=new PcmWaveReader().Read(wave);Assert(imported.SampleRate==WaveRenderer.SampleRate,"import sample rate");Assert(imported.Duration>1.9,"import duration");
    var service=new ProjectService(Path.Combine(temp,"cache"));var source=new StudioProject{Name="Roundtrip",Tracks=[new(){Name="Vocal",Kind=TrackKind.Vocal,Clips=[new(){Name="Verse",Start=4,Length=8}]}]};var projectFile=Path.Combine(temp,"project.myvocal");await service.SaveAsync(source,projectFile);var loaded=await service.LoadAsync(projectFile);Assert(loaded.Name==source.Name,"project name");Assert(loaded.Tracks.Single().Clips.Single().Start==4,"clip roundtrip");Assert((await File.ReadAllBytesAsync(projectFile)).Take(2).SequenceEqual(new byte[]{(byte)'P',(byte)'K'}),"portable project package");
    var legacyFile=Path.Combine(temp,"legacy.myvocal");await File.WriteAllTextAsync(legacyFile,JsonSerializer.Serialize(new StudioProject{Name="Legacy"},new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase}));Assert((await service.LoadAsync(legacyFile)).Name=="Legacy","legacy JSON compatibility");
    var mutedFile=Path.Combine(temp,"muted.wav");source.Tracks.Single().IsMuted=true;await renderer.RenderProjectAsync(source,mutedFile,1);var mutedBytes=await File.ReadAllBytesAsync(mutedFile);Assert(mutedBytes.Skip(44).All(value=>value==0),"muted track renders silence");
    source.Tracks.Single().IsMuted=false;source.Bpm=0;await AssertThrows<ArgumentOutOfRangeException>(()=>renderer.RenderProjectAsync(source,Path.Combine(temp,"invalid.wav"),1),"invalid BPM rejected");
    source.Bpm=120;source.Tracks=[new(){Name="Imported Audio",Kind=TrackKind.Audio,Clips=[new(){Name="Imported",Length=1,SourcePath=wave}]}];var mixedFile=Path.Combine(temp,"mixed.wav");await renderer.RenderProjectAsync(source,mixedFile,1);var mixedBytes=await File.ReadAllBytesAsync(mixedFile);Assert(mixedBytes.Skip(44).Any(value=>value!=0),"imported PCM mixed into project");
    var portableFile=Path.Combine(temp,"portable.myvocal");await service.SaveAsync(source,portableFile);File.Delete(wave);var portable=await service.LoadAsync(portableFile);Assert(File.Exists(portable.Tracks.Single().Clips.Single().SourcePath),"embedded audio extracted");Assert(new PcmWaveReader().Read(portable.Tracks.Single().Clips.Single().SourcePath!).Duration>.9,"embedded audio readable");source=portable;
    var history=new ProjectHistory(3);history.Record(source);source.Name="Edited";var undone=history.Undo(source)!;Assert(undone.Name=="Roundtrip","history undo");var redone=history.Redo(undone)!;Assert(redone.Name=="Edited","history redo");
    var recovery=new RecoveryService(service,Path.Combine(temp,"recovery"));await recovery.SaveAsync(redone);Assert(recovery.GetRecoveryInfo() is { Size:>0 },"recovery snapshot created");var recovered=await recovery.LoadAsync();Assert(recovered.Name=="Edited","recovery snapshot loaded");recovery.Clear();Assert(recovery.GetRecoveryInfo() is null,"recovery snapshot cleared");
    Console.WriteLine("All MYVOCAL smoke tests passed.");return 0;
}
finally{Directory.Delete(temp,true);}
static void Assert(bool condition,string name){if(!condition)throw new InvalidOperationException($"Failed: {name}");Console.WriteLine($"PASS: {name}");}
static async Task AssertThrows<T>(Func<Task> action,string name) where T:Exception{try{await action();}catch(T){Console.WriteLine($"PASS: {name}");return;}throw new InvalidOperationException($"Failed: {name}");}

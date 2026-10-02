using System.Text;
using MyVocalStudio.Models;
using MyVocalStudio.Services;

var temp=Path.Combine(Path.GetTempPath(),"myvocal-tests-"+Guid.NewGuid());Directory.CreateDirectory(temp);
try
{
    var renderer=new WaveRenderer();var wave=Path.Combine(temp,"test.wav");await renderer.RenderAsync(wave,120,1);
    await using(var file=File.OpenRead(wave)){var header=new byte[44];await file.ReadExactlyAsync(header);Assert(Encoding.ASCII.GetString(header,0,4)=="RIFF","RIFF header");Assert(Encoding.ASCII.GetString(header,8,4)=="WAVE","WAVE format");Assert(BitConverter.ToInt16(header,22)==2,"stereo channels");Assert(BitConverter.ToInt32(header,24)==WaveRenderer.SampleRate,"sample rate");Assert(file.Length>WaveRenderer.SampleRate*4,"audio frames");}
    var service=new ProjectService();var source=new StudioProject{Name="Roundtrip",Tracks=[new(){Name="Vocal",Kind=TrackKind.Vocal,Clips=[new(){Name="Verse",Start=4,Length=8}]}]};var projectFile=Path.Combine(temp,"project.myvocal");await service.SaveAsync(source,projectFile);var loaded=await service.LoadAsync(projectFile);Assert(loaded.Name==source.Name,"project name");Assert(loaded.Tracks.Single().Clips.Single().Start==4,"clip roundtrip");
    var mutedFile=Path.Combine(temp,"muted.wav");source.Tracks.Single().IsMuted=true;await renderer.RenderProjectAsync(source,mutedFile,1);var mutedBytes=await File.ReadAllBytesAsync(mutedFile);Assert(mutedBytes.Skip(44).All(value=>value==0),"muted track renders silence");
    source.Tracks.Single().IsMuted=false;source.Bpm=0;await AssertThrows<ArgumentOutOfRangeException>(()=>renderer.RenderProjectAsync(source,Path.Combine(temp,"invalid.wav"),1),"invalid BPM rejected");
    Console.WriteLine("All MYVOCAL smoke tests passed.");return 0;
}
finally{Directory.Delete(temp,true);}
static void Assert(bool condition,string name){if(!condition)throw new InvalidOperationException($"Failed: {name}");Console.WriteLine($"PASS: {name}");}
static async Task AssertThrows<T>(Func<Task> action,string name) where T:Exception{try{await action();}catch(T){Console.WriteLine($"PASS: {name}");return;}throw new InvalidOperationException($"Failed: {name}");}

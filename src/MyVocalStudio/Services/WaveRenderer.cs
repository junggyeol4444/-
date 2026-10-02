using System.Text;
namespace MyVocalStudio.Services;
public sealed class WaveRenderer
{
    public const int SampleRate=44100;
    public async Task RenderAsync(string path,double bpm,int bars=8,IProgress<double>? progress=null,CancellationToken token=default)
    {
        if(bpm is <40 or >300) throw new ArgumentOutOfRangeException(nameof(bpm));
        var beat=60d/bpm; var seconds=bars*4*beat; var frames=(int)(seconds*SampleRate); var dataBytes=frames*4;
        await using var stream=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None,65536,true); using var writer=new BinaryWriter(stream,Encoding.ASCII,true);
        WriteHeader(writer,dataBytes); var chords=new[]{new[]{220d,261.63,329.63},new[]{174.61,220d,261.63},new[]{130.81,164.81,196d},new[]{196d,246.94,293.66}};
        var buffer=new byte[65536]; var offset=0;
        for(var i=0;i<frames;i++)
        {
            token.ThrowIfCancellationRequested(); var t=i/(double)SampleRate;var chord=chords[(int)(t/(beat*2))%chords.Length];var phase=t%beat;
            var env=Math.Min(1,phase*20)*Math.Exp(-phase*1.2);var melodic=chord.Sum(f=>Math.Sin(2*Math.PI*f*t))/3;var bass=Math.Sin(2*Math.PI*chord[0]/2*t);var kick=Math.Sin(2*Math.PI*(75-35*phase)*t)*Math.Exp(-phase*18);var sample=Math.Clamp((melodic*.25+bass*.18)*env+kick*.28,-1,1);var value=(short)(sample*22000);
            BitConverter.TryWriteBytes(buffer.AsSpan(offset,2),value);BitConverter.TryWriteBytes(buffer.AsSpan(offset+2,2),value);offset+=4;
            if(offset>buffer.Length-4){await stream.WriteAsync(buffer.AsMemory(0,offset),token);offset=0;} if(i%(SampleRate/4)==0)progress?.Report(i/(double)frames);
        }
        if(offset>0)await stream.WriteAsync(buffer.AsMemory(0,offset),token);progress?.Report(1);
    }
    private static void WriteHeader(BinaryWriter w,int bytes){w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+bytes);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)2);w.Write(SampleRate);w.Write(SampleRate*4);w.Write((short)4);w.Write((short)16);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(bytes);}
}

using System.Text;
using MyVocalStudio.Models;

namespace MyVocalStudio.Services;

/// <summary>Dependency-free offline renderer used for preview and WAV export.</summary>
public sealed class WaveRenderer
{
    public const int SampleRate = 44_100;

    public Task RenderAsync(
        string path,
        double bpm,
        int bars = 8,
        IProgress<double>? progress = null,
        CancellationToken token = default)
    {
        var project = new StudioProject { Bpm = bpm };
        project.Tracks.Add(new TrackModel
        {
            Name = "Demo Instrument",
            Kind = TrackKind.Instrument,
            Clips = [new ClipModel { Name = "Demo", Length = bars * 4 * 60 / bpm }]
        });
        return RenderProjectAsync(project, path, bars * 4 * 60 / bpm, progress, token);
    }

    public async Task RenderProjectAsync(
        StudioProject project,
        string path,
        double? durationSeconds = null,
        IProgress<double>? progress = null,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Bpm is < 40 or > 300)
            throw new ArgumentOutOfRangeException(nameof(project.Bpm), "BPM must be between 40 and 300.");

        var duration = durationSeconds ?? Math.Max(8, project.Tracks
            .SelectMany(track => track.Clips)
            .Select(clip => clip.Start + clip.Length)
            .DefaultIfEmpty(8)
            .Max());
        var frameCount = checked((int)(duration * SampleRate));
        var dataBytes = checked(frameCount * 4);
        var hasSolo = project.Tracks.Any(track => track.IsSolo);
        var audibleTracks = project.Tracks
            .Where(track => !track.IsMuted && (!hasSolo || track.IsSolo))
            .ToArray();

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 65_536, true);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        WriteHeader(writer, dataBytes);

        var buffer = new byte[65_536];
        var offset = 0;
        for (var frame = 0; frame < frameCount; frame++)
        {
            token.ThrowIfCancellationRequested();
            var time = frame / (double)SampleRate;
            var sample = 0d;
            foreach (var track in audibleTracks)
            {
                if (!track.Clips.Any(clip => time >= clip.Start && time < clip.Start + clip.Length))
                    continue;
                sample += RenderTrack(track, time, project.Bpm) * track.Volume;
            }

            // Soft saturation prevents clipping when many tracks are active.
            var value = (short)(Math.Tanh(sample * .72) * 25_000);
            BitConverter.TryWriteBytes(buffer.AsSpan(offset, 2), value);
            BitConverter.TryWriteBytes(buffer.AsSpan(offset + 2, 2), value);
            offset += 4;
            if (offset > buffer.Length - 4)
            {
                await stream.WriteAsync(buffer.AsMemory(0, offset), token);
                offset = 0;
            }
            if (frame % (SampleRate / 4) == 0)
                progress?.Report(frame / (double)frameCount);
        }
        if (offset > 0)
            await stream.WriteAsync(buffer.AsMemory(0, offset), token);
        progress?.Report(1);
    }

    private static double RenderTrack(TrackModel track, double time, double bpm)
    {
        var beat = 60 / bpm;
        var beatPhase = time % beat;
        var beatIndex = (int)(time / beat);
        var progression = new[] { 220d, 174.61, 130.81, 196d };
        var root = progression[(beatIndex / 8) % progression.Length];
        return track.Kind switch
        {
            TrackKind.Vocal => Math.Sin(2 * Math.PI * root * 2 * time) *
                               (.45 + .12 * Math.Sin(2 * Math.PI * 5.2 * time)),
            TrackKind.Instrument => (Math.Sin(2 * Math.PI * root * time) +
                                     .5 * Math.Sin(2 * Math.PI * root * 1.25 * time) +
                                     .35 * Math.Sin(2 * Math.PI * root * 1.5 * time)) / 2,
            TrackKind.Audio when track.Name.Contains("Drum", StringComparison.OrdinalIgnoreCase) =>
                Math.Sin(2 * Math.PI * (78 - 35 * beatPhase) * time) * Math.Exp(-beatPhase * 18),
            TrackKind.Audio => Math.Sin(2 * Math.PI * root / 2 * time) * Math.Exp(-beatPhase * 1.4),
            _ => 0
        };
    }

    private static void WriteHeader(BinaryWriter writer, int dataBytes)
    {
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataBytes);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)2);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 4);
        writer.Write((short)4);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataBytes);
    }
}

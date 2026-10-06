using System.Text;

namespace MyVocalStudio.Services;

public sealed record PcmAudioBuffer(int SampleRate, float[] Samples)
{
    public double Duration => Samples.Length / (double)SampleRate;

    public double SampleAt(double seconds)
    {
        var position = seconds * SampleRate;
        if (position < 0 || position >= Samples.Length - 1)
            return 0;
        var index = (int)position;
        var fraction = position - index;
        return Samples[index] * (1 - fraction) + Samples[index + 1] * fraction;
    }
}

/// <summary>Reads uncompressed 16-bit PCM WAV files without a third-party codec package.</summary>
public sealed class PcmWaveReader
{
    public PcmAudioBuffer Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.ASCII, false);
        if (ReadFourCc(reader) != "RIFF" || reader.ReadUInt32() < 36 || ReadFourCc(reader) != "WAVE")
            throw new InvalidDataException("RIFF/WAVE 파일이 아닙니다.");

        ushort format = 0, channels = 0, bits = 0;
        uint sampleRate = 0;
        byte[]? pcm = null;
        while (stream.Position + 8 <= stream.Length)
        {
            var id = ReadFourCc(reader);
            var size = reader.ReadUInt32();
            if (size > int.MaxValue || stream.Position + size > stream.Length)
                throw new InvalidDataException("손상된 WAV chunk입니다.");
            if (id == "fmt ")
            {
                if (size < 16) throw new InvalidDataException("WAV format chunk가 잘못되었습니다.");
                format = reader.ReadUInt16(); channels = reader.ReadUInt16(); sampleRate = reader.ReadUInt32();
                reader.ReadUInt32(); reader.ReadUInt16(); bits = reader.ReadUInt16();
                stream.Position += size - 16;
            }
            else if (id == "data") pcm = reader.ReadBytes((int)size);
            else stream.Position += size;
            if ((size & 1) == 1 && stream.Position < stream.Length) stream.Position++;
        }
        if (format != 1 || bits != 16 || channels is < 1 or > 2 || sampleRate == 0 || pcm is null)
            throw new NotSupportedException("현재는 Mono/Stereo 16-bit PCM WAV만 가져올 수 있습니다.");

        var frameSize = channels * 2;
        var frames = pcm.Length / frameSize;
        var samples = new float[frames];
        for (var frame = 0; frame < frames; frame++)
        {
            var total = 0d;
            for (var channel = 0; channel < channels; channel++)
                total += BitConverter.ToInt16(pcm, frame * frameSize + channel * 2) / 32768d;
            samples[frame] = (float)(total / channels);
        }
        return new PcmAudioBuffer((int)sampleRate, samples);
    }

    private static string ReadFourCc(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));
}

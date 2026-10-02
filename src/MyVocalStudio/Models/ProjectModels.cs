using System.Collections.ObjectModel;
namespace MyVocalStudio.Models;
public sealed class StudioProject
{
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "다시, 우리";
    public double Bpm { get; set; } = 128;
    public string Key { get; set; } = "A min";
    public ObservableCollection<TrackModel> Tracks { get; set; } = [];
    public ObservableCollection<StoryboardScene> Scenes { get; set; } = [];
}
public sealed class TrackModel
{
    public Guid Id { get; set; } = Guid.NewGuid(); public string Name { get; set; } = "New Track";
    public TrackKind Kind { get; set; } public string Color { get; set; } = "#718FEE";
    public double Volume { get; set; } = .75; public bool IsMuted { get; set; } public bool IsSolo { get; set; }
    public ObservableCollection<ClipModel> Clips { get; set; } = [];
}
public sealed class ClipModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Clip";
    public double Start { get; set; }
    public double Length { get; set; } = 8;
    public string? SourcePath { get; set; }
    public double SourceOffset { get; set; }
}
public sealed class StoryboardScene { public int Number { get; set; } public string Title { get; set; }="Scene"; public TimeSpan Start { get; set; } public TimeSpan End { get; set; } }
public enum TrackKind { Vocal, Instrument, Audio, Video }

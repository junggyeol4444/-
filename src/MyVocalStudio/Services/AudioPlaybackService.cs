using System.Windows;
using System.Windows.Media;
using MyVocalStudio.Models;

namespace MyVocalStudio.Services;

/// <summary>Owns native Windows Media Foundation playback for rendered previews.</summary>
public sealed class AudioPlaybackService : IDisposable
{
    private readonly MediaPlayer _player = new();
    private string? _temporaryPreview;

    public bool IsOpen { get; private set; }
    public TimeSpan Position
    {
        get => _player.Position;
        set => _player.Position = value < TimeSpan.Zero ? TimeSpan.Zero : value;
    }

    public double Volume
    {
        get => _player.Volume;
        set => _player.Volume = Math.Clamp(value, 0, 1);
    }

    public async Task PrepareAsync(
        StudioProject project,
        WaveRenderer renderer,
        CancellationToken token = default)
    {
        Stop();
        DeletePreview();
        _temporaryPreview = Path.Combine(Path.GetTempPath(), $"myvocal-preview-{Guid.NewGuid():N}.wav");
        await renderer.RenderProjectAsync(project, _temporaryPreview, token: token);
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler openedHandler = (_, _) => opened.TrySetResult();
        EventHandler<ExceptionEventArgs> failedHandler = (_, args) => opened.TrySetException(args.ErrorException);
        _player.MediaOpened += openedHandler;
        _player.MediaFailed += failedHandler;
        _player.Open(new Uri(_temporaryPreview, UriKind.Absolute));
        try
        {
            await opened.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
            IsOpen = true;
        }
        finally
        {
            _player.MediaOpened -= openedHandler;
            _player.MediaFailed -= failedHandler;
        }
    }

    public void Play() => _player.Play();
    public void Pause() => _player.Pause();
    public void Stop()
    {
        _player.Stop();
        _player.Position = TimeSpan.Zero;
    }

    public void Dispose()
    {
        _player.Close();
        DeletePreview();
        GC.SuppressFinalize(this);
    }

    private void DeletePreview()
    {
        if (_temporaryPreview is null)
            return;
        try { File.Delete(_temporaryPreview); }
        catch (IOException) { /* Media Foundation may still be releasing the file. */ }
        _temporaryPreview = null;
        IsOpen = false;
    }
}

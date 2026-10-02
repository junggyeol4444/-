using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using MyVocalStudio.Models;
using MyVocalStudio.Services;
namespace MyVocalStudio;
public partial class MainWindow:Window
{
    private readonly ProjectService _projects=new(); private readonly ProjectHistory _history=new(); private readonly WaveRenderer _renderer=new(); private readonly PcmWaveReader _waveReader=new(); private readonly AudioPlaybackService _playback=new(); private readonly System.Windows.Threading.DispatcherTimer _timer=new(){Interval=TimeSpan.FromMilliseconds(50)};
    private StudioProject _project=CreateDefault(); private string? _path; private double _position=72; private bool _playing; private bool _isDirty; private bool _previewDirty=true; private CancellationTokenSource? _previewCancellation; private (ClipModel Clip,Border Visual,double StartX,double OldStart)? _drag; private const double Scale=14;
    public MainWindow(){InitializeComponent();_history.Changed+=(_,_)=>UpdateHistoryButtons();PreviewKeyDown+=Window_PreviewKeyDown;Closing+=Window_Closing;_timer.Tick+=(_,_)=>{if(_playing){_position=_playback.IsOpen?_playback.Position.TotalSeconds:(_position+.05)%100;UpdatePlayhead();}};_timer.Start();Closed+=(_,_)=>{_previewCancellation?.Cancel();_playback.Dispose();};RenderAll();UpdateHistoryButtons();}
    private static StudioProject CreateDefault()=>new(){Tracks=
    [
      new(){Name="Lead Vocal",Kind=TrackKind.Vocal,Color="#FF7168",Clips=[new(){Name="Verse 1",Start=8,Length=16},new(){Name="Chorus",Start=28,Length=22},new(){Name="Verse 2",Start=54,Length=18}]},
      new(){Name="Harmony",Kind=TrackKind.Vocal,Color="#B96BE5",Clips=[new(){Name="Harmony",Start=30,Length=18},new(){Name="Final Harmony",Start=72,Length=15}]},
      new(){Name="Drums",Kind=TrackKind.Audio,Color="#59C5D5",Clips=[new(){Name="Anime Rock Kit",Length=26},new(){Name="Power Chorus",Start=27,Length=24},new(){Name="Verse Groove",Start=52,Length=25}]},
      new(){Name="Bass",Kind=TrackKind.Instrument,Color="#708FEE",Clips=[new(){Name="Bassline",Start=8,Length=18},new(){Name="Bassline",Start=28,Length=22}]},
      new(){Name="Electric Guitar",Kind=TrackKind.Instrument,Color="#E39A52",Clips=[new(){Name="Clean Arp",Start=8,Length=18},new(){Name="Power Chords",Start=28,Length=23}]},
      new(){Name="Strings",Kind=TrackKind.Instrument,Color="#DB6C99",Clips=[new(){Name="Sustain",Start=17,Length=10},new(){Name="Epic Strings",Start=29,Length=22}]}
    ],Scenes=[new(){Number=1,Title="도시 야경",End=TimeSpan.FromSeconds(12)},new(){Number=2,Title="LUNA 등장",Start=TimeSpan.FromSeconds(12),End=TimeSpan.FromSeconds(28)},new(){Number=3,Title="Close-up",Start=TimeSpan.FromSeconds(28),End=TimeSpan.FromSeconds(45)}]};
    private void RenderAll(){ProjectTitle.Text=_project.Name+(_isDirty?"  •":"");RenderTracks();RenderStoryboard();UpdatePlayhead();}
    private void RenderTracks(){_previewDirty=true;TrackHeaders.Items.Clear();Timeline.Children.Clear();Timeline.Height=Math.Max(500,28+_project.Tracks.Count*58);
      for(var n=0;n<101;n+=8){var x=n*Scale;Timeline.Children.Add(new Line{X1=x,X2=x,Y1=0,Y2=Timeline.Height,Stroke=new SolidColorBrush(Color.FromRgb(41,42,49)),StrokeThickness=1});AddText(Timeline,(n/2+1).ToString(),x+5,7,Brushes.Gray);}
      for(var i=0;i<_project.Tracks.Count;i++){var t=_project.Tracks[i];var row=new Grid{Height=58,Background=new SolidColorBrush(Color.FromRgb(17,18,24))};row.ColumnDefinitions.Add(new(){Width=new(4)});row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=new(34)});row.ColumnDefinitions.Add(new(){Width=new(34)});row.Children.Add(new Border{Background=(Brush)new BrushConverter().ConvertFromString(t.Color)!});var label=new StackPanel{Margin=new Thickness(9,8,0,0)};label.Children.Add(new TextBlock{Text=t.Name,FontWeight=FontWeights.SemiBold});label.Children.Add(new TextBlock{Text=t.Kind.ToString().ToUpperInvariant(),FontSize=8,Foreground=Brushes.Gray});Grid.SetColumn(label,1);row.Children.Add(label);var mute=new Button{Content="M",Foreground=t.IsMuted?Brushes.OrangeRed:Brushes.Gray};mute.Click+=(_,_)=>{RecordChange();t.IsMuted=!t.IsMuted;SetDirty();RenderTracks();};Grid.SetColumn(mute,2);row.Children.Add(mute);var solo=new Button{Content="S",Foreground=t.IsSolo?Brushes.Violet:Brushes.Gray};solo.Click+=(_,_)=>{RecordChange();t.IsSolo=!t.IsSolo;SetDirty();RenderTracks();};Grid.SetColumn(solo,3);row.Children.Add(solo);TrackHeaders.Items.Add(row);
        var y=28+i*58;var lane=new Rectangle{Width=1500,Height=58,Fill=new SolidColorBrush(i%2==0?Color.FromRgb(17,18,23):Color.FromRgb(14,15,20))};Canvas.SetTop(lane,y);Timeline.Children.Add(lane);
        var volume=new Slider{Minimum=0,Maximum=1,Value=t.Volume,Width=72,Height=14,Margin=new Thickness(0,1,0,0),ToolTip="Track volume"};volume.PreviewMouseLeftButtonDown+=(_,_)=>RecordChange();volume.ValueChanged+=(_,args)=>{t.Volume=args.NewValue;SetDirty();_previewDirty=true;};label.Children.Add(volume);
        foreach(var c in t.Clips){var border=new Border{Width=Math.Max(35,c.Length*Scale),Height=44,Background=WithAlpha(t.Color,55),BorderBrush=(Brush)new BrushConverter().ConvertFromString(t.Color)!,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(3),Tag=c,Child=new TextBlock{Text=c.Name,Foreground=(Brush)new BrushConverter().ConvertFromString(t.Color)!,FontSize=10,FontWeight=FontWeights.SemiBold,Margin=new Thickness(7)}};border.MouseLeftButtonDown+=Clip_Down;Canvas.SetLeft(border,c.Start*Scale);Canvas.SetTop(border,y+7);Timeline.Children.Add(border);}}
      var playhead=new Line{Name="Playhead",X1=_position*Scale,X2=_position*Scale,Y1=0,Y2=Timeline.Height,Stroke=new SolidColorBrush(Color.FromRgb(255,108,98)),StrokeThickness=1};Timeline.Children.Add(playhead);}
    private static Brush WithAlpha(string hex,byte alpha){var c=(Color)ColorConverter.ConvertFromString(hex);return new SolidColorBrush(Color.FromArgb(alpha,c.R,c.G,c.B));}
    private static void AddText(Canvas c,string text,double x,double y,Brush color){var t=new TextBlock{Text=text,Foreground=color,FontFamily=new FontFamily("Consolas"),FontSize=9};Canvas.SetLeft(t,x);Canvas.SetTop(t,y);c.Children.Add(t);}
    private void Clip_Down(object s,MouseButtonEventArgs e){if(s is Border{Tag:ClipModel c} b){RecordChange();_drag=(c,b,e.GetPosition(Timeline).X,c.Start);b.CaptureMouse();e.Handled=true;}}
    private void Timeline_MouseMove(object s,MouseEventArgs e){if(_drag is not { } d||e.LeftButton!=MouseButtonState.Pressed)return;var next=Math.Max(0,d.OldStart+(e.GetPosition(Timeline).X-d.StartX)/Scale);d.Clip.Start=SnapCheck.IsChecked==true?Math.Round(next/2)*2:next;Canvas.SetLeft(d.Visual,d.Clip.Start*Scale);_previewDirty=true;}
    private void Timeline_MouseLeftButtonUp(object s,MouseButtonEventArgs e){if(_drag is not null)SetDirty();_drag=null;Mouse.Capture(null);}
    private void Timeline_MouseLeftButtonDown(object s,MouseButtonEventArgs e){if(_drag is null){_position=Math.Max(0,e.GetPosition(Timeline).X/Scale);UpdatePlayhead();}}
    private void UpdatePlayhead(){TimeText.Text=$"{(int)(_position/60):00}:{(int)(_position%60):00}.{(int)(_position%1*1000):000}";var line=Timeline.Children.OfType<Line>().LastOrDefault(x=>x.Name=="Playhead");if(line!=null)line.X1=line.X2=_position*Scale;}
    private void RenderStoryboard(){Storyboard.Items.Clear();foreach(var s in _project.Scenes){var p=new StackPanel{Width=150,Height=120,Margin=new Thickness(10),Background=new SolidColorBrush(Color.FromRgb(24,25,32))};p.Children.Add(new TextBlock{Text=$"{s.Number:00}",Foreground=Brushes.OrangeRed,Margin=new Thickness(10)});p.Children.Add(new TextBlock{Text=s.Title,FontWeight=FontWeights.SemiBold,Margin=new Thickness(10,35,10,0)});p.Children.Add(new TextBlock{Text=$"{s.Start:mm\\:ss}–{s.End:mm\\:ss}",Foreground=Brushes.Gray,Margin=new Thickness(10,4,10,0)});Storyboard.Items.Add(p);}}
    private async void Play_Click(object s,RoutedEventArgs e)
    {
        try
        {
            if(_playing){_playback.Pause();_playing=false;PlayButton.Content="▶";return;}
            if(_previewDirty||!_playback.IsOpen)
            {
                PlayButton.IsEnabled=false;PlayButton.Content="…";_previewCancellation?.Cancel();_previewCancellation=new CancellationTokenSource();
                await _playback.PrepareAsync(_project,_renderer,_previewCancellation.Token);_previewDirty=false;
            }
            _playback.Position=TimeSpan.FromSeconds(_position);_playback.Play();_playing=true;PlayButton.Content="Ⅱ";
        }
        catch(OperationCanceledException){PlayButton.Content="▶";}
        catch(Exception ex){MessageBox.Show(ex.Message,"미리듣기 실패",MessageBoxButton.OK,MessageBoxImage.Error);PlayButton.Content="▶";}
        finally{PlayButton.IsEnabled=true;}
    }
    private void Stop_Click(object s,RoutedEventArgs e){_playback.Stop();_playing=false;_position=0;PlayButton.Content="▶";UpdatePlayhead();} private void Rewind_Click(object s,RoutedEventArgs e){_position=Math.Max(0,_position-4);if(_playback.IsOpen)_playback.Position=TimeSpan.FromSeconds(_position);UpdatePlayhead();}
    private void AddTrack_Click(object s,RoutedEventArgs e){RecordChange();_project.Tracks.Add(new(){Name="New Instrument",Kind=TrackKind.Instrument,Color="#718FEE"});SetDirty();RenderTracks();}
    private void ImportAudio_Click(object s,RoutedEventArgs e)
    {
        var dialog=new OpenFileDialog{Filter="PCM WAV Audio|*.wav",Multiselect=true};if(dialog.ShowDialog()!=true)return;
        try
        {
            RecordChange();foreach(var path in dialog.FileNames)
            {
                var audio=_waveReader.Read(path);var track=new TrackModel{Name=Path.GetFileNameWithoutExtension(path),Kind=TrackKind.Audio,Color="#59C5D5"};
                track.Clips.Add(new ClipModel{Name=Path.GetFileName(path),Start=_position,Length=audio.Duration,SourcePath=Path.GetFullPath(path)});_project.Tracks.Add(track);
            }
            SetDirty();RenderTracks();
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"오디오 가져오기 실패",MessageBoxButton.OK,MessageBoxImage.Error);}
    }
    private void Rewrite_Click(object s,RoutedEventArgs e)=>LyricsText.Text="기억의 저편에서 네 목소리가 와\n\n멈춘 계절 사이로 다시 피어난 우리\n\n이제는 놓치지 않을게";
    private void Drums_Click(object s,RoutedEventArgs e)=>ApplyAi("후렴 드럼을 강화해줘");private void Harmony_Click(object s,RoutedEventArgs e)=>ApplyAi("후렴에 화음을 추가해줘");private void AiApply_Click(object s,RoutedEventArgs e)=>ApplyAi(AiPrompt.Text);
    private void ApplyAi(string prompt){RecordChange();var answer="요청을 프로젝트에 적용했습니다.";if(prompt.Contains("드럼")){_project.Tracks.First(x=>x.Name=="Drums").Clips.Add(new(){Name="AI Power Drums",Start=76,Length=16});answer="마지막 후렴에 파워 드럼과 크래시를 추가했습니다.";}else if(prompt.Contains("화음")){_project.Tracks.First(x=>x.Name=="Harmony").Clips.Add(new(){Name="AI Harmony",Start=76,Length=15});answer="마지막 후렴에 3도 위 화음을 추가했습니다.";}else if(prompt.Contains("반키"))answer="마지막 후렴의 조성을 반키 올리는 편집 지시를 등록했습니다.";ChatLog.AppendText($"\n\n나: {prompt}\n✦ {answer}");ChatLog.ScrollToEnd();SetDirty();RenderTracks();}
    private async void Save_Click(object s,RoutedEventArgs e){if(_path is null){var d=new SaveFileDialog{Filter="MYVOCAL Project|*.myvocal",FileName=_project.Name};if(d.ShowDialog()!=true)return;_path=d.FileName;}try{await _projects.SaveAsync(_project,_path);_isDirty=false;RenderAll();MessageBox.Show("프로젝트를 안전하게 저장했습니다.","MYVOCAL Studio");}catch(Exception ex){MessageBox.Show(ex.Message,"저장 실패",MessageBoxButton.OK,MessageBoxImage.Error);}}
    private async void Open_Click(object s,RoutedEventArgs e){var d=new OpenFileDialog{Filter="MYVOCAL Project|*.myvocal"};if(d.ShowDialog()!=true)return;try{_project=await _projects.LoadAsync(d.FileName);_path=d.FileName;_history.Clear();_isDirty=false;RenderAll();}catch(Exception ex){MessageBox.Show(ex.Message,"열기 실패",MessageBoxButton.OK,MessageBoxImage.Error);}}
    private async void Export_Click(object s,RoutedEventArgs e){var d=new SaveFileDialog{Filter="WAV Audio|*.wav",FileName=_project.Name};if(d.ShowDialog()!=true)return;try{IsEnabled=false;await _renderer.RenderProjectAsync(_project,d.FileName);MessageBox.Show("현재 트랙의 Mute, Solo, Volume과 클립 배치를 반영한 44.1 kHz 스테레오 WAV 렌더링이 완료되었습니다.","내보내기 완료");}catch(Exception ex){MessageBox.Show(ex.Message,"렌더링 실패",MessageBoxButton.OK,MessageBoxImage.Error);}finally{IsEnabled=true;}}
    private void RecordChange()=>_history.Record(_project);
    private void SetDirty(){_isDirty=true;ProjectTitle.Text=_project.Name+"  •";}
    private void Undo_Click(object s,RoutedEventArgs e){var restored=_history.Undo(_project);if(restored is null)return;_project=restored;SetDirty();RenderAll();}
    private void Redo_Click(object s,RoutedEventArgs e){var restored=_history.Redo(_project);if(restored is null)return;_project=restored;SetDirty();RenderAll();}
    private void UpdateHistoryButtons(){UndoButton.IsEnabled=_history.CanUndo;RedoButton.IsEnabled=_history.CanRedo;}
    private void Window_PreviewKeyDown(object s,KeyEventArgs e)
    {
        if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&((e.Key==Key.Z&&Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))||e.Key==Key.Y)){Redo_Click(this,new RoutedEventArgs());e.Handled=true;}
        else if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&e.Key==Key.Z){Undo_Click(this,new RoutedEventArgs());e.Handled=true;}
        else if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&e.Key==Key.S){Save_Click(this,new RoutedEventArgs());e.Handled=true;}
        else if(e.Key==Key.Space&&e.OriginalSource is not TextBox){Play_Click(this,new RoutedEventArgs());e.Handled=true;}
    }
    private void Window_Closing(object? sender,System.ComponentModel.CancelEventArgs e)
    {
        if(!_isDirty)return;
        var result=MessageBox.Show("저장하지 않은 변경사항이 있습니다. 그래도 종료하시겠습니까?","MYVOCAL Studio",MessageBoxButton.YesNo,MessageBoxImage.Warning);
        if(result!=MessageBoxResult.Yes)e.Cancel=true;
    }
    private void PianoRoll_Loaded(object s,RoutedEventArgs e){for(var i=0;i<14;i++){var r=new Rectangle{Width=34+(i%3)*12,Height=10,Fill=Brushes.MediumPurple};Canvas.SetLeft(r,85+i*53);Canvas.SetTop(r,15+(i%6)*19);PianoRoll.Children.Add(r);}}
    private void Automation_Loaded(object s,RoutedEventArgs e){Automation.Children.Add(new Polyline{Points=new PointCollection([new(0,120),new(150,60),new(330,105),new(520,35),new(800,52)]),Stroke=Brushes.MediumPurple,StrokeThickness=2});}
}

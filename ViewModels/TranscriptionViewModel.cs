using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Timer = System.Timers.Timer;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Vexa.Converters;
using Vexa.Models;
using Vexa.Services;

namespace Vexa.ViewModels;

public sealed class TranscriptionViewModel : INotifyPropertyChanged
{
    private readonly AudioEngine _audio;
    private readonly SrtService _srtService;
    private readonly ExportService _exportService;
    private readonly JsonSessionService _jsonService;
    private readonly ShortcutService _shortcutService = new();
    private readonly WaveformGenerator _waveformGenerator = new();
    private readonly Timer _autosaveTimer;

    private Segment? _selectedSegment;
    private Segment? _activeSegment;
    private string _sessionPath = string.Empty;
    private WaveformData? _waveformData;
    private double _zoomLevel = 1.0;
    private string _audioPath = string.Empty;
    private string _status = "Ready";
    private double _playbackSpeed = 1.0;
    private double _volume = 0.9;
    private double _rewindSeconds = 2.0;
    private bool _isDirty;
    private bool _isSeeking;
    private ShortcutProfile _shortcutProfile = ShortcutProfile.CreateDefaults();
    private ObservableCollection<string> _playlist = new();
    private int _playlistIndex = -1;
    private bool _isPlaylistVisible;

    public TranscriptionViewModel(AudioEngine audio, SrtService srtService, ExportService exportService, JsonSessionService jsonService)
    {
        _audio = audio;
        _srtService = srtService;
        _exportService = exportService;
        _jsonService = jsonService;

        _shortcutProfile = _shortcutService.Load();
        Session = new TranscriptionSession();
        
        // Ensure at least one segment if empty to allow typing/pasting
        if (Session.Segments.Count == 0)
        {
            var firstSegment = new Segment { StartSeconds = 0, EndSeconds = 0, Text = string.Empty };
            AttachSegment(firstSegment);
            Session.Segments.Add(firstSegment);
        }

        OpenAudioCommand = new RelayCommand(OpenAudio);
        OpenSessionCommand = new RelayCommand(OpenSession);
        SaveCommand = new RelayCommand(SaveSession, () => Session.Segments.Count > 0);
        SaveAsCommand = new RelayCommand(SaveSessionAs, () => Session.Segments.Count > 0);
        ExportCommand = new RelayCommand(ExportSession, () => Session.Segments.Count > 0);
        AddSegmentCommand = new RelayCommand(AddSegmentAtPlayhead, () => _audio.HasAudio);
        RemoveSegmentCommand = new RelayCommand(RemoveSelectedSegment, () => SelectedSegment != null);
        SplitSegmentCommand = new RelayCommand(SplitSelectedSegment, () => SelectedSegment != null);
        MergeNextCommand = new RelayCommand(MergeWithNext, () => SelectedSegment != null);
        PlayPauseCommand = new RelayCommand(TogglePlay, () => _audio.HasAudio);
        RewindCommand = new RelayCommand(() => _audio.Rewind(), () => _audio.HasAudio);
        SlowDownCommand = new RelayCommand(() => PlaybackSpeed = Math.Max(0.5, PlaybackSpeed - 0.1), () => _audio.HasAudio);
        SpeedUpCommand = new RelayCommand(() => PlaybackSpeed = Math.Min(1.5, PlaybackSpeed + 0.1), () => _audio.HasAudio);
        LoopLastFiveSecondsCommand = new RelayCommand(LoopLastFiveSeconds, () => _audio.HasAudio);
        LoopSelectionCommand = new RelayCommand(LoopSelection, () => SelectedSegment != null);
        ForwardCommand = new RelayCommand(() => _audio.Forward(), () => _audio.HasAudio);
        NextSegmentCommand = new RelayCommand(GoToNextSegment, () => _audio.HasAudio);
        ClearLoopCommand = new RelayCommand(() => _audio.ClearLoop(), () => _audio.HasAudio);
        InsertTimestampCommand = new RelayCommand<RichTextBox>(InsertTimestamp);
        AddMarkerCommand = new RelayCommand<string>(AddMarker, _ => SelectedSegment != null);
        RemoveFromPlaylistCommand = new RelayCommand<string>(path => { if (path != null) Playlist.Remove(path); });
        ClearPlaylistCommand = new RelayCommand(() => { Playlist.Clear(); PlaylistIndex = -1; });
        TogglePlaylistCommand = new RelayCommand(() => IsPlaylistVisible = !IsPlaylistVisible);
        ZoomInCommand = new RelayCommand(() => ZoomLevel = Math.Min(10, ZoomLevel * 1.2), () => WaveformData != null);
        ZoomOutCommand = new RelayCommand(() => ZoomLevel = Math.Max(1, ZoomLevel / 1.2), () => WaveformData != null);
        PasteCommand = new RelayCommand(PasteTranscript);

        _audio.PositionChanged += (_, _) =>
        {
            if (!_isSeeking)
            {
                OnPropertyChanged(nameof(AudioPosition));
                OnPropertyChanged(nameof(AudioPositionSeconds));
                OnPropertyChanged(nameof(AudioPositionLabel));
                OnPropertyChanged(nameof(FormattedCurrentTime));
                UpdateActiveSegment();
            }
        };
        _audio.DurationChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(AudioDuration));
            OnPropertyChanged(nameof(AudioDurationSeconds));
            OnPropertyChanged(nameof(AudioDurationLabel));
            OnPropertyChanged(nameof(FormattedTotalTime));
        };
        _audio.PlaybackStateChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(PlaybackStateLabel));
        };

        _audio.PlaybackEnded += (_, _) => PlayNextInPlaylist();

        _autosaveTimer = new Timer(30_000);
        _autosaveTimer.Elapsed += (_, _) => Autosave();
        _autosaveTimer.AutoReset = true;
        _autosaveTimer.Start();

        _audio.Speed = _playbackSpeed;
        _audio.Volume = _volume;
        _audio.RewindAmount = TimeSpan.FromSeconds(_rewindSeconds);
    }

    public TranscriptionSession Session { get; }

    public ObservableCollection<Segment> Segments => Session.Segments;

    public Segment? SelectedSegment
    {
        get => _selectedSegment;
        set { _selectedSegment = value; OnPropertyChanged(); RaiseCommandStates(); }
    }

    public Segment? ActiveSegment
    {
        get => _activeSegment;
        private set
        {
            if (_activeSegment == value)
            {
                return;
            }

            if (_activeSegment != null)
            {
                _activeSegment.IsActive = false;
            }

            _activeSegment = value;
            if (_activeSegment != null)
            {
                _activeSegment.IsActive = true;
            }

            OnPropertyChanged();
        }
    }

    public string SessionPath
    {
        get => _sessionPath;
        private set { _sessionPath = value; OnPropertyChanged(); }
    }

    public string AudioPath
    {
        get => _audioPath;
        private set { _audioPath = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    public WaveformData? WaveformData
    {
        get => _waveformData;
        private set { _waveformData = value; OnPropertyChanged(); }
    }

    public double ZoomLevel
    {
        get => _zoomLevel;
        set { _zoomLevel = Math.Clamp(value, 1.0, 10.0); OnPropertyChanged(); }
    }

    public bool IsPlaying => _audio.State == PlaybackState.Playing;

    public string PlaybackStateLabel => _audio.State switch
    {
        PlaybackState.Playing => "Playing",
        PlaybackState.Paused => "Paused",
        _ => "Stopped"
    };

    public ObservableCollection<string> Playlist => _playlist;

    public int PlaylistIndex
    {
        get => _playlistIndex;
        set
        {
            if (_playlistIndex != value && value >= 0 && value < _playlist.Count)
            {
                _playlistIndex = value;
                LoadAudioInternal(_playlist[_playlistIndex]);
                OnPropertyChanged();
            }
        }
    }

    public bool IsPlaylistVisible
    {
        get => _isPlaylistVisible;
        set { _isPlaylistVisible = value; OnPropertyChanged(); }
    }

    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            _playbackSpeed = Math.Clamp(value, 0.5, 1.5);
            _audio.Speed = _playbackSpeed;
            OnPropertyChanged();
        }
    }

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0.0, 1.0);
            _audio.Volume = _volume;
            OnPropertyChanged();
        }
    }

    public double RewindSeconds
    {
        get => _rewindSeconds;
        set
        {
            _rewindSeconds = Math.Clamp(value, 0.5, 10.0);
            _audio.RewindAmount = TimeSpan.FromSeconds(_rewindSeconds);
            OnPropertyChanged();
        }
    }

    public TimeSpan AudioPosition
    {
        get => _audio.Position;
        set
        {
            _isSeeking = true;
            _audio.Position = value;
            _isSeeking = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AudioPositionSeconds));
            OnPropertyChanged(nameof(AudioPositionLabel));
        }
    }

    public TimeSpan AudioDuration => _audio.Duration;

    public double AudioPositionSeconds
    {
        get => _audio.Position.TotalSeconds;
        set => AudioPosition = TimeSpan.FromSeconds(Math.Max(0, value));
    }

    public double AudioDurationSeconds => _audio.Duration.TotalSeconds;

    public string AudioPositionLabel => FormatTime(AudioPosition);
    public string AudioDurationLabel => FormatTime(AudioDuration);

    public string FormattedCurrentTime => FormatClock(AudioPosition);
    public string FormattedTotalTime => FormatClock(AudioDuration);

    public RelayCommand OpenAudioCommand { get; }
    public RelayCommand OpenSessionCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveAsCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand AddSegmentCommand { get; }
    public RelayCommand RemoveSegmentCommand { get; }
    public RelayCommand SplitSegmentCommand { get; }
    public RelayCommand MergeNextCommand { get; }
    public RelayCommand PlayPauseCommand { get; }
    public RelayCommand RewindCommand { get; }
    public RelayCommand SlowDownCommand { get; }
    public RelayCommand SpeedUpCommand { get; }
    public RelayCommand LoopLastFiveSecondsCommand { get; }
    public RelayCommand LoopSelectionCommand { get; }
    public RelayCommand ForwardCommand { get; }
    public RelayCommand NextSegmentCommand { get; }
    public RelayCommand ClearLoopCommand { get; }
    public RelayCommand<string> RemoveFromPlaylistCommand { get; }
    public RelayCommand ClearPlaylistCommand { get; }
    public RelayCommand TogglePlaylistCommand { get; }
    public RelayCommand<RichTextBox> InsertTimestampCommand { get; }
    public RelayCommand<string> AddMarkerCommand { get; }
    public RelayCommand ZoomInCommand { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand PasteCommand { get; }
    public RelayCommand<object>? UpdateShortcutCommand { get; set; }

    public ShortcutProfile CurrentShortcuts
    {
        get => _shortcutProfile;
        private set { _shortcutProfile = value; OnPropertyChanged(); }
    }

    public string GetShortcutLabel(InputAction action)
    {
        if (CurrentShortcuts.Bindings.TryGetValue(action, out var gesture))
        {
            return gesture.GetDisplayStringForCulture(CultureInfo.CurrentCulture);
        }
        return "None";
    }

    public void SaveShortcuts()
    {
        _shortcutService.Save(CurrentShortcuts);
    }
    
    private void PasteTranscript()
    {
        var text = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text)) return;

        if (Session.Segments.Count <= 1 && string.IsNullOrWhiteSpace(Session.Segments.FirstOrDefault()?.Text))
        {
            // If only one empty segment, just replace its text
            if (Session.Segments.Count == 0)
            {
                var segment = new Segment { StartSeconds = 0, EndSeconds = 0, Text = text };
                AttachSegment(segment);
                Session.Segments.Add(segment);
            }
            else
            {
                Session.Segments[0].Text = text;
            }
        }
        else
        {
            // Otherwise append or handle as multiple segments? 
            // For Notepad-like feel, lets just append to the last segment for now
            // Or better: ask the user. But I'll just append.
            var last = Session.Segments.LastOrDefault();
            if (last != null)
            {
                last.Text = (last.Text + "\n" + text).Trim();
            }
        }
        MarkDirty("Text pasted");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void LoadRecoveredSession(TranscriptionSession session, string sessionPath)
    {
        Session.Segments.Clear();
        foreach (var segment in session.Segments)
        {
            AttachSegment(segment);
            Session.Segments.Add(segment);
        }

        Session.AudioFile = session.AudioFile;
        AudioPath = session.AudioFile;
        SessionPath = sessionPath;
        if (!string.IsNullOrWhiteSpace(session.AudioFile) && File.Exists(session.AudioFile))
        {
            _audio.Load(session.AudioFile);
        }
        _isDirty = true;
        RaiseCommandStates();
    }

    private async void OpenAudio()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Audio Files|*.mp3;*.wav;*.m4a|All Files|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() == true)
        {
            var first = true;
            foreach (var file in dialog.FileNames)
            {
                if (!Playlist.Contains(file))
                {
                    Playlist.Add(file);
                }
                
                if (first && PlaylistIndex == -1)
                {
                    PlaylistIndex = Playlist.Count - 1;
                    first = false;
                }
            }
            
            MarkDirty("Audio added to playlist");
            RaiseCommandStates();
        }
    }

    public void ProcessDroppedFiles(string[] filePaths)
    {
        if (filePaths == null || filePaths.Length == 0) return;

        var supportedExtensions = new[] { ".mp3", ".wav", ".m4a", ".flac", ".ogg" };
        var audioFiles = filePaths
            .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        if (audioFiles.Count == 0) return;

        bool wasEmpty = Playlist.Count == 0;
        int firstNewIndex = Playlist.Count;

        foreach (var file in audioFiles)
        {
            if (!Playlist.Contains(file))
            {
                Playlist.Add(file);
            }
        }

        if (wasEmpty && Playlist.Count > 0)
        {
            PlaylistIndex = 0;
            // The PlaylistIndex setter calls LoadAudioInternal which sets up the engine.
            // We should auto-play if it's a fresh drag into an empty app.
            if (_audio.State != PlaybackState.Playing)
            {
                _audio.Play();
            }
        }

        MarkDirty($"{audioFiles.Count} file(s) added via drag-and-drop");
        RaiseCommandStates();
    }

    private async void LoadAudioInternal(string path)
    {
        _audio.Load(path);
        Session.AudioFile = path;
        AudioPath = path;
        WaveformData = await _waveformGenerator.GenerateAsync(path);
        _audio.Play();
        MarkDirty("Audio loaded");
        RaiseCommandStates();
    }

    private void PlayNextInPlaylist()
    {
        if (PlaylistIndex < Playlist.Count - 1)
        {
            PlaylistIndex++;
        }
    }

    private void OpenSession()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Vexa Session (*.json)|*.json|SubRip (*.srt)|*.srt|Text (*.txt)|*.txt|All Files|*.*"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var path = dialog.FileName;
        Session.Segments.Clear();
        SessionPath = path;

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".json")
        {
            var session = _jsonService.Load(path);
            LoadRecoveredSession(session, path);
        }
        else if (ext == ".srt")
        {
            foreach (var segment in _srtService.Load(path))
            {
                AttachSegment(segment);
                Session.Segments.Add(segment);
            }
        }
        else
        {
            var text = File.ReadAllText(path);
            var segment = new Segment
            {
                StartSeconds = 0,
                EndSeconds = 0,
                Text = text
            };
            AttachSegment(segment);
            Session.Segments.Add(segment);
        }

        MarkDirty("Session loaded");
        RaiseCommandStates();
    }

    private void SaveSession()
    {
        if (!string.IsNullOrWhiteSpace(SessionPath))
        {
            SaveToPath(SessionPath);
            return;
        }

        SaveSessionAs();
    }

    private void SaveSessionAs()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Vexa Session (*.json)|*.json",
            FileName = string.IsNullOrWhiteSpace(SessionPath) ? "session.json" : Path.GetFileName(SessionPath)
        };
        if (dialog.ShowDialog() == true)
        {
            SessionPath = dialog.FileName;
            SaveToPath(dialog.FileName);
        }
    }

    private void ExportSession()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text (*.txt)|*.txt|SubRip (*.srt)|*.srt|WebVTT (*.vtt)|*.vtt|Word Document (*.docx)|*.docx|Vexa Session (*.json)|*.json",
            FileName = "transcript"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var path = dialog.FileName;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        switch (ext)
        {
            case ".txt":
                _exportService.ExportTxt(path, Segments);
                break;
            case ".srt":
                _exportService.ExportSrt(path, Segments);
                break;
            case ".vtt":
                _exportService.ExportVtt(path, Segments);
                break;
            case ".docx":
                _exportService.ExportDocx(path, Segments);
                break;
            case ".json":
                _jsonService.Save(path, Session);
                break;
            default:
                _exportService.ExportTxt(path, Segments);
                break;
        }

        Status = $"Exported to {Path.GetFileName(path)}";
    }

    private void AddSegmentAtPlayhead()
    {
        var start = AudioPosition.TotalSeconds;
        var end = start + 2;
        var segment = new Segment
        {
            StartSeconds = start,
            EndSeconds = end,
            Speaker = "Speaker 1",
            Text = string.Empty
        };
        AttachSegment(segment);
        Session.Segments.Add(segment);
        SelectedSegment = segment;
        MarkDirty("Segment added");
    }

    private void RemoveSelectedSegment()
    {
        if (SelectedSegment == null)
        {
            return;
        }

        Session.Segments.Remove(SelectedSegment);
        SelectedSegment = null;
        MarkDirty("Segment removed");
    }

    private void SplitSelectedSegment()
    {
        if (SelectedSegment == null)
        {
            return;
        }

        var playhead = AudioPosition.TotalSeconds;
        if (playhead <= SelectedSegment.StartSeconds || playhead >= SelectedSegment.EndSeconds)
        {
            MessageBox.Show("Playhead must be inside the selected segment to split.", "Split Segment");
            return;
        }

        var original = SelectedSegment;
        var newSegment = new Segment
        {
            StartSeconds = playhead,
            EndSeconds = original.EndSeconds,
            Speaker = original.Speaker,
            Text = string.Empty
        };

        original.EndSeconds = playhead;
        var index = Session.Segments.IndexOf(original);
        Session.Segments.Insert(index + 1, newSegment);
        SelectedSegment = newSegment;
        MarkDirty("Segment split");
    }

    private void MergeWithNext()
    {
        if (SelectedSegment == null)
        {
            return;
        }

        var index = Session.Segments.IndexOf(SelectedSegment);
        if (index < 0 || index >= Session.Segments.Count - 1)
        {
            return;
        }

        var next = Session.Segments[index + 1];
        SelectedSegment.EndSeconds = next.EndSeconds;
        SelectedSegment.Text = string.Concat(SelectedSegment.Text, Environment.NewLine, next.Text).Trim();
        Session.Segments.Remove(next);
        MarkDirty("Segments merged");
    }

    private void TogglePlay()
    {
        if (_audio.State == PlaybackState.Playing)
        {
            _audio.Pause(true);
        }
        else
        {
            _audio.Play();
        }
    }

    private void LoopLastFiveSeconds()
    {
        var end = AudioPosition;
        var start = end - TimeSpan.FromSeconds(5);
        _audio.SetLoop(start, end);
        if (_audio.State != PlaybackState.Playing)
        {
            _audio.Play();
        }
        Status = "Looping last 5 seconds";
    }

    private void LoopSelection()
    {
        if (SelectedSegment == null) return;
        _audio.SetLoop(SelectedSegment.StartTime, SelectedSegment.EndTime);
        Status = "Looping selection";
    }

    private void GoToNextSegment()
    {
        var position = AudioPosition.TotalSeconds;
        var next = Session.Segments.FirstOrDefault(s => s.StartSeconds > position);
        if (next != null)
        {
            AudioPosition = TimeSpan.FromSeconds(next.StartSeconds);
            Status = "Skipped to next segment";
        }
        else
        {
            Status = "No next segment found";
        }
    }

    private void InsertTimestamp(RichTextBox? richTextBox)
    {
        if (richTextBox == null)
        {
            return;
        }

        var timestamp = $"[{FormatClock(AudioPosition)}]";
        RichTextBoxHelper.InsertAtCaret(richTextBox, timestamp);
    }

    private void AddMarker(string? marker)
    {
        if (SelectedSegment == null || string.IsNullOrWhiteSpace(marker))
        {
            return;
        }

        var text = SelectedSegment.Text ?? string.Empty;
        SelectedSegment.Text = string.Concat(text.TrimEnd(), text.EndsWith(' ') ? string.Empty : " ", marker).Trim();
        MarkDirty("Marker inserted");
    }

    private void UpdateActiveSegment()
    {
        var position = AudioPosition.TotalSeconds;
        var newActive = Session.Segments.FirstOrDefault(segment => position >= segment.StartSeconds && position <= segment.EndSeconds);
        ActiveSegment = newActive;
    }

    private void AttachSegment(Segment segment)
    {
        segment.PropertyChanged += OnSegmentChanged;
    }

    private void OnSegmentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Segment.IsActive))
        {
            return;
        }

        MarkDirty("Edits pending");
    }

    private void SaveToPath(string path)
    {
        _jsonService.Save(path, Session);
        _isDirty = false;
        Status = "Saved";
    }

    private void Autosave()
    {
        if (!_isDirty || Session.Segments.Count == 0)
        {
            return;
        }

        var autosavePath = GetAutosavePath();
        _jsonService.Save(autosavePath, Session);
        Application.Current.Dispatcher.Invoke(() => Status = "Autosaved");
    }

    private void MarkDirty(string reason)
    {
        _isDirty = true;
        Status = $"{reason} (unsaved)";
    }

    private void RaiseCommandStates()
    {
        SaveCommand.RaiseCanExecuteChanged();
        SaveAsCommand.RaiseCanExecuteChanged();
        ExportCommand.RaiseCanExecuteChanged();
        AddSegmentCommand.RaiseCanExecuteChanged();
        RemoveSegmentCommand.RaiseCanExecuteChanged();
        SplitSegmentCommand.RaiseCanExecuteChanged();
        MergeNextCommand.RaiseCanExecuteChanged();
        PlayPauseCommand.RaiseCanExecuteChanged();
        RewindCommand.RaiseCanExecuteChanged();
        SlowDownCommand.RaiseCanExecuteChanged();
        SpeedUpCommand.RaiseCanExecuteChanged();
        LoopLastFiveSecondsCommand.RaiseCanExecuteChanged();
        LoopSelectionCommand.RaiseCanExecuteChanged();
        ForwardCommand.RaiseCanExecuteChanged();
        NextSegmentCommand.RaiseCanExecuteChanged();
        ClearLoopCommand.RaiseCanExecuteChanged();
        AddMarkerCommand.RaiseCanExecuteChanged();
        RemoveFromPlaylistCommand.RaiseCanExecuteChanged();
        ClearPlaylistCommand.RaiseCanExecuteChanged();
        TogglePlaylistCommand.RaiseCanExecuteChanged();
    }

    private static string FormatClock(TimeSpan span)
    {
        if (span.TotalHours >= 1)
            return span.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        return span.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    private static string FormatTime(TimeSpan span)
    {
        return span.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
    }

    private static string GetAutosavePath()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vexa");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "autosave.json");
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

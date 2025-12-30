using System;
using System.Windows.Media;
using System.Windows.Threading;

namespace Vexa.Services;

public enum PlaybackState
{
    Stopped,
    Playing,
    Paused
}

public sealed class AudioEngine
{
    // MediaPlayer uses Windows Media Foundation on modern Windows builds.
    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer _timer;
    private TimeSpan _loopStart = TimeSpan.Zero;
    private TimeSpan _loopEnd = TimeSpan.Zero;
    private bool _loopEnabled;

    public AudioEngine()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _timer.Tick += (_, _) => OnTick();
        _player.MediaOpened += (_, _) =>
        {
            DurationChanged?.Invoke(this, EventArgs.Empty);
            _timer.Start();
        };
        _player.MediaEnded += (_, _) => 
        {
            Stop();
            PlaybackEnded?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? PositionChanged;
    public event EventHandler? DurationChanged;
    public event EventHandler? PlaybackStateChanged;
    public event EventHandler? PlaybackEnded;

    public PlaybackState State { get; private set; } = PlaybackState.Stopped;

    public bool HasAudio => _player.Source != null;

    public TimeSpan Position
    {
        get => _player.Position;
        set => _player.Position = value;
    }

    public TimeSpan Duration => _player.NaturalDuration.HasTimeSpan ? _player.NaturalDuration.TimeSpan : TimeSpan.Zero;

    public double Speed
    {
        get => _player.SpeedRatio;
        set => _player.SpeedRatio = Math.Clamp(value, 0.5, 1.5);
    }

    public double Volume
    {
        get => _player.Volume;
        set => _player.Volume = Math.Clamp(value, 0.0, 1.0);
    }

    public TimeSpan RewindAmount { get; set; } = TimeSpan.FromSeconds(2);

    public void Load(string path)
    {
        _player.Open(new Uri(path));
        _player.Position = TimeSpan.Zero;
        SetState(PlaybackState.Stopped);
    }

    public void Play()
    {
        _player.Play();
        SetState(PlaybackState.Playing);
    }

    public void Pause(bool autoRewind)
    {
        _player.Pause();
        if (autoRewind)
        {
            var rewound = _player.Position - RewindAmount;
            _player.Position = rewound < TimeSpan.Zero ? TimeSpan.Zero : rewound;
        }
        SetState(PlaybackState.Paused);
    }

    public void Stop()
    {
        _player.Stop();
        SetState(PlaybackState.Stopped);
    }

    public void Rewind()
    {
        var position = _player.Position - RewindAmount;
        _player.Position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Forward()
    {
        var position = _player.Position + RewindAmount;
        _player.Position = position > Duration ? Duration : position;
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetLoop(TimeSpan start, TimeSpan end)
    {
        _loopStart = start < TimeSpan.Zero ? TimeSpan.Zero : start;
        _loopEnd = end > Duration ? Duration : end;
        _loopEnabled = _loopEnd > _loopStart;
    }

    public void ClearLoop() => _loopEnabled = false;

    private void OnTick()
    {
        if (_loopEnabled && State == PlaybackState.Playing && _player.Position >= _loopEnd)
        {
            _player.Position = _loopStart;
        }
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetState(PlaybackState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }
}

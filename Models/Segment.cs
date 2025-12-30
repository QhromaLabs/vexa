using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Vexa.Models;

public sealed class Segment : INotifyPropertyChanged
{
    private double _startSeconds;
    private double _endSeconds;
    private string _speaker = string.Empty;
    private string _text = string.Empty;
    private bool _isActive;

    [JsonPropertyName("start")]
    public double StartSeconds
    {
        get => _startSeconds;
        set
        {
            _startSeconds = Math.Max(0, value);
            if (_endSeconds < _startSeconds)
            {
                _endSeconds = _startSeconds;
                OnPropertyChanged(nameof(EndSeconds));
                OnPropertyChanged(nameof(EndTime));
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(StartTime));
        }
    }

    [JsonPropertyName("end")]
    public double EndSeconds
    {
        get => _endSeconds;
        set
        {
            var normalized = Math.Max(0, value);
            _endSeconds = normalized < _startSeconds ? _startSeconds : normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EndTime));
        }
    }

    public TimeSpan StartTime => TimeSpan.FromSeconds(StartSeconds);

    public TimeSpan EndTime => TimeSpan.FromSeconds(EndSeconds);

    [JsonPropertyName("speaker")]
    public string Speaker
    {
        get => _speaker;
        set { _speaker = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("text")]
    public string Text
    {
        get => _text;
        set { _text = value; OnPropertyChanged(); }
    }

    [JsonPropertyName("flags")]
    public ObservableCollection<string> Flags { get; } = new();

    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

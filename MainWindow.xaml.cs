using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Vexa.Models;
using Vexa.Services;
using Vexa.ViewModels;

namespace Vexa;

public partial class MainWindow : Window
{
    private readonly FootPedalService _footPedalService = new();

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new TranscriptionViewModel(new AudioEngine(), new SrtService(), new ExportService(), new JsonSessionService());
        DataContext = ViewModel;

        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
        RegisterShortcuts(ShortcutProfile.CreateDefaults());
    }

    public TranscriptionViewModel ViewModel { get; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _footPedalService.Initialize(new WindowInteropHelper(this));
        _footPedalService.ActionTriggered += (_, action) =>
        {
            switch (action)
            {
                case InputAction.PlayPause:
                    ViewModel.PlayPauseCommand.Execute(null);
                    break;
                case InputAction.Rewind:
                    ViewModel.RewindCommand.Execute(null);
                    break;
                case InputAction.SlowDown:
                    ViewModel.SlowDownCommand.Execute(null);
                    break;
                case InputAction.SpeedUp:
                    ViewModel.SpeedUpCommand.Execute(null);
                    break;
                case InputAction.LoopLastFiveSeconds:
                    ViewModel.LoopLastFiveSecondsCommand.Execute(null);
                    break;
            }
        };
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Vexa is a professional transcription editor for refining existing transcripts alongside audio.\n\nBuilt with .NET 8 and WPF.",
            "About Vexa", MessageBoxButton.OK, MessageBoxImage.Information);
    }


    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
        {
            var focused = Keyboard.FocusedElement as RichTextBox;
            ViewModel.InsertTimestampCommand.Execute(focused);
            e.Handled = true;
        }
    }

    private void RegisterShortcuts(ShortcutProfile profile)
    {
        InputBindings.Clear();

        foreach (var binding in profile.Bindings)
        {
            switch (binding.Key)
            {
                case InputAction.PlayPause:
                    InputBindings.Add(new KeyBinding(ViewModel.PlayPauseCommand, binding.Value));
                    break;
                case InputAction.Rewind:
                    InputBindings.Add(new KeyBinding(ViewModel.RewindCommand, binding.Value));
                    break;
                case InputAction.SlowDown:
                    InputBindings.Add(new KeyBinding(ViewModel.SlowDownCommand, binding.Value));
                    break;
                case InputAction.SpeedUp:
                    InputBindings.Add(new KeyBinding(ViewModel.SpeedUpCommand, binding.Value));
                    break;
                case InputAction.LoopLastFiveSeconds:
                    InputBindings.Add(new KeyBinding(ViewModel.LoopLastFiveSecondsCommand, binding.Value));
                    break;
            }
        }

        InputBindings.Add(new KeyBinding(ViewModel.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(ViewModel.ExportCommand, new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift)));
    }
}

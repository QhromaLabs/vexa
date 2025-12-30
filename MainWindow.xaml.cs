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
        RegisterShortcuts(ViewModel.CurrentShortcuts);
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

    private InputAction? _recordingAction;

    private void OnButtonRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button btn && btn.Tag is InputAction action)
        {
            _recordingAction = action;
            ShortcutActionText.Text = action.ToString();
            NewShortcutPreview.Text = ViewModel.GetShortcutLabel(action);
            ShortcutPopup.IsOpen = true;
            e.Handled = true;
        }
    }

    private void CloseShortcutPopup(object sender, RoutedEventArgs e)
    {
        ShortcutPopup.IsOpen = false;
        _recordingAction = null;
    }


    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_recordingAction != null)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin)
            {
                var currentModifiers = Keyboard.Modifiers;
                if (currentModifiers != ModifierKeys.None)
                {
                    NewShortcutPreview.Text = currentModifiers.ToString() + " + ...";
                }
                return;
            }

            var modifiers = Keyboard.Modifiers;
            var gesture = new KeyGesture(key, modifiers);
            
            ViewModel.CurrentShortcuts.Bindings[_recordingAction.Value] = gesture;
            NewShortcutPreview.Text = ViewModel.GetShortcutLabel(_recordingAction.Value);
            
            ViewModel.SaveShortcuts();
            RegisterShortcuts(ViewModel.CurrentShortcuts);
            UpdateToolTips();
            
            ShortcutPopup.IsOpen = false;
            _recordingAction = null;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
        {
            var focused = Keyboard.FocusedElement as RichTextBox;
            ViewModel.InsertTimestampCommand.Execute(focused);
            e.Handled = true;
        }
    }

    private void UpdateToolTips()
    {
        // Simple update: Refresh the UI properties that ToolTips might bind to, 
        // but here we aren't using bindings for ToolTips yet. 
        // Let's iterate and update them manually for now if needed, 
        // or just rely on the next time the app opens/refreshes if we had used bindings.
        // Actually, let's fix the XAML to use a better tooltip approach later if needed.
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
                case InputAction.OpenAudio:
                    InputBindings.Add(new KeyBinding(ViewModel.OpenAudioCommand, binding.Value));
                    break;
                case InputAction.Save:
                    InputBindings.Add(new KeyBinding(ViewModel.SaveCommand, binding.Value));
                    break;
                case InputAction.Export:
                    InputBindings.Add(new KeyBinding(ViewModel.ExportCommand, binding.Value));
                    break;
                case InputAction.ZoomIn:
                    InputBindings.Add(new KeyBinding(ViewModel.ZoomInCommand, binding.Value));
                    break;
                case InputAction.ZoomOut:
                    InputBindings.Add(new KeyBinding(ViewModel.ZoomOutCommand, binding.Value));
                    break;
                case InputAction.LoopSelection:
                    InputBindings.Add(new KeyBinding(ViewModel.LoopSelectionCommand, binding.Value));
                    break;
                case InputAction.Forward:
                    InputBindings.Add(new KeyBinding(ViewModel.ForwardCommand, binding.Value));
                    break;
                case InputAction.NextSegment:
                    InputBindings.Add(new KeyBinding(ViewModel.NextSegmentCommand, binding.Value));
                    break;
            }
        }

        // Standard hardcoded ones can be moved to the profile too, but keeping consistency for now.
    }
}

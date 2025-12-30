using System.Collections.Generic;
using System.Windows.Input;

namespace Vexa.Models;

public sealed class ShortcutProfile
{
    public Dictionary<InputAction, KeyGesture> Bindings { get; } = new();

    public static ShortcutProfile CreateDefaults()
    {
        var profile = new ShortcutProfile();
        profile.Bindings[InputAction.PlayPause] = new KeyGesture(Key.F1);
        profile.Bindings[InputAction.Rewind] = new KeyGesture(Key.F2);
        profile.Bindings[InputAction.SlowDown] = new KeyGesture(Key.F3);
        profile.Bindings[InputAction.SpeedUp] = new KeyGesture(Key.F4);
        profile.Bindings[InputAction.LoopLastFiveSeconds] = new KeyGesture(Key.Space, ModifierKeys.Control);
        
        profile.Bindings[InputAction.OpenAudio] = new KeyGesture(Key.O, ModifierKeys.Control);
        profile.Bindings[InputAction.Save] = new KeyGesture(Key.S, ModifierKeys.Control);
        profile.Bindings[InputAction.Export] = new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift);
        profile.Bindings[InputAction.ZoomIn] = new KeyGesture(Key.OemPlus, ModifierKeys.Control);
        profile.Bindings[InputAction.ZoomOut] = new KeyGesture(Key.OemMinus, ModifierKeys.Control);
        profile.Bindings[InputAction.LoopSelection] = new KeyGesture(Key.L, ModifierKeys.Control);
        
        return profile;
    }
}

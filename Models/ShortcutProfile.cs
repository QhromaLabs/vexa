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
        return profile;
    }
}

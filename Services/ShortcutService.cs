using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using Vexa.Models;

namespace Vexa.Services;

public sealed class ShortcutService
{
    private readonly string _filePath;

    public ShortcutService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vexa");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "shortcuts.json");
    }

    public void Save(ShortcutProfile profile)
    {
        var dto = new Dictionary<InputAction, string>();
        foreach (var binding in profile.Bindings)
        {
            var converter = new KeyGestureConverter();
            dto[binding.Key] = converter.ConvertToString(binding.Value) ?? string.Empty;
        }

        var json = JsonSerializer.Serialize(dto);
        File.WriteAllText(_filePath, json);
    }

    public ShortcutProfile Load()
    {
        if (!File.Exists(_filePath))
        {
            return ShortcutProfile.CreateDefaults();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var dto = JsonSerializer.Deserialize<Dictionary<InputAction, string>>(json);
            
            var profile = new ShortcutProfile();
            var converter = new KeyGestureConverter();

            if (dto != null)
            {
                foreach (var item in dto)
                {
                    try
                    {
                        var gesture = converter.ConvertFromInvariantString(item.Value) as KeyGesture;
                        if (gesture != null)
                        {
                            profile.Bindings[item.Key] = gesture;
                        }
                    }
                    catch
                    {
                        // Fallback to default if one fails
                        var defaults = ShortcutProfile.CreateDefaults();
                        if (defaults.Bindings.TryGetValue(item.Key, out var defGesture))
                        {
                            profile.Bindings[item.Key] = defGesture;
                        }
                    }
                }
            }

            // Fill in any missing ones from defaults
            var finalDefaults = ShortcutProfile.CreateDefaults();
            foreach (var def in finalDefaults.Bindings)
            {
                if (!profile.Bindings.ContainsKey(def.Key))
                {
                    profile.Bindings[def.Key] = def.Value;
                }
            }

            return profile;
        }
        catch
        {
            return ShortcutProfile.CreateDefaults();
        }
    }
}

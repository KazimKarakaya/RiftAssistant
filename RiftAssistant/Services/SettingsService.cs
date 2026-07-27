using System;
using System.IO;
using System.Text.Json;
using RiftAssistant.Models;

namespace RiftAssistant.Services;

public class SettingsService
{
    private readonly string _settingsPath;

    public SettingsService()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "RiftAssistant"
        );

        Directory.CreateDirectory(folder);

        _settingsPath = Path.Combine(
            folder,
            "settings.json"
        );
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new AppSettings();

            string json =
                File.ReadAllText(_settingsPath);

            return JsonSerializer.Deserialize<AppSettings>(json)
                   ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        string json =
            JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }
            );

        File.WriteAllText(
            _settingsPath,
            json
        );
    }
}

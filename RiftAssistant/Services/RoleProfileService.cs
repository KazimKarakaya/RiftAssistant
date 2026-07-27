using System;
using System.IO;
using System.Text.Json;
using RiftAssistant.Models;

namespace RiftAssistant.Services
{
    public sealed class RoleProfileService
    {
        private readonly string _filePath;

        public RoleProfileService()
        {
            string appDataPath =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData
                    ),
                    "RiftAssistant"
                );

            Directory.CreateDirectory(appDataPath);

            _filePath =
                Path.Combine(
                    appDataPath,
                    "role-profiles.json"
                );
        }

        public bool Exists()
        {
            return File.Exists(_filePath);
        }

        public RoleProfileSettings Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new RoleProfileSettings();

                string json =
                    File.ReadAllText(_filePath);

                return JsonSerializer.Deserialize<RoleProfileSettings>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }
                ) ?? new RoleProfileSettings();
            }
            catch
            {
                return new RoleProfileSettings();
            }
        }

        public void Save(
            RoleProfileSettings settings)
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
                _filePath,
                json
            );
        }
    }
}

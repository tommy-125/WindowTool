using System.Diagnostics;
using System.IO;
using System.Text.Json;
using WindowTool.Model;

namespace WindowTool.Service {
    internal sealed class ProcessSettingsStore {
        private readonly string _settingsPath;
        private readonly Dictionary<string, ProcessSettings> _settingsByProcessName;
        private readonly object _sync = new();

        public ProcessSettingsStore() {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string settingsDirectory = Path.Combine(appDataPath, "WindowTool");
            _settingsPath = Path.Combine(settingsDirectory, "settings.json");
            _settingsByProcessName = LoadSettings();
        }

        public void Apply(ProcessInfo process) {
            ProcessSettings? settings;
            lock (_sync) {
                _settingsByProcessName.TryGetValue(process.Name, out settings);
            }

            if (settings == null) return;

            process.EnableUnfocusMute = settings.EnableUnfocusMute;
            process.UnfocusMuteDurationSec = settings.UnfocusMuteDurationSec;
            process.FocusUnmuteDurationSec = settings.FocusUnmuteDurationSec;
            process.FadeMuteDurationSec = settings.FadeMuteDurationSec;
            process.FadeUnmuteDurationSec = settings.FadeUnmuteDurationSec;
            process.ShouldBeTopMost = settings.ShouldBeTopMost;
            bool hasTargetVolume = settings.HasTargetVolume || settings.HasOriginalVolume == true;
            process.HasTargetVolume = hasTargetVolume;
            if (hasTargetVolume) {
                float storedVolume = settings.HasTargetVolume
                    ? settings.TargetVolume
                    : settings.OriginalVolume ?? 1.0f;
                process.TargetVolume = Math.Clamp(storedVolume, 0.0f, 1.0f);
            }
        }

        public void Save(ProcessInfo process) {
            lock (_sync) {
                _settingsByProcessName[process.Name] = new ProcessSettings {
                    EnableUnfocusMute = process.EnableUnfocusMute,
                    UnfocusMuteDurationSec = process.UnfocusMuteDurationSec,
                    FocusUnmuteDurationSec = process.FocusUnmuteDurationSec,
                    FadeMuteDurationSec = process.FadeMuteDurationSec,
                    FadeUnmuteDurationSec = process.FadeUnmuteDurationSec,
                    ShouldBeTopMost = process.ShouldBeTopMost,
                    HasTargetVolume = process.HasTargetVolume,
                    TargetVolume = process.TargetVolume,
                };
            }

            PersistSettings();
        }

        private Dictionary<string, ProcessSettings> LoadSettings() {
            try {
                if (!File.Exists(_settingsPath)) return new Dictionary<string, ProcessSettings>();

                string json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<Dictionary<string, ProcessSettings>>(json)
                    ?? new Dictionary<string, ProcessSettings>();
            }
            catch (Exception ex) {
                Debug.WriteLine($"[ProcessSettingsStore] Failed to load settings: {ex.Message}");
                return new Dictionary<string, ProcessSettings>();
            }
        }

        private void PersistSettings() {
            try {
                string? directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(directory)) {
                    Directory.CreateDirectory(directory);
                }

                string json;
                lock (_sync) {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    json = JsonSerializer.Serialize(_settingsByProcessName, options);
                }

                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex) {
                Debug.WriteLine($"[ProcessSettingsStore] Failed to save settings: {ex.Message}");
            }
        }
    }
}

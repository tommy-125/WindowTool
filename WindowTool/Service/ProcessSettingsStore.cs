using System.Diagnostics;
using System.Text.Json;
using WindowTool.Model;

namespace WindowTool.Service {
    internal sealed class ProcessSettingsStore {
        private readonly string _settingsPath;
        private readonly Dictionary<string, ProcessSettings> _settingsByProcessName;

        public ProcessSettingsStore() {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string settingsDirectory = Path.Combine(appDataPath, "WindowTool");
            _settingsPath = Path.Combine(settingsDirectory, "settings.json");
            _settingsByProcessName = LoadSettings();
        }

        public void Apply(ProcessInfo process) {
            if (!_settingsByProcessName.TryGetValue(process.Name, out var settings)) return;

            process.EnableUnfocusMute = settings.EnableUnfocusMute;
            process.UnfocusMuteDurationSec = settings.UnfocusMuteDurationSec;
            process.FocusUnmuteDurationSec = settings.FocusUnmuteDurationSec;
            process.FadeMuteDurationSec = settings.FadeMuteDurationSec;
            process.FadeUnmuteDurationSec = settings.FadeUnmuteDurationSec;
            process.ShouldBeTopMost = settings.ShouldBeTopMost;
        }

        public void Save(ProcessInfo process) {
            _settingsByProcessName[process.Name] = new ProcessSettings {
                EnableUnfocusMute = process.EnableUnfocusMute,
                UnfocusMuteDurationSec = process.UnfocusMuteDurationSec,
                FocusUnmuteDurationSec = process.FocusUnmuteDurationSec,
                FadeMuteDurationSec = process.FadeMuteDurationSec,
                FadeUnmuteDurationSec = process.FadeUnmuteDurationSec,
                ShouldBeTopMost = process.ShouldBeTopMost,
            };

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

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_settingsByProcessName, options));
            }
            catch (Exception ex) {
                Debug.WriteLine($"[ProcessSettingsStore] Failed to save settings: {ex.Message}");
            }
        }
    }
}

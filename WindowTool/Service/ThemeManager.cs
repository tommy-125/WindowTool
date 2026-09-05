using System.IO;
using System.Windows;
using System.Windows.Media;

namespace WindowTool.Service;

internal static class ThemeManager {
    private const string DefaultTheme = "Midnight";
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WindowTool",
        "theme.txt");

    private sealed record Palette(
        string WindowBackground,
        string Panel,
        string PanelHover,
        string Border,
        string Primary,
        string PrimaryHover,
        string Text,
        string SecondaryText,
        string Success,
        string ScrollBar,
        string ScrollBarHover,
        string ScrollBarPressed,
        string Input,
        string Selection,
        string RowAlternate,
        string IconFallback);

    private static readonly IReadOnlyDictionary<string, Palette> Palettes =
        new Dictionary<string, Palette>(StringComparer.OrdinalIgnoreCase) {
            ["Midnight"] = new(
                "#0B1020", "#121A2B", "#172238", "#26334A", "#7C8CFF", "#94A0FF",
                "#F4F7FF", "#9AA8C1", "#4FD1A5", "#5364C7", "#7C8CFF", "#94A0FF",
                "#0E1525", "#273455", "#0D1424", "#273455"),
            ["Light"] = new(
                "#F2F5FA", "#FFFFFF", "#E8EDF5", "#CBD5E1", "#5267D8", "#4054C2",
                "#172033", "#5B677A", "#16856A", "#6276D8", "#5267D8", "#4054C2",
                "#F8FAFD", "#D9E1FF", "#F1F4F9", "#D9E1FF"),
            ["Purple"] = new(
                "#160F24", "#211735", "#30214A", "#49356A", "#B58CFF", "#C9ABFF",
                "#FBF7FF", "#BBAACF", "#65D6B2", "#8B63D6", "#B58CFF", "#C9ABFF",
                "#1B132C", "#49356A", "#1C142D", "#49356A")
        };

    public static string LoadTheme() {
        try {
            if (File.Exists(SettingsPath)) {
                string value = File.ReadAllText(SettingsPath).Trim();
                if (Palettes.ContainsKey(value)) return value;
            }
        }
        catch {
            // A missing or unreadable preference should not prevent startup.
        }

        return DefaultTheme;
    }

    public static void SaveTheme(string theme) {
        if (!Palettes.ContainsKey(theme)) theme = DefaultTheme;

        try {
            string? directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(SettingsPath, theme);
        }
        catch {
            // Theme changes still apply for the current session if persistence fails.
        }
    }

    public static void Apply(Application application, string theme) {
        if (!Palettes.TryGetValue(theme, out Palette? palette)) {
            theme = DefaultTheme;
            palette = Palettes[theme];
        }

        SetColor(application, "WindowBackgroundBrush", palette.WindowBackground);
        SetColor(application, "PanelBrush", palette.Panel);
        SetColor(application, "PanelHoverBrush", palette.PanelHover);
        SetColor(application, "BorderBrush", palette.Border);
        SetColor(application, "PrimaryBrush", palette.Primary);
        SetColor(application, "PrimaryHoverBrush", palette.PrimaryHover);
        SetColor(application, "TextBrush", palette.Text);
        SetColor(application, "SecondaryTextBrush", palette.SecondaryText);
        SetColor(application, "SuccessBrush", palette.Success);
        SetColor(application, "ScrollBarThumbBrush", palette.ScrollBar);
        SetColor(application, "ScrollBarThumbHoverBrush", palette.ScrollBarHover);
        SetColor(application, "ScrollBarThumbPressedBrush", palette.ScrollBarPressed);
        SetColor(application, "InputBackgroundBrush", palette.Input);
        SetColor(application, "SelectionBrush", palette.Selection);
        SetColor(application, "RowAlternateBrush", palette.RowAlternate);
        SetColor(application, "IconFallbackBrush", palette.IconFallback);
    }

    private static void SetColor(Application application, string key, string value) {
        object? converted = ColorConverter.ConvertFromString(value);
        if (converted is not Color color) return;
        ResourceDictionary resources = application.Resources;
        if (resources.Contains(key)) {
            resources.Remove(key);
        }
        resources.Add(key, new SolidColorBrush(color));
    }
}

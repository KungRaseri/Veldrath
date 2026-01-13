namespace RealmForge.Models;

/// <summary>
/// User preferences and settings for the RealmForge editor
/// </summary>
public class EditorSettings
{
    /// <summary>
    /// Theme preference (Light or Dark)
    /// </summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>
    /// Path to the game data folder
    /// </summary>
    public string DataFolderPath { get; set; } = @"c:\code\console-game\RealmEngine.Data\Data\Json";

    /// <summary>
    /// Auto-save interval in seconds (0 = disabled)
    /// </summary>
    public int AutoSaveIntervalSeconds { get; set; } = 120; // 2 minutes

    /// <summary>
    /// Maximum number of recent files to track
    /// </summary>
    public int MaxRecentFiles { get; set; } = 10;

    /// <summary>
    /// Recently opened files (full paths)
    /// </summary>
    public List<string> RecentFiles { get; set; } = new();

    /// <summary>
    /// Default editor mode (Form or JSON)
    /// </summary>
    public string DefaultEditorMode { get; set; } = "Form";

    /// <summary>
    /// Enable Monaco Editor features (syntax highlighting, autocomplete)
    /// </summary>
    public bool EnableMonacoFeatures { get; set; } = true;

    /// <summary>
    /// Show validation errors inline
    /// </summary>
    public bool ShowInlineValidation { get; set; } = true;

    /// <summary>
    /// Confirm before closing unsaved files
    /// </summary>
    public bool ConfirmUnsavedChanges { get; set; } = true;
}

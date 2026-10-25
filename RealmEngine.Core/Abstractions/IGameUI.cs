namespace RealmEngine.Core.Abstractions;

/// <summary>
/// Abstraction for game UI operations. Implementations can be console, web, mobile, etc.
/// This allows RealmEngine.Core to remain UI-agnostic.
/// </summary>
public interface IGameUI
{
    /// <summary>
    /// Displays a standard message to the user.
    /// </summary>
    void ShowMessage(string message);

    /// <summary>
    /// Writes plain text output to the UI.
    /// </summary>
    void WriteText(string text);

    /// <summary>
    /// Displays a success message (typically in green).
    /// </summary>
    void ShowSuccess(string message);

    /// <summary>
    /// Displays an error message (typically in red).
    /// </summary>
    void ShowError(string message);

    /// <summary>
    /// Displays a warning message (typically in yellow).
    /// </summary>
    void ShowWarning(string message);

    /// <summary>
    /// Displays an informational message (typically in blue).
    /// </summary>
    void ShowInfo(string message);

    /// <summary>
    /// Displays a titled banner with optional subtitle.
    /// </summary>
    void ShowBanner(string title, string subtitle = "");

    /// <summary>
    /// Shows a progress indicator while executing work.
    /// </summary>
    void ShowProgress(string title, Action<object> work);

    /// <summary>
    /// Prompts the user for text input and returns the response.
    /// </summary>
    string AskForInput(string prompt);

    /// <summary>
    /// Prompts the user for numeric input within a specified range.
    /// </summary>
    int AskForNumber(string prompt, int min, int max);

    /// <summary>
    /// Asks the user a yes/no question and returns their answer.
    /// </summary>
    bool Confirm(string question);

    /// <summary>
    /// Displays a menu of options and returns the user's selection.
    /// </summary>
    string ShowMenu(string title, params string[] options);

    /// <summary>
    /// Displays a menu allowing multiple selections and returns the chosen options.
    /// </summary>
    List<string> ShowMultiSelectMenu(string title, params string[] options);

    /// <summary>
    /// Displays data in a formatted table.
    /// </summary>
    void ShowTable(string title, string[] headers, List<string[]> rows);

    /// <summary>
    /// Displays content within a bordered panel.
    /// </summary>
    void ShowPanel(string title, string content, string color = "white");

    /// <summary>
    /// Clears the UI display.
    /// </summary>
    void Clear();

    /// <summary>
    /// Pauses execution until the user presses a key.
    /// </summary>
    void PressAnyKey(string message = "Press any key to continue...");

    /// <summary>
    /// Utility method to strip markup from text for length calculations.
    /// </summary>
    string StripMarkup(string text);
}

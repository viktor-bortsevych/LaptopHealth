namespace LaptopHealth.Services.Interfaces
{
    /// <summary>
    /// Interface for pages that handle keyboard shortcuts
    /// Allows pages to intercept and handle keyboard events globally
    /// </summary>
    public interface IKeyboardShortcutHandler
    {
        /// <summary>
        /// Handles a keyboard event for this page
        /// </summary>
        /// <param name="key">The key that was pressed</param>
        /// <returns>True if the key was handled, false otherwise</returns>
        bool HandleKeyDown(System.Windows.Input.Key key);
    }
}

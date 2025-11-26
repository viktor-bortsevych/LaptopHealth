using LaptopHealth.ViewModels.Infrastructure;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace LaptopHealth.ViewModels
{
    /// <summary>
    /// ViewModel for KeyboardTestPage - handles keyboard key press visualization
    /// </summary>
    public class KeyboardTestPageViewModel : ViewModelBase
    {
        private readonly HashSet<Key> _pressedKeys = new();
        private readonly Dictionary<Key, string> _keyDisplayNames = new();
        private const int MaxHistoryCount = 20;

        #region Properties

        private string _lastKeyPressed = "N/A";
        public string LastKeyPressed
        {
            get => _lastKeyPressed;
            set => SetProperty(ref _lastKeyPressed, value);
        }

        private ObservableCollection<string> _keyPressHistory = new();
        public ObservableCollection<string> KeyPressHistory
        {
            get => _keyPressHistory;
            set => SetProperty(ref _keyPressHistory, value);
        }

        #endregion

        #region Constructor

        public KeyboardTestPageViewModel()
        {
            InitializeKeyDisplayNames();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Handles key down events
        /// </summary>
        public void HandleKeyDown(Key key, Key systemKey)
        {
            // Handle system keys (like Alt, F10)
            var actualKey = key == Key.System ? systemKey : key;

            if (actualKey == Key.None)
                return;

            // Track pressed keys
            if (!_pressedKeys.Contains(actualKey))
            {
                _pressedKeys.Add(actualKey);
                UpdateLastKeyPressed(actualKey);
            }
        }

        /// <summary>
        /// Handles key up events
        /// </summary>
        public void HandleKeyUp(Key key, Key systemKey)
        {
            // Handle system keys (like Alt, F10)
            var actualKey = key == Key.System ? systemKey : key;

            if (actualKey == Key.None)
                return;

            // Remove from pressed keys
            _pressedKeys.Remove(actualKey);
        }

        /// <summary>
        /// Checks if a key is currently pressed
        /// </summary>
        public bool IsKeyPressed(Key key)
        {
            return _pressedKeys.Contains(key);
        }

        /// <summary>
        /// Cleanup method called before page disposal
        /// </summary>
        public void Cleanup()
        {
            _pressedKeys.Clear();
            KeyPressHistory.Clear();
        }

        #endregion

        #region Private Methods

        private void UpdateLastKeyPressed(Key key)
        {
            string displayName;
            if (_keyDisplayNames.TryGetValue(key, out var name))
            {
                displayName = name;
            }
            else
            {
                displayName = key.ToString();
            }

            LastKeyPressed = displayName;

            // Add to history
            KeyPressHistory.Insert(0, displayName);

            // Limit history size
            while (KeyPressHistory.Count > MaxHistoryCount)
            {
                KeyPressHistory.RemoveAt(KeyPressHistory.Count - 1);
            }
        }

        private void InitializeKeyDisplayNames()
        {
            // Function keys
            _keyDisplayNames[Key.Escape] = "Esc";
            _keyDisplayNames[Key.F1] = "F1";
            _keyDisplayNames[Key.F2] = "F2";
            _keyDisplayNames[Key.F3] = "F3";
            _keyDisplayNames[Key.F4] = "F4";
            _keyDisplayNames[Key.F5] = "F5";
            _keyDisplayNames[Key.F6] = "F6";
            _keyDisplayNames[Key.F7] = "F7";
            _keyDisplayNames[Key.F8] = "F8";
            _keyDisplayNames[Key.F9] = "F9";
            _keyDisplayNames[Key.F10] = "F10";
            _keyDisplayNames[Key.F11] = "F11";
            _keyDisplayNames[Key.F12] = "F12";

            // Number row
            _keyDisplayNames[Key.OemTilde] = "` (Backtick)";
            _keyDisplayNames[Key.D1] = "1";
            _keyDisplayNames[Key.D2] = "2";
            _keyDisplayNames[Key.D3] = "3";
            _keyDisplayNames[Key.D4] = "4";
            _keyDisplayNames[Key.D5] = "5";
            _keyDisplayNames[Key.D6] = "6";
            _keyDisplayNames[Key.D7] = "7";
            _keyDisplayNames[Key.D8] = "8";
            _keyDisplayNames[Key.D9] = "9";
            _keyDisplayNames[Key.D0] = "0";
            _keyDisplayNames[Key.OemMinus] = "- (Minus)";
            _keyDisplayNames[Key.OemPlus] = "= (Equals)";
            _keyDisplayNames[Key.Back] = "Backspace";

            // Letters
            _keyDisplayNames[Key.Q] = "Q";
            _keyDisplayNames[Key.W] = "W";
            _keyDisplayNames[Key.E] = "E";
            _keyDisplayNames[Key.R] = "R";
            _keyDisplayNames[Key.T] = "T";
            _keyDisplayNames[Key.Y] = "Y";
            _keyDisplayNames[Key.U] = "U";
            _keyDisplayNames[Key.I] = "I";
            _keyDisplayNames[Key.O] = "O";
            _keyDisplayNames[Key.P] = "P";
            _keyDisplayNames[Key.A] = "A";
            _keyDisplayNames[Key.S] = "S";
            _keyDisplayNames[Key.D] = "D";
            _keyDisplayNames[Key.F] = "F";
            _keyDisplayNames[Key.G] = "G";
            _keyDisplayNames[Key.H] = "H";
            _keyDisplayNames[Key.J] = "J";
            _keyDisplayNames[Key.K] = "K";
            _keyDisplayNames[Key.L] = "L";
            _keyDisplayNames[Key.Z] = "Z";
            _keyDisplayNames[Key.X] = "X";
            _keyDisplayNames[Key.C] = "C";
            _keyDisplayNames[Key.V] = "V";
            _keyDisplayNames[Key.B] = "B";
            _keyDisplayNames[Key.N] = "N";
            _keyDisplayNames[Key.M] = "M";

            // Special characters
            _keyDisplayNames[Key.OemOpenBrackets] = "[ (Left Bracket)";
            _keyDisplayNames[Key.OemCloseBrackets] = "] (Right Bracket)";
            _keyDisplayNames[Key.OemPipe] = "\\ (Backslash)";
            _keyDisplayNames[Key.OemSemicolon] = "; (Semicolon)";
            _keyDisplayNames[Key.OemQuotes] = "' (Quote)";
            _keyDisplayNames[Key.OemComma] = ", (Comma)";
            _keyDisplayNames[Key.OemPeriod] = ". (Period)";
            _keyDisplayNames[Key.OemQuestion] = "/ (Slash)";

            // Special keys
            _keyDisplayNames[Key.Tab] = "Tab";
            _keyDisplayNames[Key.CapsLock] = "Caps Lock";
            _keyDisplayNames[Key.LeftShift] = "Left Shift";
            _keyDisplayNames[Key.RightShift] = "Right Shift";
            _keyDisplayNames[Key.LeftCtrl] = "Left Ctrl";
            _keyDisplayNames[Key.RightCtrl] = "Right Ctrl";
            _keyDisplayNames[Key.LWin] = "Windows";
            _keyDisplayNames[Key.LeftAlt] = "Left Alt";
            _keyDisplayNames[Key.RightAlt] = "Right Alt";
            _keyDisplayNames[Key.Space] = "Space";
            _keyDisplayNames[Key.Apps] = "Menu";
            _keyDisplayNames[Key.Return] = "Enter";

            // Navigation keys
            _keyDisplayNames[Key.Insert] = "Insert";
            _keyDisplayNames[Key.Delete] = "Delete";
            _keyDisplayNames[Key.Home] = "Home";
            _keyDisplayNames[Key.End] = "End";
            _keyDisplayNames[Key.PageUp] = "Page Up";
            _keyDisplayNames[Key.PageDown] = "Page Down";
            _keyDisplayNames[Key.PrintScreen] = "Print Screen";
            _keyDisplayNames[Key.Scroll] = "Scroll Lock";
            _keyDisplayNames[Key.Pause] = "Pause";

            // Arrow keys
            _keyDisplayNames[Key.Up] = "Up Arrow";
            _keyDisplayNames[Key.Down] = "Down Arrow";
            _keyDisplayNames[Key.Left] = "Left Arrow";
            _keyDisplayNames[Key.Right] = "Right Arrow";

            // Numpad keys
            _keyDisplayNames[Key.NumLock] = "Num Lock";
            _keyDisplayNames[Key.Divide] = "/ (Numpad)";
            _keyDisplayNames[Key.Multiply] = "* (Numpad)";
            _keyDisplayNames[Key.Subtract] = "- (Numpad)";
            _keyDisplayNames[Key.Add] = "+ (Numpad)";
            _keyDisplayNames[Key.Decimal] = ". (Numpad)";
            _keyDisplayNames[Key.NumPad0] = "0 (Numpad)";
            _keyDisplayNames[Key.NumPad1] = "1 (Numpad)";
            _keyDisplayNames[Key.NumPad2] = "2 (Numpad)";
            _keyDisplayNames[Key.NumPad3] = "3 (Numpad)";
            _keyDisplayNames[Key.NumPad4] = "4 (Numpad)";
            _keyDisplayNames[Key.NumPad5] = "5 (Numpad)";
            _keyDisplayNames[Key.NumPad6] = "6 (Numpad)";
            _keyDisplayNames[Key.NumPad7] = "7 (Numpad)";
            _keyDisplayNames[Key.NumPad8] = "8 (Numpad)";
            _keyDisplayNames[Key.NumPad9] = "9 (Numpad)";
        }

        #endregion
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LaptopHealth.Behaviors
{
    public static class ComboBoxBehaviors
    {
        public static readonly DependencyProperty SuppressArrowKeysProperty =
            DependencyProperty.RegisterAttached(
                "SuppressArrowKeys",
                typeof(bool),
                typeof(ComboBoxBehaviors),
                new PropertyMetadata(false, OnSuppressArrowKeysChanged));

        public static bool GetSuppressArrowKeys(DependencyObject obj)
        {
            return (bool)obj.GetValue(SuppressArrowKeysProperty);
        }

        public static void SetSuppressArrowKeys(DependencyObject obj, bool value)
        {
            obj.SetValue(SuppressArrowKeysProperty, value);
        }

        private static void OnSuppressArrowKeysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ComboBox comboBox)
                return;

            if ((bool)e.NewValue)
            {
                comboBox.PreviewKeyDown += ComboBox_PreviewKeyDown;
            }
            else
            {
                comboBox.PreviewKeyDown -= ComboBox_PreviewKeyDown;
            }
        }

        private static void ComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is ComboBox comboBox &&
                (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down) &&
                (comboBox.IsDropDownOpen || comboBox.IsKeyboardFocusWithin))
            {
                e.Handled = true;
            }
        }
    }
}

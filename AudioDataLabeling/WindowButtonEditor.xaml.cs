using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AudioDataLabeling
{
    public partial class WindowButtonEditor : Window
    {
        Button _currentButton = null;
        public List<string> ResultButtonNames { get; private set; } = new List<string>();
        public WindowButtonEditor(List<string> existingNames)   
        {
            InitializeComponent();
            BuildEditorButtons(existingNames);
            CountButtonSlider.Value = existingNames.Count;
            CountButtonSlider.ValueChanged += CountButtonSlider_ValueChanged;
        }

        private void BuildEditorButtons(List<string> names)
        {
            if (ButtonsContainer != null) ButtonsContainer.Children.Clear();

            foreach (string name in names)
            {
                Button btn = CreateEditorButton(name);
                ButtonsContainer?.Children.Add(btn);
            }
        }

        private void CountButtonSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ButtonsContainer == null) return;

            int targetCount = (int)e.NewValue;
            int currentCount = ButtonsContainer.Children.Count;

            if (targetCount > currentCount)
            {
                for (int i = currentCount; i < targetCount; i++)
                {
                    Button btn = CreateEditorButton($"Button {i + 1}");
                    ButtonsContainer.Children.Add(btn);
                }
            }
            else if (targetCount < currentCount)
            {
                while (ButtonsContainer.Children.Count > targetCount)
                {
                    ButtonsContainer.Children.RemoveAt(ButtonsContainer.Children.Count - 1);
                }
                _currentButton = null;
                ButtonNameField.Text = "";
            }
        }
        private Button CreateEditorButton(string text)
        {
            Button btn = new Button
            {
                Content = text,
                Margin = new Thickness(3),
                FontSize = 14,
            };
            btn.Style = (Style)this.FindResource("EditorButtonStyle");
            btn.Click += DynamicButton_Click;
            return btn;
        }
        private void WBE_Ok_Click(object sender, RoutedEventArgs e)
        {
            ResultButtonNames.Clear();
            foreach (UIElement element in ButtonsContainer.Children)
            {
                if (element is Button btn)
                {
                    ResultButtonNames.Add(btn.Content.ToString() ?? string.Empty);
                }
            }
            this.DialogResult = true;
            this.Close();
        }

        private void ValidateButtonName()
        {
            if (_currentButton == null) return;

            string newName = ButtonNameField.Text.Trim();

            if (string.IsNullOrEmpty(newName))
            {
                ShowError("Button name cannot be empty.");
                return;
            }

            if (newName.Length > 20)
            {
                ShowError($"Name too long (currently: {newName.Length}/20 characters).");
                return;
            }

            ClearError();
            _currentButton.Content = newName;
        }
        private void ShowError(string message)
        {
            ButtonNameField.Tag = "Error";
            ErrorMessageLabel.Text = message;        
            ErrorMessageLabel.Visibility = Visibility.Visible;
            WBE_Ok.IsEnabled = false;                
        }

        private void ClearError()
        {
            ButtonNameField.Tag = null;
            ErrorMessageLabel.Visibility = Visibility.Collapsed;
            WBE_Ok.IsEnabled = true;                 
        }

        private void ButtonNameField_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateButtonName();
        }

        private void DynamicButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
                ButtonNameField.TextChanged -= ButtonNameField_TextChanged;

                ClearError();
                _currentButton = clickedButton;
                string buttonText = clickedButton.Content?.ToString() ?? string.Empty;

                ButtonNameField.Text = buttonText;
                ButtonNameField.Focus();
                ButtonNameField.SelectAll();
                ButtonNameField.TextChanged += ButtonNameField_TextChanged;
            }
        }
        private void ButtonNameField_LostFocus(object sender, RoutedEventArgs e)
        {
            TryApplyNewButtonName();
        }

        private void TryApplyNewButtonName()
        {
            if (_currentButton == null) return;

            string newName = ButtonNameField.Text.Trim();

            if (string.IsNullOrEmpty(newName) || newName.Length > 20)
            {
                ButtonNameField.Tag = "Error";
                WBE_Ok.IsEnabled = false;
                return;
            }

            ButtonNameField.Tag = null;
            WBE_Ok.IsEnabled = true;
            _currentButton.Content = newName;
        }
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ButtonNameField.Tag?.ToString() != "Error")
                {
                    Window parentWindow = Window.GetWindow(ButtonNameField);
                    if (parentWindow != null)
                    {
                        FocusManager.SetFocusedElement(parentWindow, parentWindow);
                    }
                }
                e.Handled = true;
            }
        }
    }
}

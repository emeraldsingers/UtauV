using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OpenUtau.App.Views {
    public partial class TypeInDialog : Window {
        public Action<string>? onFinish;
        private bool numbersOnly = false;

        public TypeInDialog() {
            InitializeComponent();
            OkButton.Click += OkButtonClick;
            TextBox.AttachedToVisualTree += (s, e) => { TextBox.SelectAll(); TextBox.Focus(); };
            TextBox.TextChanging += TextBoxTextChanging;
        }

        private void TextBoxTextChanging(object? sender, TextChangingEventArgs e) {
            if (numbersOnly && TextBox.Text != null) {
                string filtered = "";
                foreach (char c in TextBox.Text) {
                    if (char.IsDigit(c) || (c == '-' && filtered.Length == 0)) {
                        filtered += c;
                    }
                }
                if (filtered != TextBox.Text) {
                    int oldCaretIndex = TextBox.CaretIndex;
                    TextBox.Text = filtered;
                    TextBox.CaretIndex = Math.Min(oldCaretIndex, filtered.Length);
                }
            }
        }

        public void SetNumbersOnly(bool value) {
            numbersOnly = value;
        }

        public void SetPrompt(string prompt) {
            Prompt.IsVisible = true;
            Prompt.Text = prompt;
        }

        public void SetText(string text) {
            TextBox.Text = text;
            TextBox.SelectAll();
        }

        private void OkButtonClick(object? sender, RoutedEventArgs e) {
            Finish();
        }

        private void Finish() {
            if (onFinish != null) {
                onFinish.Invoke(TextBox.Text ?? string.Empty);
            }
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                e.Handled = true;
                Close();
            } else if (e.Key == Key.Enter) {
                e.Handled = true;
                Finish();
            } else {
                base.OnKeyDown(e);
            }
        }
    }
}

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OpenUtau.Core.Format;

namespace OpenUtau.App.Views {
    public partial class SvpImportDialog : Window {
        public bool Apply { get; private set; } = false;
        public SvpImportOptions Options { get; }

        public SvpImportDialog() : this(new SvpImportOptions()) {
        }

        public SvpImportDialog(SvpImportOptions options) {
            Options = options;
            DataContext = options;
            InitializeComponent();
        }

        private void Ok_OnClick(object sender, RoutedEventArgs e) => Finish();

        private void Cancel_OnClick(object sender, RoutedEventArgs e) => Close();

        private void Finish() {
            Apply = true;
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

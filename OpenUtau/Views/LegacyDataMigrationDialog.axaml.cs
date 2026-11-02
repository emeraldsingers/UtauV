using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using OpenUtau.Core.Util;
using Serilog;

namespace OpenUtau.App.Views {
    public partial class LegacyDataMigrationDialog : Window {
        private static readonly TimeSpan MinimumDisplay = TimeSpan.FromSeconds(3);
        private readonly LegacyDataMigration migration;
        private bool finished;

        public LegacyDataMigrationDialog() : this(LegacyDataMigration.ForCurrent()) { }

        public LegacyDataMigrationDialog(LegacyDataMigration migration) {
            InitializeComponent();
            this.migration = migration;
            Opened += (_, _) => Begin();
            Closing += (_, e) => e.Cancel = !finished;
        }

        private async void Begin() {
            Log.Information("Legacy data migration dialog opened.");
            var progress = new Progress<LegacyDataProgress>(OnProgress);
            try {
                await Task.WhenAll(Task.Run(() => migration.Run(progress)), Task.Delay(MinimumDisplay));
            } catch (Exception e) {
                Log.Error(e, "Legacy data migration failed.");
            }
            finished = true;
            Log.Information("Legacy data migration finished.");
            Close();
        }

        private void OnProgress(LegacyDataProgress p) {
            Bar.IsIndeterminate = p.Total == 0;
            if (p.Total > 0) {
                Bar.Value = (double)p.Done / p.Total;
            }
            CurrentText.Text = string.IsNullOrEmpty(p.Current)
                ? ThemeManager.GetString("migration.finishing")
                : string.Format(ThemeManager.GetString("migration.moving"), p.Current);
            CountText.Text = string.Format(ThemeManager.GetString("migration.count"), p.Done, p.Total);
        }
    }
}

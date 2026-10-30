using ReactiveUI.SourceGenerators;

namespace OpenUtau.App.ViewModels {
    public partial class VideoExportViewModel : ViewModelBase {
        [Reactive] public partial int ResolutionIndex { get; set; }
        [Reactive] public partial int FpsIndex { get; set; }

        public VideoExportViewModel() {
            ResolutionIndex = 1;
            FpsIndex = 1;
        }

        public double Resolution => ResolutionIndex switch {
            0 => 0.5,
            2 => 2,
            _ => 1,
        };
        public int Fps => FpsIndex switch {
            0 => 30,
            1 => 60,
            2 => 144,
            _ => 60,
        };
    }
}

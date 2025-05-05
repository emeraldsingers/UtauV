using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using OpenUtau.App.Controls;
using OpenUtau.Core.Util;
using ReactiveUI;

namespace OpenUtau.App {
    class ThemeChangedEvent { }

    class ThemeManager {
        public static bool IsDarkMode = false;
        public static IBrush ForegroundBrush = Brushes.Black;
        public static IBrush BackgroundBrush = Brushes.White;
        public static IBrush NeutralAccentBrush = Brushes.Gray;
        public static IBrush NeutralAccentBrushSemi = Brushes.Gray;
        public static IPen NeutralAccentPen = new Pen(Brushes.Black);
        public static IPen NeutralAccentPenSemi = new Pen(Brushes.Black);
        public static IBrush AccentBrush1 = Brushes.White;
        public static IPen AccentPen1 = new Pen(Brushes.White);
        public static IPen AccentPen1Thickness2 = new Pen(Brushes.White);
        public static IPen AccentPen1Thickness3 = new Pen(Brushes.White);
        public static IBrush AccentBrush1Semi = Brushes.Gray;
        public static IBrush AccentBrush2 = Brushes.Gray;
        public static IPen AccentPen2 = new Pen(Brushes.White);
        public static IPen AccentPen2Thickness2 = new Pen(Brushes.White);
        public static IPen AccentPen2Thickness3 = new Pen(Brushes.White);
        public static IBrush AccentBrush2Semi = Brushes.Gray;
        public static IBrush AccentBrush3 = Brushes.Gray;
        public static IPen AccentPen3 = new Pen(Brushes.White);
        public static IPen AccentPen3Thick = new Pen(Brushes.White);
        public static IBrush AccentBrush3Semi = Brushes.Gray;
        public static IBrush TickLineBrushLow = Brushes.Black;
        public static IBrush BarNumberBrush = Brushes.Black;
        public static IPen BarNumberPen = new Pen(Brushes.White);
        public static IBrush FinalPitchBrush = Brushes.Gray;
        public static IBrush TransSynthVTrack = Brushes.Transparent;
        public static IPen FinalPitchPen = new Pen(Brushes.Gray);
        public static IBrush WhiteKeyBrush = Brushes.White;
        public static IBrush WhiteKeyNameBrush = Brushes.Black;
        public static IBrush CenterKeyBrush = Brushes.White;
        public static IBrush CenterKeyNameBrush = Brushes.Black;
        public static IBrush BlackKeyBrush = Brushes.Black;
        public static IBrush BlackKeyNameBrush = Brushes.White;
        public static IBrush ExpBrush = Brushes.White;
        public static IBrush ExpNameBrush = Brushes.Black;
        public static IBrush ExpShadowBrush = Brushes.Gray;
        public static IBrush ExpShadowNameBrush = Brushes.White;
        public static IBrush ExpActiveBrush = Brushes.Black;
        public static IBrush ExpActiveNameBrush = Brushes.White;
        public static IPen KekoPitch = new Pen(new SolidColorBrush(Color.Parse("#BA55D3")), 1);
        public static IPen TilkePitch = new Pen(new SolidColorBrush(Color.Parse("#FFFFFF")), 1);
        public static IPen AkizoraPitch = new Pen(new SolidColorBrush(Color.Parse("#8b87a8")), 1);
        public static IPen AsoqwerPitchBend = new Pen(new SolidColorBrush(Color.Parse("#ff5d6a")), 1);
        public static IBrush AsoqwerPitchBendBrush = new SolidColorBrush(Color.Parse("#ff5d6a"));
        public static IPen ShiroiPitchBend = new Pen(new SolidColorBrush(Color.Parse("#ffffff")), 1);
        public static IBrush ShiroiPitchBendBrush = new SolidColorBrush(Color.Parse("#ffffff"));

        public static IPen AsoqwerPhoneme = new Pen(new SolidColorBrush(Color.Parse("#efddc7")), 1);
        public static IPen AsoqwerPhoneme2 = new Pen(new SolidColorBrush(Color.Parse("#ffd79f")), 1);
        public static IPen AsoqwerAccentPen2 = new Pen(new SolidColorBrush(Color.Parse("#ffd79f")), 1);
        public static IPen AsoqwerAccentPen2Thickness3 = new Pen(new SolidColorBrush(Color.Parse("#ffd79f")), 3);
        public static IBrush AsoqwerAccentColorSemi = new SolidColorBrush(Color.Parse("#ecefa8")) {
            Opacity = 0.5
        };
        public static IPen KekoPhoneme = new Pen(new SolidColorBrush(Color.Parse("#1691c7")), 1);
        public static IBrush TilkePhonemeBrush = new SolidColorBrush(Color.Parse("#7c0a02"));
        public static IPen KekoPhoneme2 = new Pen(new SolidColorBrush(Color.Parse("#20b8ff")), 1);
        public static IPen KekoAccentPen2 = new Pen(new SolidColorBrush(Color.Parse("#73c9ff")), 1);
        public static IPen KekoAccentPen2Thickness3 = new Pen(new SolidColorBrush(Color.Parse("#73c9ff")), 3);
        public static IBrush KekoAccentColorSemi = new SolidColorBrush(Color.Parse("#4597e5")) {
            Opacity = 0.5
        };
        public static IPen AkizoraPhoneme = new Pen(new SolidColorBrush(Color.Parse("#3daba9")), 1);
        public static IPen AkizoraPhoneme2 = new Pen(new SolidColorBrush(Color.Parse("#99aba9")), 1);
        public static IPen AkizoraAccentPen2 = new Pen(new SolidColorBrush(Color.Parse("#82aba9")), 1);
        public static IPen AkizoraAccentPen2Thickness3 = new Pen(new SolidColorBrush(Color.Parse("#82aba9")), 3);
        public static IBrush AkizoraAccentColorSemi = new SolidColorBrush(Color.Parse("#41cdaf")) {
            Opacity = 0.5
        };
        public static IPen TilkePhoneme = new Pen(new SolidColorBrush(Color.Parse("#484F85")), 1);
        public static IPen TilkePhoneme2 = new Pen(new SolidColorBrush(Color.Parse("#484F85")), 1);
        public static IPen TilkeAccentPen2 = new Pen(new SolidColorBrush(Color.Parse("#484F85")), 1);
        public static IPen TilkeAccentPen2Thickness3 = new Pen(new SolidColorBrush(Color.Parse("#484F85")), 3);
        public static IBrush TilkeAccentColorSemi = new SolidColorBrush(Color.Parse("#756cb6")) {
            Opacity = 0.5
        };

        public static IPen SimonPhoneme = new Pen(new SolidColorBrush(Color.Parse("#ffbb52")), 1);
        public static IPen SimonPhoneme2 = new Pen(new SolidColorBrush(Color.Parse("#ffbb52")), 1);
        public static IPen SimonAccentPen2 = new Pen(new SolidColorBrush(Color.Parse("#ffbb52")), 1);
        public static IPen SimonAccentPen2Thickness3 = new Pen(new SolidColorBrush(Color.Parse("#ffbb52")), 3);
        public static IBrush SimonAccentColorSemi = new SolidColorBrush(Color.Parse("#ffbb52")) {
            Opacity = 0.5
        };

        public static IPen MitsuoPhoneme = new Pen(new SolidColorBrush(Color.Parse("#7bd8ae")), 1);
        public static IPen MitsuoPhoneme2 = new Pen(new SolidColorBrush(Color.Parse("#ffa423")), 1);
        public static IPen MitsuoAccentPen2 = new Pen(new SolidColorBrush(Color.Parse("#ffa423")), 1);
        public static IPen MitsuoAccentPen2Thickness3 = new Pen(new SolidColorBrush(Color.Parse("#ffa423")), 3);
        public static IBrush MitsuoAccentColorSemi = new SolidColorBrush(Color.Parse("#ffa423")) {
            Opacity = 0.5
        };


        public static IPen ShiroiPhoneme = new Pen(new SolidColorBrush(Color.Parse("#781818")), 1);
        public static IPen ShiroiPhoneme2 = new Pen(new SolidColorBrush(Color.Parse("#b35353")), 1);
        public static IPen ShiroiAccentPen2 = new Pen(new SolidColorBrush(Color.Parse("#b35353")), 1);
        public static IPen ShiroiAccentPen2Thickness3 = new Pen(new SolidColorBrush(Color.Parse("#b35353")), 3);
        public static IBrush ShiroiAccentColorSemi = new SolidColorBrush(Color.Parse("#b35353")) {
            Opacity = 0.5
        };

        public static List<TrackColor> TrackColors = new List<TrackColor>(){
                new TrackColor("Pink", "#F06292", "#EC407A", "#F48FB1", "#FAC7D8"),
                new TrackColor("Red", "#EF5350", "#E53935", "#E57373", "#F2B9B9"),
                new TrackColor("Orange", "#FF8A65", "#FF7043", "#FFAB91", "#FFD5C8"),
                new TrackColor("Yellow", "#FBC02D", "#F9A825", "#FDD835", "#FEF1B6"),
                new TrackColor("Light Green", "#CDDC39", "#C0CA33", "#DCE775", "#F2F7CE"),
                new TrackColor("Green", "#66BB6A", "#43A047", "#A5D6A7", "#D2EBD3"),
                new TrackColor("Light Blue", "#4FC3F7", "#29B6F6", "#81D4FA", "#C0EAFD"),
                new TrackColor("Blue", "#4EA6EA", "#1E88E5", "#90CAF9", "#C8E5FC"),
                new TrackColor("Purple", "#BA68C8", "#AB47BC", "#CE93D8", "#E7C9EC"),
                new TrackColor("Pink2", "#E91E63", "#C2185B", "#F06292", "#F8B1C9"),
                new TrackColor("Red2", "#D32F2F", "#B71C1C", "#EF5350", "#F7A9A8"),
                new TrackColor("Orange2", "#FF5722", "#E64A19", "#FF7043", "#FFB8A1"),
                new TrackColor("Yellow2", "#FF8F00", "#FF7F00", "#FFB300", "#FFE097"),
                new TrackColor("Light Green2", "#AFB42B", "#9E9D24", "#CDDC39", "#E6EE9C"),
                new TrackColor("Green2", "#2E7D32", "#1B5E20", "#43A047", "#A1D0A3"),
                new TrackColor("Light Blue2", "#1976D2", "#0D47A1", "#2196F3", "#90CBF9"),
                new TrackColor("Blue2", "#3949AB", "#283593", "#5C6BC0", "#AEB5E0"),
                new TrackColor("Purple2", "#7B1FA2", "#4A148C", "#AB47BC", "#D5A3DE"),
                new TrackColor("asoqwer", "#fecd8a", "#212121", "#aec9d2", "#e32636"), 
                new TrackColor("keko", "#2a52be", "#EEEEEE", "#4597e5", "#90caf9"), 
                new TrackColor("tilke", "#484F85", "#EEEEEE", "#979FB6", "#979fb6"), 
                new TrackColor("akizora", "#82aba9", "#212121", "#66a5a4", "#c8e6c9"), 
                new TrackColor("simon", "#ffb63f", "#212121", "#84d8cd", "#ffcd41"),
                new TrackColor("mitsuo", "#ffa423", "#212121", "#7bd8ae", "#212121"),
                new TrackColor("shiroi", "#852929", "#212121", "#5d5959", "#212121"),
            };

        public static void LoadTheme() {
            if (Application.Current == null) {
                return;
            }
            IResourceDictionary resDict = Application.Current.Resources;
            object? outVar;
            IsDarkMode = false;
            var themeVariant = ThemeVariant.Default;
            if (resDict.TryGetResource("IsDarkMode", themeVariant, out outVar)) {
                IsDarkMode = true;
                
            }
            if (resDict.TryGetResource("SystemControlForegroundBaseHighBrush", themeVariant, out outVar)) {
                ForegroundBrush = (IBrush)outVar!;
            }
            if (resDict.TryGetResource("SystemControlBackgroundAltHighBrush", themeVariant, out outVar)) {
                BackgroundBrush = (IBrush)outVar!;
            }
            if (resDict.TryGetResource("NeutralAccentBrush", themeVariant, out outVar)) {
                NeutralAccentBrush = (IBrush)outVar!;
                NeutralAccentPen = new Pen(NeutralAccentBrush, 1);
            }
            if (resDict.TryGetResource("NeutralAccentBrushSemi", themeVariant, out outVar)) {
                NeutralAccentBrushSemi = (IBrush)outVar!;
                NeutralAccentPenSemi = new Pen(NeutralAccentBrushSemi, 1);
            }
            if (resDict.TryGetResource("AccentBrush1", themeVariant, out outVar)) {
                AccentBrush1 = (IBrush)outVar!;
                AccentPen1 = new Pen(AccentBrush1);
                AccentPen1Thickness2 = new Pen(AccentBrush1, 2);
                AccentPen1Thickness3 = new Pen(AccentBrush1, 3);
            }
            if (resDict.TryGetResource("AccentBrush1Semi", themeVariant, out outVar)) {
                AccentBrush1Semi = (IBrush)outVar!;
            }
            if (resDict.TryGetResource("AccentBrush2", themeVariant, out outVar)) {
                AccentBrush2 = (IBrush)outVar!;
                AccentPen2 = new Pen(AccentBrush2, 1);
                AccentPen2Thickness2 = new Pen(AccentBrush2, 2);
                AccentPen2Thickness3 = new Pen(AccentBrush2, 3);
            }
            if (resDict.TryGetResource("AccentBrush2Semi", themeVariant, out outVar)) {
                AccentBrush2Semi = (IBrush)outVar!;
            }
            if (resDict.TryGetResource("AccentBrush3", themeVariant, out outVar)) {
                AccentBrush3 = (IBrush)outVar!;
                AccentPen3 = new Pen(AccentBrush3, 1);
                AccentPen3Thick = new Pen(AccentBrush3, 3);
            }
            if (resDict.TryGetResource("AccentBrush3Semi", themeVariant, out outVar)) {
                AccentBrush3Semi = (IBrush)outVar!;
            }
            if (resDict.TryGetResource("TickLineBrushLow", themeVariant, out outVar)) {
                TickLineBrushLow = (IBrush)outVar!;
            }
            if (resDict.TryGetResource("BarNumberBrush", themeVariant, out outVar)) {
                BarNumberBrush = (IBrush)outVar!;
                BarNumberPen = new Pen(BarNumberBrush, 1);
            }
            if (resDict.TryGetResource("FinalPitchBrush", themeVariant, out outVar)) {
                FinalPitchBrush = (IBrush)outVar!;
                FinalPitchPen = new Pen(FinalPitchBrush, 1);
            }
            SetKeyboardBrush();
            TextLayoutCache.Clear();
            MessageBus.Current.SendMessage(new ThemeChangedEvent());
        }

        public static void ChangePianorollColor(string color) {
            if (Application.Current == null) {
                return;
            }
            try {
                IResourceDictionary resDict = Application.Current.Resources;
                TrackColor tcolor = GetTrackColor(color);
                
                resDict["SelectedTrackAccentBrush"] = tcolor.AccentColor;
                resDict["SelectedTrackAccentLightBrush"] = tcolor.AccentColorLight;
                resDict["SelectedTrackAccentLightBrushSemi"] = tcolor.AccentColorLightSemi;
                resDict["SelectedTrackAccentDarkBrush"] = tcolor.AccentColorDark;
                resDict["SelectedTrackCenterKeyBrush"] = tcolor.AccentColorCenterKey;

                SetKeyboardBrush();
            } catch { }
            MessageBus.Current.SendMessage(new ThemeChangedEvent());
        }
        private static void SetKeyboardBrush() {
            if (Application.Current == null) {
                return;
            }
            IResourceDictionary resDict = Application.Current.Resources;
            object? outVar;
            var themeVariant = ThemeVariant.Default;

            if (Preferences.Default.UseTrackColor) {
                if (IsDarkMode) {
                    if (resDict.TryGetResource("SelectedTrackAccentBrush", themeVariant, out outVar)) {
                        CenterKeyNameBrush = (IBrush)outVar!;
                        WhiteKeyBrush = (IBrush)outVar!;
                    }
                    if (resDict.TryGetResource("SelectedTrackCenterKeyBrush", themeVariant, out outVar)) {
                        CenterKeyBrush = (IBrush)outVar!;
                    }
                    if (resDict.TryGetResource("WhiteKeyNameBrush", themeVariant, out outVar)) {
                        WhiteKeyNameBrush = (IBrush)outVar!;
                    }
                    if (resDict.TryGetResource("BlackKeyBrush", themeVariant, out outVar)) {
                        BlackKeyBrush = (IBrush)outVar!;
                    }
                    if (resDict.TryGetResource("BlackKeyNameBrush", themeVariant, out outVar)) {
                        BlackKeyNameBrush = (IBrush)outVar!;
                    }
                    ExpBrush = BlackKeyBrush;
                    ExpNameBrush = BlackKeyNameBrush;
                    ExpActiveBrush = WhiteKeyBrush;
                    ExpActiveNameBrush = WhiteKeyNameBrush;
                    ExpShadowBrush = CenterKeyBrush;
                    ExpShadowNameBrush = CenterKeyNameBrush;
                } else { // LightMode
                    if (resDict.TryGetResource("SelectedTrackAccentBrush", themeVariant, out outVar)) {
                        CenterKeyNameBrush = (IBrush)outVar!;
                        WhiteKeyNameBrush = (IBrush)outVar!;
                        BlackKeyBrush = (IBrush)outVar!;
                    }
                    if (resDict.TryGetResource("SelectedTrackCenterKeyBrush", themeVariant, out outVar)) {
                        CenterKeyBrush = (IBrush)outVar!;
                    }
                    if (resDict.TryGetResource("WhiteKeyBrush", themeVariant, out outVar)) {
                        WhiteKeyBrush = (IBrush)outVar!;
                    }
                    if (resDict.TryGetResource("BlackKeyNameBrush", themeVariant, out outVar)) {
                        BlackKeyNameBrush = (IBrush)outVar!;
                    }
                    ExpBrush = WhiteKeyBrush;
                    ExpNameBrush = WhiteKeyNameBrush;
                    ExpActiveBrush = BlackKeyBrush;
                    ExpActiveNameBrush = BlackKeyNameBrush;
                    ExpShadowBrush = CenterKeyBrush;
                    ExpShadowNameBrush = CenterKeyNameBrush;
                }
            } else { // DefColor
                if (resDict.TryGetResource("WhiteKeyBrush", themeVariant, out outVar)) {
                    WhiteKeyBrush = (IBrush)outVar!;
                }
                if (resDict.TryGetResource("WhiteKeyNameBrush", themeVariant, out outVar)) {
                    WhiteKeyNameBrush = (IBrush)outVar!;
                }
                if (resDict.TryGetResource("CenterKeyBrush", themeVariant, out outVar)) {
                    CenterKeyBrush = (IBrush)outVar!;
                }
                if (resDict.TryGetResource("CenterKeyNameBrush", themeVariant, out outVar)) {
                    CenterKeyNameBrush = (IBrush)outVar!;
                }
                if (resDict.TryGetResource("BlackKeyBrush", themeVariant, out outVar)) {
                    BlackKeyBrush = (IBrush)outVar!;
                }
                if (resDict.TryGetResource("BlackKeyNameBrush", themeVariant, out outVar)) {
                    BlackKeyNameBrush = (IBrush)outVar!;
                }
                if (!IsDarkMode) {
                    ExpBrush = WhiteKeyBrush;
                    ExpNameBrush = WhiteKeyNameBrush;
                    ExpActiveBrush = BlackKeyBrush;
                    ExpActiveNameBrush = BlackKeyNameBrush;
                    ExpShadowBrush = CenterKeyBrush;
                    ExpShadowNameBrush = CenterKeyNameBrush;
                } else {
                    ExpBrush = BlackKeyBrush;
                    ExpNameBrush = BlackKeyNameBrush;
                    ExpActiveBrush = WhiteKeyBrush;
                    ExpActiveNameBrush = WhiteKeyNameBrush;
                    ExpShadowBrush = CenterKeyBrush;
                    ExpShadowNameBrush = CenterKeyNameBrush;
                }
            }
        }

        public static string GetString(string key) {
            if (Application.Current == null) {
                return key;
            }
            IResourceDictionary resDict = Application.Current.Resources;
            if (resDict.TryGetResource(key, ThemeVariant.Default, out var outVar) && outVar is string s) {
                return s;
            }
            return key;
        }

        public static bool TryGetString(string key, out string value) {
            if (Application.Current == null) {
                value = key;
                return false;
            }
            IResourceDictionary resDict = Application.Current.Resources;
            if (resDict.TryGetResource(key, ThemeVariant.Default, out var outVar) && outVar is string s) {
                value = s;
                return true;
            }
            value = key;
            return false;
        }

        public static TrackColor GetTrackColor(string name) {
            if (TrackColors.Any(c => c.Name == name)) {
                return TrackColors.First(c => c.Name == name);
            }

            if (TrackColors.Any(c => c.Name == "Green")) {
                return TrackColors.First(c => c.Name == "Green");
            }

            if (TrackColors.Count > 0) {
                return TrackColors[0];
            }

            return new TrackColor("Default", "#66BB6A", "#43A047", "#A5D6A7", "#D2EBD3");
        }

    }

    public class TrackColor {
        public string Name { get; set; } = "";
        public SolidColorBrush AccentColor { get; set; }
        // public SolidColorBrush AccentColorSemi { get; set; }
        public SolidColorBrush AccentColorDark { get; set; } // Pressed
        public SolidColorBrush AccentColorLight { get; set; } // PointerOver
        public SolidColorBrush AccentColorLightSemi { get; set; } // BackGround
        public SolidColorBrush AccentColorCenterKey { get; set; } // Keyboard

        public TrackColor(string name, string accentColor, string darkColor, string lightColor, string centerKey) {
            Name = name;
            AccentColor = SolidColorBrush.Parse(accentColor);
            // AccentColorSemi = SolidColorBrush.Parse(accentColor);
            // AccentColor.Opacity = 0.5;
            AccentColorDark = SolidColorBrush.Parse(darkColor);
            AccentColorLight = SolidColorBrush.Parse(lightColor);
            AccentColorLightSemi = SolidColorBrush.Parse(lightColor);
            AccentColorLightSemi.Opacity = 0.5;
            AccentColorCenterKey = SolidColorBrush.Parse(centerKey);
        }
    }
}

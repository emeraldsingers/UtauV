using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace OpenUtau.App.Controls {
    public class MarkdownView : StackPanel {
        private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex ListRegex = new(@"^\s*[-*+]\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex LinkRegex = new(@"\[([^\]]+)\]\(([^)\s]+)\)", RegexOptions.Compiled);
        private static readonly Regex BoldRegex = new(@"\*\*([^*]+)\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicRegex = new(@"(?<!\*)\*([^*]+)\*(?!\*)|_([^_]+)_", RegexOptions.Compiled);
        private static readonly Regex CodeRegex = new(@"`([^`]+)`", RegexOptions.Compiled);
        private static readonly Regex TableSeparatorRegex = new(@"^\s*\|?(\s*[:\-]+\s*\|\s*)*[:\-]+\s*\|?\s*$", RegexOptions.Compiled);
        private static readonly Regex TableRowRegex = new(@"^\s*\|(.*)\|\s*$", RegexOptions.Compiled);

        public static readonly StyledProperty<string?> MarkdownProperty =
            AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

        public string? Markdown {
            get => GetValue(MarkdownProperty);
            set => SetValue(MarkdownProperty, value);
        }

        public static readonly StyledProperty<TextAlignment> HeadingAlignmentProperty =
            AvaloniaProperty.Register<MarkdownView, TextAlignment>(nameof(HeadingAlignment), TextAlignment.Left);

        public TextAlignment HeadingAlignment {
            get => GetValue(HeadingAlignmentProperty);
            set => SetValue(HeadingAlignmentProperty, value);
        }

        static MarkdownView() {
            MarkdownProperty.Changed.AddClassHandler<MarkdownView>((view, _) => view.RenderMarkdown());
            HeadingAlignmentProperty.Changed.AddClassHandler<MarkdownView>((view, _) => view.RenderMarkdown());
        }

        public MarkdownView() {
            Spacing = 8;
        }

        private void RenderMarkdown() {
            Children.Clear();
            foreach (var control in MarkdownParser.Parse(Markdown, HeadingAlignment)) {
                Children.Add(control);
            }
        }

        private static class MarkdownParser {
            private static readonly FontFamily MonospaceFont = new("Consolas, Menlo, Monaco, monospace");
            private static readonly IBrush CodeBackground = new SolidColorBrush(Color.FromArgb((byte)40, (byte)127, (byte)127, (byte)127));

            public static IEnumerable<Control> Parse(string? markdown, TextAlignment headingAlignment = TextAlignment.Left) {
                if (string.IsNullOrWhiteSpace(markdown)) {
                    yield break;
                }

                var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                var index = 0;
                while (index < lines.Length) {
                    if (string.IsNullOrWhiteSpace(lines[index])) {
                        index++;
                        continue;
                    }

                    var headingMatch = HeadingRegex.Match(lines[index]);
                    if (headingMatch.Success) {
                        yield return CreateHeading(headingMatch.Groups[2].Value.Trim(), headingMatch.Groups[1].Value.Length, headingAlignment);
                        index++;
                        continue;
                    }

                    var listMatch = ListRegex.Match(lines[index]);
                    if (listMatch.Success) {
                        var list = new StackPanel {
                            Spacing = 4,
                            Margin = new Thickness(0, 0, 0, 4),
                        };
                        while (index < lines.Length) {
                            listMatch = ListRegex.Match(lines[index]);
                            if (!listMatch.Success) {
                                break;
                            }
                            list.Children.Add(CreateListItem(listMatch.Groups[1].Value.Trim()));
                            index++;
                        }
                        yield return list;
                        continue;
                    }

                    if (index + 1 < lines.Length && TableSeparatorRegex.IsMatch(lines[index + 1])) {
                        var headerLine = lines[index];
                        var separatorLine = lines[index + 1];
                        var rows = new List<string>();
                        index += 2;
                        while (index < lines.Length && lines[index].Contains("|")) {
                            rows.Add(lines[index]);
                            index++;
                        }
                        yield return CreateTable(headerLine, separatorLine, rows);
                        continue;
                    }

                    var paragraphLines = new List<string>();
                    while (index < lines.Length &&
                        !string.IsNullOrWhiteSpace(lines[index]) &&
                        !HeadingRegex.IsMatch(lines[index]) &&
                        !ListRegex.IsMatch(lines[index])) {
                        paragraphLines.Add(lines[index].Trim());
                        index++;
                    }

                    if (paragraphLines.Count > 0) {
                        yield return CreateParagraph(string.Join(" ", paragraphLines));
                    }
                }
            }

            private static Control CreateHeading(string text, int level, TextAlignment alignment) {
                var textBlock = CreateTextBlock();
                textBlock.Margin = level <= 2 ? new Thickness(0, 16, 0, 2) : new Thickness(0, 12, 0, 0);
                textBlock.TextAlignment = alignment;
                textBlock.FontWeight = FontWeight.Bold;
                textBlock.FontSize = level switch {
                    1 => 28,
                    2 => 22,
                    3 => 18,
                    _ => 16,
                };
                AppendInlines(textBlock.Inlines!, text);
                return textBlock;
            }

            private static Control CreateParagraph(string text) {
                var textBlock = CreateTextBlock();
                AppendInlines(textBlock.Inlines!, text);
                return textBlock;
            }

            private static Control CreateListItem(string text) {
                var grid = new Grid {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                };
                var bullet = new TextBlock {
                    Text = "-",
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Top,
                };
                var content = CreateTextBlock();
                AppendInlines(content.Inlines!, text);
                Grid.SetColumn(content, 1);
                grid.Children.Add(bullet);
                grid.Children.Add(content);
                return grid;
            }

            private static Control CreateTable(string headerLine, string separatorLine, List<string> dataLines) {
                var headerCells = headerLine.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToList();
                var separatorCells = separatorLine.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToList();
                var columnCount = Math.Max(headerCells.Count, separatorCells.Count);

                var grid = new Grid {
                    Margin = new Thickness(0, 4, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                };

                var colDefinitions = new ColumnDefinitions();
                for (int i = 0; i < columnCount; i++) {
                    colDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                }
                grid.ColumnDefinitions = colDefinitions;

                var alignments = new List<HorizontalAlignment>();
                for (int i = 0; i < columnCount; i++) {
                    var sep = i < separatorCells.Count ? separatorCells[i] : "";
                    if (sep.StartsWith(":") && sep.EndsWith(":")) {
                        alignments.Add(HorizontalAlignment.Center);
                    } else if (sep.EndsWith(":")) {
                        alignments.Add(HorizontalAlignment.Right);
                    } else {
                        alignments.Add(HorizontalAlignment.Left);
                    }
                }

                for (int i = 0; i < headerCells.Count; i++) {
                    var cell = CreateTableCell(headerCells[i], true, alignments[i]);
                    Grid.SetColumn(cell, i);
                    Grid.SetRow(cell, 0);
                    grid.Children.Add(cell);
                }

                var rowDefinitions = new RowDefinitions();
                rowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Header
                for (int i = 0; i < dataLines.Count; i++) {
                    rowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    var cells = dataLines[i].Split('|', StringSplitOptions.None);
                    var rowCells = dataLines[i].Trim().Trim('|').Split('|').Select(c => c.Trim()).ToList();
                    
                    for (int j = 0; j < Math.Min(rowCells.Count, columnCount); j++) {
                        var cell = CreateTableCell(rowCells[j], false, alignments[j]);
                        Grid.SetColumn(cell, j);
                        Grid.SetRow(cell, i + 1);
                        grid.Children.Add(cell);
                    }
                }
                grid.RowDefinitions = rowDefinitions;

                return new Border {
                    BorderBrush = new SolidColorBrush(Color.FromArgb((byte)60, (byte)127, (byte)127, (byte)127)),
                    BorderThickness = new Thickness(1, 1, 0, 0),
                    Child = grid,
                };
            }

            private static Control CreateTableCell(string text, bool isHeader, HorizontalAlignment alignment) {
                var container = new Border {
                    Padding = new Thickness(12, 6),
                    BorderBrush = new SolidColorBrush(Color.FromArgb((byte)60, (byte)127, (byte)127, (byte)127)),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Background = isHeader ? new SolidColorBrush(Color.FromArgb((byte)30, (byte)127, (byte)127, (byte)127)) : null,
                };

                var textBlock = CreateTextBlock();
                textBlock.HorizontalAlignment = alignment;
                if (isHeader) {
                    textBlock.FontWeight = FontWeight.Bold;
                }
                AppendInlines(textBlock.Inlines!, text);
                container.Child = textBlock;
                return container;
            }

            private static TextBlock CreateTextBlock() {
                return new TextBlock {
                    TextWrapping = TextWrapping.Wrap,
                };
            }

            private static void AppendInlines(InlineCollection inlines, string text) {
                var position = 0;
                while (position < text.Length) {
                    var next = FindNextToken(text, position);
                    if (next == null) {
                        AddRun(inlines, text[position..]);
                        break;
                    }

                    if (next.Index > position) {
                        AddRun(inlines, text[position..next.Index]);
                    }

                    switch (next.Kind) {
                        case InlineTokenKind.Link:
                            AddLink(inlines, next.Groups[1].Value, next.Groups[2].Value);
                            break;
                        case InlineTokenKind.Bold:
                            AddRun(inlines, next.Groups[1].Value, fontWeight: FontWeight.Bold);
                            break;
                        case InlineTokenKind.Italic:
                            var italicText = next.Groups[1].Success ? next.Groups[1].Value : next.Groups[2].Value;
                            AddRun(inlines, italicText, fontStyle: FontStyle.Italic);
                            break;
                        case InlineTokenKind.Code:
                            AddRun(inlines, next.Groups[1].Value, fontFamily: MonospaceFont, background: CodeBackground);
                            break;
                    }

                    position = next.Index + next.Length;
                }
            }

            private static InlineTokenMatch? FindNextToken(string text, int start) {
                InlineTokenMatch? best = null;
                TrySelect(LinkRegex.Match(text, start), InlineTokenKind.Link);
                TrySelect(BoldRegex.Match(text, start), InlineTokenKind.Bold);
                TrySelect(ItalicRegex.Match(text, start), InlineTokenKind.Italic);
                TrySelect(CodeRegex.Match(text, start), InlineTokenKind.Code);
                return best;

                void TrySelect(Match match, InlineTokenKind kind) {
                    if (!match.Success) {
                        return;
                    }
                    if (best == null || match.Index < best.Index) {
                        best = new InlineTokenMatch(match, kind);
                    }
                }
            }

            private static void AddRun(
                InlineCollection inlines,
                string text,
                FontWeight? fontWeight = null,
                FontStyle? fontStyle = null,
                FontFamily? fontFamily = null,
                IBrush? background = null) {
                if (string.IsNullOrEmpty(text)) {
                    return;
                }
                var run = new Run(text);
                if (fontWeight.HasValue) {
                    run.FontWeight = fontWeight.Value;
                }
                if (fontStyle.HasValue) {
                    run.FontStyle = fontStyle.Value;
                }
                if (fontFamily != null) {
                    run.FontFamily = fontFamily;
                }
                if (background != null) {
                    run.Background = background;
                }
                inlines.Add(run);
            }

            private static void AddLink(InlineCollection inlines, string text, string url) {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) {
                    AddRun(inlines, text);
                    return;
                }
                var link = new HyperlinkButton {
                    Content = text,
                    NavigateUri = uri,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0),
                    MinWidth = 0,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                inlines.Add(link);
            }

            private sealed record InlineTokenMatch(Match Match, InlineTokenKind Kind) {
                public int Index => Match.Index;
                public int Length => Match.Length;
                public GroupCollection Groups => Match.Groups;
            }

            private enum InlineTokenKind {
                Link,
                Bold,
                Italic,
                Code,
            }
        }
    }
}

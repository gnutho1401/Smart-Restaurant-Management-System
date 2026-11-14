using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SmartRestaurant.WPF.Common;

/// <summary>
/// Attached behavior for WPF TextBlock to render rich Markdown syntax,
/// including headings, bold, italic, bullet lists, badges, divider lines, and structured tables.
/// Transforms raw GenAI outputs into elegant, executive-ready presentations.
/// </summary>
public static class MarkdownHelper
{
    public static readonly DependencyProperty MarkdownTextProperty =
        DependencyProperty.RegisterAttached(
            "MarkdownText",
            typeof(string),
            typeof(MarkdownHelper),
            new PropertyMetadata(null, OnMarkdownTextChanged));

    public static string? GetMarkdownText(DependencyObject obj) => (string?)obj.GetValue(MarkdownTextProperty);
    public static void SetMarkdownText(DependencyObject obj, string? value) => obj.SetValue(MarkdownTextProperty, value);

    private static void OnMarkdownTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock) return;

        textBlock.Inlines.Clear();
        string? text = e.NewValue as string;
        if (string.IsNullOrWhiteSpace(text)) return;

        try
        {
            RenderMarkdown(textBlock, text);
        }
        catch
        {
            // Fallback to plain text on any unexpected parsing exception
            textBlock.Inlines.Clear();
            textBlock.Inlines.Add(new Run(text));
        }
    }

    private static void RenderMarkdown(TextBlock target, string markdown)
    {
        // Normalize line endings
        string normalized = markdown.Replace("\r\n", "\n").Replace("\r", "\n");
        string[] rawLines = normalized.Split('\n');

        int lineIndex = 0;
        bool isFirstLine = true;

        while (lineIndex < rawLines.Length)
        {
            string line = rawLines[lineIndex];

            // 1. Check for Table block
            if (IsTableLine(line) && lineIndex + 1 < rawLines.Length && IsTableSeparator(rawLines[lineIndex + 1]))
            {
                var tableLines = new List<string>();
                while (lineIndex < rawLines.Length && IsTableLine(rawLines[lineIndex]))
                {
                    tableLines.Add(rawLines[lineIndex]);
                    lineIndex++;
                }

                if (!isFirstLine) target.Inlines.Add(new LineBreak());
                target.Inlines.Add(RenderTable(tableLines));
                target.Inlines.Add(new LineBreak());
                isFirstLine = false;
                continue;
            }

            // 2. Horizontal divider: --- or ***
            string trimmed = line.Trim();
            if (trimmed == "---" || trimmed == "***" || trimmed == "___")
            {
                if (!isFirstLine) target.Inlines.Add(new LineBreak());
                target.Inlines.Add(CreateHorizontalRule());
                target.Inlines.Add(new LineBreak());
                isFirstLine = false;
                lineIndex++;
                continue;
            }

            // 3. Headings: #, ##, ###, ####
            if (trimmed.StartsWith("#"))
            {
                int level = 0;
                while (level < trimmed.Length && trimmed[level] == '#') level++;

                string headingText = trimmed.Substring(level).Trim();
                if (!isFirstLine) target.Inlines.Add(new LineBreak());

                var headingSpan = new Span();
                switch (level)
                {
                    case 1:
                        headingSpan.FontSize = 17;
                        headingSpan.FontWeight = FontWeights.Bold;
                        headingSpan.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)); // Slate 900
                        break;
                    case 2:
                        headingSpan.FontSize = 15;
                        headingSpan.FontWeight = FontWeights.Bold;
                        headingSpan.Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)); // Slate 800
                        break;
                    case 3:
                        headingSpan.FontSize = 14;
                        headingSpan.FontWeight = FontWeights.SemiBold;
                        headingSpan.Foreground = new SolidColorBrush(Color.FromRgb(79, 70, 229)); // Indigo 600
                        break;
                    default:
                        headingSpan.FontSize = 13;
                        headingSpan.FontWeight = FontWeights.SemiBold;
                        headingSpan.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)); // Slate 600
                        break;
                }

                ParseFormattedInlines(headingSpan, headingText);
                target.Inlines.Add(headingSpan);
                target.Inlines.Add(new LineBreak());
                isFirstLine = false;
                lineIndex++;
                continue;
            }

            // 4. Bullet lists: * item, - item, or numbered list
            if (trimmed.StartsWith("* ") || trimmed.StartsWith("- ") || trimmed.StartsWith("• ") ||
                Regex.IsMatch(trimmed, @"^\d+\.\s+"))
            {
                if (!isFirstLine) target.Inlines.Add(new LineBreak());

                int indent = line.Length - line.TrimStart().Length;
                string indentStr = indent >= 4 ? "    ◦ " : (indent >= 2 ? "  • " : "• ");

                var bulletRun = new Run(indentStr)
                {
                    Foreground = indent >= 4
                        ? new SolidColorBrush(Color.FromRgb(100, 116, 139))
                        : new SolidColorBrush(Color.FromRgb(99, 102, 241)), // Indigo bullet
                    FontWeight = FontWeights.Bold
                };
                target.Inlines.Add(bulletRun);

                string contentText;
                if (trimmed.StartsWith("* ") || trimmed.StartsWith("- ") || trimmed.StartsWith("• "))
                {
                    contentText = trimmed.Substring(2).Trim();
                }
                else
                {
                    var match = Regex.Match(trimmed, @"^(\d+\.\s+)(.*)$");
                    contentText = match.Groups[2].Value.Trim();
                }

                var itemSpan = new Span();
                ParseFormattedInlines(itemSpan, contentText);
                target.Inlines.Add(itemSpan);
                target.Inlines.Add(new LineBreak());
                isFirstLine = false;
                lineIndex++;
                continue;
            }

            // 5. Blank line -> Paragraph spacing
            if (string.IsNullOrWhiteSpace(line))
            {
                if (!isFirstLine) target.Inlines.Add(new LineBreak());
                lineIndex++;
                continue;
            }

            // 6. Normal line
            if (!isFirstLine) target.Inlines.Add(new LineBreak());
            var normalSpan = new Span();
            ParseFormattedInlines(normalSpan, line);
            target.Inlines.Add(normalSpan);
            isFirstLine = false;
            lineIndex++;
        }
    }

    private static bool IsTableLine(string line) => line.Trim().StartsWith("|") && line.Trim().EndsWith("|");
    private static bool IsTableSeparator(string line) => Regex.IsMatch(line.Trim(), @"^\|[\s\-:]+(\|[\s\-:]+)+\|$");

    private static InlineUIContainer RenderTable(List<string> lines)
    {
        var grid = new Grid
        {
            Margin = new Thickness(0, 4, 0, 6)
        };

        // Extract header and rows
        string headerLine = lines[0];
        var headers = headerLine.Split('|', StringSplitOptions.TrimEntries)
            .Where((col, idx) => idx > 0 && idx < headerLine.Split('|').Length - 1).ToList();

        int colCount = headers.Count;
        for (int i = 0; i < colCount; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        // Header row
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int c = 0; c < colCount; c++)
        {
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)), // Slate 100
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6)
            };
            var headerText = new TextBlock
            {
                Text = headers[c],
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                TextWrapping = TextWrapping.Wrap
            };
            headerBorder.Child = headerText;
            Grid.SetRow(headerBorder, 0);
            Grid.SetColumn(headerBorder, c);
            grid.Children.Add(headerBorder);
        }

        // Data rows (skip line 1 which is separator |---|---|)
        int rowIndex = 1;
        for (int r = 2; r < lines.Count; r++)
        {
            string rowLine = lines[r];
            var cells = rowLine.Split('|', StringSplitOptions.TrimEntries)
                .Where((col, idx) => idx > 0 && idx < rowLine.Split('|').Length - 1).ToList();

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var rowBg = (rowIndex % 2 == 1)
                ? new SolidColorBrush(Color.FromRgb(255, 255, 255))
                : new SolidColorBrush(Color.FromRgb(248, 250, 252));

            for (int c = 0; c < colCount; c++)
            {
                string cellText = c < cells.Count ? cells[c] : string.Empty;
                var cellBorder = new Border
                {
                    Background = rowBg,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 5, 8, 5)
                };
                var tb = new TextBlock
                {
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                    TextWrapping = TextWrapping.Wrap
                };
                ParseFormattedInlines(tb, cellText);
                cellBorder.Child = tb;
                Grid.SetRow(cellBorder, rowIndex);
                Grid.SetColumn(cellBorder, c);
                grid.Children.Add(cellBorder);
            }
            rowIndex++;
        }

        var containerBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            BorderThickness = new Thickness(1),
            Child = grid,
            Margin = new Thickness(0, 4, 0, 4)
        };

        return new InlineUIContainer(containerBorder);
    }

    private static InlineUIContainer CreateHorizontalRule()
    {
        var border = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            Margin = new Thickness(0, 6, 0, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        return new InlineUIContainer(border);
    }

    private static void ParseFormattedInlines(dynamic container, string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Match tags like [Khẩn cấp], **bold**, *italic*, `code`
        var tokenRegex = new Regex(@"(\*\*\*(.+?)\*\*\*)|(\*\*(.+?)\*\*)|(\*(.+?)\*)|(`(.+?)`)|(\[(🚨|⚠️|✅|⭐|🐄|❓|🐕|💡)?\s*([A-Za-z0-9_\sÀ-ỹ\-]+)\])");
        int lastIndex = 0;

        foreach (Match match in tokenRegex.Matches(text))
        {
            if (match.Index > lastIndex)
            {
                container.Inlines.Add(new Run(text.Substring(lastIndex, match.Index - lastIndex)));
            }

            if (match.Groups[1].Success) // ***bold italic***
            {
                var run = new Run(match.Groups[2].Value) { FontWeight = FontWeights.Bold, FontStyle = FontStyles.Italic };
                container.Inlines.Add(run);
            }
            else if (match.Groups[3].Success) // **bold**
            {
                var boldSpan = new Bold();
                boldSpan.Inlines.Add(new Run(match.Groups[4].Value));
                // Highlight prices or keywords with crisp contrast
                string val = match.Groups[4].Value;
                if (val.Contains("VND") || val.Contains("đ"))
                {
                    boldSpan.Foreground = new SolidColorBrush(Color.FromRgb(15, 118, 110)); // Teal 700
                }
                container.Inlines.Add(boldSpan);
            }
            else if (match.Groups[5].Success) // *italic*
            {
                var run = new Run(match.Groups[6].Value)
                {
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105))
                };
                container.Inlines.Add(run);
            }
            else if (match.Groups[7].Success) // `code`
            {
                var codeBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(2, 0, 2, 0)
                };
                codeBorder.Child = new TextBlock
                {
                    Text = match.Groups[8].Value,
                    FontFamily = new FontFamily("Consolas, Courier New"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(225, 29, 72)) // Rose 600
                };
                container.Inlines.Add(new InlineUIContainer(codeBorder));
            }
            else if (match.Groups[9].Success) // [Badge Tag]
            {
                string icon = match.Groups[10].Value;
                string label = match.Groups[11].Value.Trim();
                string fullBadgeText = string.IsNullOrEmpty(icon) ? label : $"{icon} {label}";

                var (bg, fg, borderCol) = GetBadgeColors(fullBadgeText);
                var badgeBorder = new Border
                {
                    Background = bg,
                    BorderBrush = borderCol,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(2, 0, 2, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                badgeBorder.Child = new TextBlock
                {
                    Text = fullBadgeText,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = fg
                };
                container.Inlines.Add(new InlineUIContainer(badgeBorder));
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
        {
            container.Inlines.Add(new Run(text.Substring(lastIndex)));
        }
    }

    private static (Brush bg, Brush fg, Brush border) GetBadgeColors(string text)
    {
        string lower = text.ToLower();
        if (lower.Contains("khẩn cấp") || lower.Contains("cực kỳ") || lower.Contains("lỗi") || lower.Contains("🚨") || lower.Contains("dog"))
        {
            return (
                new SolidColorBrush(Color.FromRgb(254, 242, 242)), // Red 50
                new SolidColorBrush(Color.FromRgb(185, 28, 28)),  // Red 700
                new SolidColorBrush(Color.FromRgb(254, 202, 202)));// Red 200
        }
        if (lower.Contains("lưu ý") || lower.Contains("theo dõi") || lower.Contains("⚠️") || lower.Contains("puzzle"))
        {
            return (
                new SolidColorBrush(Color.FromRgb(254, 243, 199)), // Amber 50
                new SolidColorBrush(Color.FromRgb(180, 83, 9)),   // Amber 700
                new SolidColorBrush(Color.FromRgb(253, 230, 138)));// Amber 200
        }
        if (lower.Contains("đủ dùng") || lower.Contains("an toàn") || lower.Contains("thành công") || lower.Contains("✅") || lower.Contains("star"))
        {
            return (
                new SolidColorBrush(Color.FromRgb(236, 253, 245)), // Emerald 50
                new SolidColorBrush(Color.FromRgb(4, 120, 87)),   // Emerald 700
                new SolidColorBrush(Color.FromRgb(167, 243, 208)));// Emerald 200
        }
        if (lower.Contains("gemini") || lower.Contains("mistral") || lower.Contains("ai") || lower.Contains("plowhorse"))
        {
            return (
                new SolidColorBrush(Color.FromRgb(238, 242, 255)), // Indigo 50
                new SolidColorBrush(Color.FromRgb(67, 56, 202)),  // Indigo 700
                new SolidColorBrush(Color.FromRgb(199, 210, 254)));// Indigo 200
        }

        // Default neutral badge
        return (
            new SolidColorBrush(Color.FromRgb(241, 245, 249)),
            new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            new SolidColorBrush(Color.FromRgb(226, 232, 240)));
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ProjectManager.Avalonia.Behaviors;

/// <summary>
/// Avalonia attached behavior for ANSI terminal text rendering on SelectableTextBlock.
/// Parses ANSI escape sequences and creates styled Run inlines with colored text.
/// Supports cross-line text selection via a single SelectableTextBlock.
/// Supports 16 basic colors (Campbell scheme matching Windows Terminal), 256-color, true-color (RGB),
/// bold, italic, underline, strikethrough, dim, inverse, hidden.
/// Handles CSI sequences: m (SGR), K (clear line), J (clear screen), H/f (cursor position).
/// </summary>
public static class AnsiTextBehavior
{
    // ===================== Attached Properties =====================

    /// <summary>
    /// The collection of text lines to render. Typically an ObservableCollection&lt;string&gt;
    /// that also implements INotifyCollectionChanged for incremental updates.
    /// </summary>
    public static readonly AttachedProperty<IEnumerable<string>?> ItemsSourceProperty =
        AvaloniaProperty.RegisterAttached<SelectableTextBlock, IEnumerable<string>?>("ItemsSource", typeof(AnsiTextBehavior));

    /// <summary>
    /// Whether to parse ANSI escape codes in the text. Default is true.
    /// </summary>
    public static readonly AttachedProperty<bool> EnableAnsiParsingProperty =
        AvaloniaProperty.RegisterAttached<SelectableTextBlock, bool>("EnableAnsiParsing", typeof(AnsiTextBehavior), true);

    /// <summary>
    /// Whether to auto-scroll to the bottom when new content is added. Default is true.
    /// </summary>
    public static readonly AttachedProperty<bool> AutoScrollProperty =
        AvaloniaProperty.RegisterAttached<SelectableTextBlock, bool>("AutoScroll", typeof(AnsiTextBehavior), true);

    /// <summary>
    /// Maximum number of text lines to keep in the display. Default is 2000.
    /// </summary>
    public static readonly AttachedProperty<int> MaxItemsProperty =
        AvaloniaProperty.RegisterAttached<SelectableTextBlock, int>("MaxItems", typeof(AnsiTextBehavior), 2000);

    // ===================== Getters / Setters =====================

    public static void SetItemsSource(SelectableTextBlock control, IEnumerable<string>? value) =>
        control.SetValue(ItemsSourceProperty, value);

    public static IEnumerable<string>? GetItemsSource(SelectableTextBlock control) =>
        control.GetValue(ItemsSourceProperty);

    public static void SetEnableAnsiParsing(SelectableTextBlock control, bool value) =>
        control.SetValue(EnableAnsiParsingProperty, value);

    public static bool GetEnableAnsiParsing(SelectableTextBlock control) =>
        control.GetValue(EnableAnsiParsingProperty);

    public static void SetAutoScroll(SelectableTextBlock control, bool value) =>
        control.SetValue(AutoScrollProperty, value);

    public static bool GetAutoScroll(SelectableTextBlock control) =>
        control.GetValue(AutoScrollProperty);

    public static void SetMaxItems(SelectableTextBlock control, int value) =>
        control.SetValue(MaxItemsProperty, value);

    public static int GetMaxItems(SelectableTextBlock control) =>
        control.GetValue(MaxItemsProperty);

    // ===================== State Management =====================

    private static readonly Dictionary<SelectableTextBlock, BehaviorState> States = new();

    static AnsiTextBehavior()
    {
        ItemsSourceProperty.Changed.Subscribe(new PropertyObserver(OnItemsSourceChanged));
    }

    private static void OnItemsSourceChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Sender is not SelectableTextBlock textBlock) return;

        // Unsubscribe from old collection
        if (States.TryGetValue(textBlock, out var oldState))
        {
            if (oldState.SourceCollection is INotifyCollectionChanged oldNotify)
            {
                try { oldNotify.CollectionChanged -= oldState.CollectionChangedHandler!; } catch { /* ignore */ }
            }
            States.Remove(textBlock);
        }

        var newSource = e.NewValue as IEnumerable<string>;
        if (newSource == null) return;

        // Clear existing inlines
        textBlock.Inlines?.Clear();

        var parseAnsi = GetEnableAnsiParsing(textBlock);
        var state = new BehaviorState
        {
            SourceCollection = newSource as INotifyCollectionChanged,
            TextBlock = textBlock,
            CurrentStyle = new AnsiStyle(),
            LineCount = 0
        };

        // Initial full render of existing items
        foreach (var line in newSource)
        {
            AppendLineToTextBlock(state, line ?? string.Empty, parseAnsi);
        }

        // Subscribe to incremental changes
        if (state.SourceCollection != null)
        {
            NotifyCollectionChangedEventHandler handler = (s, ev) =>
            {
                var currentParseAnsi = GetEnableAnsiParsing(textBlock);

                if (ev.Action == NotifyCollectionChangedAction.Add && ev.NewItems != null)
                {
                    foreach (var item in ev.NewItems)
                    {
                        AppendLineToTextBlock(state, item?.ToString() ?? string.Empty, currentParseAnsi);
                    }
                }
                else if (ev.Action == NotifyCollectionChangedAction.Reset)
                {
                    textBlock.Inlines?.Clear();
                    state.CurrentStyle.Reset();
                    state.LineCount = 0;
                }

                // Enforce maximum line count
                var max = GetMaxItems(textBlock);
                TrimOldLines(state, max);

                // Auto-scroll to bottom
                if (GetAutoScroll(textBlock))
                {
                    ScrollToEnd(textBlock);
                }
            };

            state.CollectionChangedHandler = handler;
            state.SourceCollection.CollectionChanged += handler;
        }

        States[textBlock] = state;

        // Attempt to hook ScrollViewer for auto-scroll disable detection (deferred until layout)
        Dispatcher.UIThread.Post(() => HookScrollViewerForUserScroll(textBlock), DispatcherPriority.Loaded);
    }

    // ===================== Line Rendering =====================

    /// <summary>
    /// Processes a single string (which may contain newlines, control chars, ESC sequences)
    /// and appends styled Run inlines to the SelectableTextBlock.
    /// </summary>
    private static void AppendLineToTextBlock(BehaviorState state, string line, bool parseAnsi)
    {
        if (string.IsNullOrEmpty(line)) return;
        if (string.IsNullOrWhiteSpace(line)) return;

        // Add newline separator if there's existing content
        var inlines = state.TextBlock.Inlines;
        if (inlines == null) return;

        if (inlines.Count > 0)
        {
            inlines.Add(new Run("\n"));
            state.LineCount++;
        }

        var currentStyle = state.CurrentStyle;
        bool pendingCarriageReturn = false;

        int i = 0;
        while (i < line.Length)
        {
            char ch = line[i];

            // Carriage return: mark pending, will clear current line on next text write
            if (ch == '\r')
            {
                pendingCarriageReturn = true;
                i++;
                continue;
            }

            // Newline within the string: add newline run
            if (ch == '\n')
            {
                if (pendingCarriageReturn)
                {
                    pendingCarriageReturn = false;
                    // \r\n together = single line break, don't add extra
                }
                else
                {
                    inlines.Add(new Run("\n"));
                    state.LineCount++;
                }
                i++;
                continue;
            }

            // Backspace: remove last character from last run
            if (ch == '\b')
            {
                RemoveLastRunChar(inlines);
                i++;
                continue;
            }

            // ESC: attempt to parse CSI escape sequence
            if (ch == '\u001B' && i + 1 < line.Length)
            {
                int consumed = TryConsumeEscape(currentStyle, inlines, line, i);
                if (consumed > 0)
                {
                    i += consumed;
                    continue;
                }
            }

            // Regular printable text: collect run until next control char or ESC
            int start = i;
            while (i < line.Length)
            {
                char c2 = line[i];
                if (c2 == '\r' || c2 == '\n' || c2 == '\b' || c2 == '\u001B') break;
                i++;
            }

            var segment = line.Substring(start, i - start);
            if (segment.Length > 0)
            {
                // On carriage return, clear the current line (runs after last newline)
                if (pendingCarriageReturn)
                {
                    ClearCurrentLine(inlines);
                    pendingCarriageReturn = false;
                }

                // Detect and render timestamp prefix [HH:mm:ss] in dim color at line start
                if (IsAtLineStart(inlines) && TryConsumeTimestampPrefix(ref segment, out var ts))
                {
                    inlines.Add(CreateTimestampRun(ts));
                }

                if (parseAnsi)
                    AppendTextWithAnsi(inlines, segment, currentStyle);
                else
                    inlines.Add(new Run(segment));
            }
        }
    }

    /// <summary>
    /// Checks if we're at the start of a line (no inlines or last inline is a newline).
    /// </summary>
    private static bool IsAtLineStart(InlineCollection inlines)
    {
        if (inlines.Count == 0) return true;
        var last = inlines.LastOrDefault();
        return last is Run run && run.Text == "\n";
    }

    /// <summary>
    /// Clears all runs after the last newline (simulates carriage return line clearing).
    /// </summary>
    private static void ClearCurrentLine(InlineCollection inlines)
    {
        // Find the index of the last newline run
        int lastNewlineIndex = -1;
        for (int i = inlines.Count - 1; i >= 0; i--)
        {
            if (inlines[i] is Run run && run.Text == "\n")
            {
                lastNewlineIndex = i;
                break;
            }
        }

        // Remove everything after the last newline (or everything if no newline found)
        while (inlines.Count > lastNewlineIndex + 1)
        {
            inlines.RemoveAt(inlines.Count - 1);
        }
    }

    /// <summary>
    /// Trims old lines from the beginning when exceeding maxLines.
    /// </summary>
    private static void TrimOldLines(BehaviorState state, int maxLines)
    {
        var inlines = state.TextBlock.Inlines;
        if (inlines == null) return;

        while (state.LineCount > maxLines && inlines.Count > 0)
        {
            // Find the first newline and remove everything up to and including it
            int newlineIndex = -1;
            for (int i = 0; i < inlines.Count; i++)
            {
                if (inlines[i] is Run run && run.Text == "\n")
                {
                    newlineIndex = i;
                    break;
                }
            }

            if (newlineIndex < 0) break; // No more newlines

            // Remove runs up to and including the newline
            for (int i = 0; i <= newlineIndex; i++)
            {
                inlines.RemoveAt(0);
            }
            state.LineCount--;
        }
    }

    // ===================== Timestamp Handling =====================

    /// <summary>
    /// Detects and consumes a timestamp prefix of the form "[HH:mm:ss] " from the start of a text segment.
    /// </summary>
    private static bool TryConsumeTimestampPrefix(ref string text, out string timestamp)
    {
        timestamp = string.Empty;
        if (text.Length >= 11 && text[0] == '[' &&
            char.IsDigit(text[1]) && char.IsDigit(text[2]) &&
            text[3] == ':' &&
            char.IsDigit(text[4]) && char.IsDigit(text[5]) &&
            text[6] == ':' &&
            char.IsDigit(text[7]) && char.IsDigit(text[8]) &&
            text[9] == ']' &&
            text[10] == ' ')
        {
            timestamp = text.Substring(0, 11);
            text = text.Substring(11);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Creates a Run for a timestamp prefix, rendered in a dim color to distinguish from content.
    /// </summary>
    private static Run CreateTimestampRun(string content)
    {
        return new Run(content)
        {
            FontWeight = FontWeight.Normal,
            FontStyle = FontStyle.Normal,
            Foreground = new SolidColorBrush(Color.FromArgb(180, 0xCC, 0xCC, 0xCC))
        };
    }

    // ===================== ANSI Text Rendering =====================

    private static readonly Regex SgrRegex = new("\u001B\\[[0-9;]*m", RegexOptions.Compiled);

    /// <summary>
    /// Splits a text segment by SGR escape sequences and creates styled Run inlines for each part.
    /// </summary>
    private static void AppendTextWithAnsi(InlineCollection inlines, string text, AnsiStyle style)
    {
        int lastIndex = 0;
        foreach (Match m in SgrRegex.Matches(text))
        {
            if (m.Index > lastIndex)
            {
                inlines.Add(CreateRun(text.Substring(lastIndex, m.Index - lastIndex), style));
            }
            ApplySgrSequence(style, m.Value);
            lastIndex = m.Index + m.Length;
        }
        if (lastIndex < text.Length)
        {
            inlines.Add(CreateRun(text.Substring(lastIndex), style));
        }
    }

    /// <summary>
    /// Creates a Run inline with the appropriate styling (bold, italic, underline, colors, etc.).
    /// </summary>
    private static Run CreateRun(string content, AnsiStyle style)
    {
        var run = new Run(content);

        if (style.Bold) run.FontWeight = FontWeight.Bold;
        if (style.Italic) run.FontStyle = FontStyle.Italic;

        // Text decorations: underline and strikethrough
        var decos = new TextDecorationCollection();
        if (style.Underline)
        {
            decos.Add(new TextDecoration { Location = TextDecorationLocation.Underline });
        }
        if (style.Strike)
        {
            decos.Add(new TextDecoration { Location = TextDecorationLocation.Strikethrough });
        }
        if (decos.Count > 0) run.TextDecorations = decos;

        // Resolve effective colors (handle inverse video)
        var (fg, bg) = style.GetEffectiveColors();
        var fgBrush = new SolidColorBrush(fg);
        if (style.Dim) fgBrush.Opacity = 0.7;
        run.Foreground = fgBrush;

        if (bg.HasValue)
        {
            run.Background = new SolidColorBrush(bg.Value);
        }

        // Hidden: draw foreground as background color (approximate)
        if (style.Hidden)
        {
            run.Foreground = bg.HasValue
                ? new SolidColorBrush(bg.Value)
                : new SolidColorBrush(Colors.Transparent);
        }

        return run;
    }

    // ===================== SGR Parsing =====================

    /// <summary>
    /// Parses and applies an SGR (Select Graphic Rendition) escape sequence to the current style.
    /// Supports: reset, bold, dim, italic, underline, blink, inverse, hidden, strikethrough,
    /// 16 basic colors (fg/bg), 256-color, and true-color (RGB).
    /// </summary>
    private static void ApplySgrSequence(AnsiStyle style, string esc)
    {
        // esc format: "\u001B[...m"
        var codesStr = esc.Substring(2, esc.Length - 3); // strip \u001B[ and m
        if (string.IsNullOrEmpty(codesStr)) { style.Reset(); return; }
        var parts = codesStr.Split(';');
        if (parts.Length == 0) { style.Reset(); return; }

        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var code)) continue;
            switch (code)
            {
                case 0: style.Reset(); break;
                case 1: style.Bold = true; style.Dim = false; break;
                case 2: style.Dim = true; break;
                case 3: style.Italic = true; break;
                case 4: style.Underline = true; break;
                case 5: style.Blink = true; break; // no visual effect, tracked
                case 7: style.Inverse = true; break;
                case 8: style.Hidden = true; break;
                case 9: style.Strike = true; break;
                case 21: style.Bold = false; break;
                case 22: style.Bold = false; style.Dim = false; break;
                case 23: style.Italic = false; break;
                case 24: style.Underline = false; break;
                case 25: style.Blink = false; break;
                case 27: style.Inverse = false; break;
                case 28: style.Hidden = false; break;
                case 29: style.Strike = false; break;

                // Foreground: basic colors 30-37, bright 90-97
                case >= 30 and <= 37:
                    style.Foreground = MapBasicColor(code - 30, bright: false);
                    break;
                case >= 90 and <= 97:
                    style.Foreground = MapBasicColor(code - 90, bright: true);
                    break;
                case 39:
                    style.Foreground = AnsiStyle.DefaultForeground;
                    break;

                // Background: basic colors 40-47, bright 100-107
                case >= 40 and <= 47:
                    style.Background = MapBasicColor(code - 40, bright: false);
                    break;
                case >= 100 and <= 107:
                    style.Background = MapBasicColor(code - 100, bright: true);
                    break;
                case 49:
                    style.Background = null;
                    break;

                // Extended color: 256-color (mode 5) and true-color RGB (mode 2)
                case 38:
                case 48:
                    bool isFg = code == 38;
                    if (i + 1 < parts.Length && int.TryParse(parts[i + 1], out var mode))
                    {
                        if (mode == 5 && i + 2 < parts.Length && int.TryParse(parts[i + 2], out var idx))
                        {
                            // 256-color: ESC[38;5;{idx}m or ESC[48;5;{idx}m
                            var c = Map256Color(idx);
                            if (isFg) style.Foreground = c; else style.Background = c;
                            i += 2;
                        }
                        else if (mode == 2 && i + 4 < parts.Length &&
                                 int.TryParse(parts[i + 2], out var r) &&
                                 int.TryParse(parts[i + 3], out var g) &&
                                 int.TryParse(parts[i + 4], out var b))
                        {
                            // True-color RGB: ESC[38;2;{r};{g};{b}m or ESC[48;2;{r};{g};{b}m
                            var c = Color.FromArgb(255, (byte)Clamp(r), (byte)Clamp(g), (byte)Clamp(b));
                            if (isFg) style.Foreground = c; else style.Background = c;
                            i += 4;
                        }
                    }
                    break;

                default:
                    // Unrecognized code, silently ignore
                    break;
            }
        }
    }

    // ===================== CSI Escape Handling =====================

    /// <summary>
    /// Attempts to consume a CSI escape sequence starting at the given index.
    /// Returns the number of characters consumed, or 0 if no valid sequence was found.
    /// Handles: m (SGR), K (clear line), J (clear screen), H/f (cursor position).
    /// </summary>
    private static int TryConsumeEscape(AnsiStyle style, InlineCollection inlines, string s, int index)
    {
        if (index + 1 >= s.Length) return 0;
        if (s[index] != '\u001B' || s[index + 1] != '[') return 0;

        int j = index + 2;
        // Collect parameter bytes until we hit a final byte (letter)
        while (j < s.Length && !char.IsLetter(s[j])) j++;
        if (j >= s.Length) return 0;

        char final = s[j];
        int length = j - index + 1;
        var payload = s.Substring(index, length);

        switch (final)
        {
            case 'm':
                // SGR: Select Graphic Rendition
                ApplySgrSequence(style, payload);
                return length;

            case 'K':
                // EL: Erase in Line - clear current line content
                ClearCurrentLine(inlines);
                return length;

            case 'J':
                // ED: Erase in Display - clear current line
                ClearCurrentLine(inlines);
                return length;

            case 'H':
            case 'f':
                // CUP: Cursor Position - approximate as carriage return (clear line)
                ClearCurrentLine(inlines);
                return length;

            default:
                // Unrecognized CSI sequence - consume and ignore
                return length;
        }
    }

    // ===================== Campbell Color Scheme =====================

    // Windows Terminal Campbell color scheme - 16 basic terminal colors
    // Standard colors (indices 0-7): Black, Red, Green, Yellow, Blue, Magenta, Cyan, White
    private static readonly Color[] CampbellNormal =
    {
        FromHex("#0C0C0C"), // Black
        FromHex("#C50F1F"), // Red
        FromHex("#13A10E"), // Green
        FromHex("#C19C00"), // Yellow
        FromHex("#0037DA"), // Blue
        FromHex("#881798"), // Magenta
        FromHex("#3A96DD"), // Cyan
        FromHex("#CCCCCC"), // White
    };

    // Bright colors (indices 0-7): Bright Black (Gray), Bright Red, Bright Green, etc.
    private static readonly Color[] CampbellBright =
    {
        FromHex("#767676"), // Bright Black (Gray)
        FromHex("#E74856"), // Bright Red
        FromHex("#16C60C"), // Bright Green
        FromHex("#F9F1A5"), // Bright Yellow
        FromHex("#3B78FF"), // Bright Blue
        FromHex("#B4009E"), // Bright Magenta
        FromHex("#61D6D6"), // Bright Cyan
        FromHex("#F2F2F2"), // Bright White
    };

    private static Color MapBasicColor(int idx, bool bright)
    {
        if (idx < 0 || idx > 7) return FromHex("#CCCCCC");
        return bright ? CampbellBright[idx] : CampbellNormal[idx];
    }

    // ===================== 256-Color and True-Color Mapping =====================

    /// <summary>
    /// Maps a 256-color index (0-255) to an RGB color.
    /// 0-15: standard/bright colors (Campbell), 16-231: 6x6x6 color cube, 232-255: grayscale ramp.
    /// </summary>
    private static Color Map256Color(int idx)
    {
        idx = Math.Clamp(idx, 0, 255);
        if (idx < 16)
        {
            // 0-15: standard and bright colors
            return MapBasicColor(idx % 8, idx >= 8);
        }
        if (idx < 232)
        {
            // 16-231: 6x6x6 color cube
            int n = idx - 16;
            int r = n / 36;
            int g = (n % 36) / 6;
            int b = n % 6;
            byte Cv(int v) => (byte)(v == 0 ? 0 : 55 + 40 * v);
            return Color.FromArgb(255, Cv(r), Cv(g), Cv(b));
        }
        // 232-255: grayscale ramp
        int gray = 8 + 10 * (idx - 232);
        return Color.FromArgb(255, (byte)gray, (byte)gray, (byte)gray);
    }

    // ===================== Utility =====================

    private static int Clamp(int v) => v < 0 ? 0 : (v > 255 ? 255 : v);

    private static Color FromHex(string hex)
    {
        if (hex.StartsWith('#')) hex = hex[1..];
        byte a = 255, r = 0, g = 0, b = 0;
        if (hex.Length == 6)
        {
            r = Convert.ToByte(hex[..2], 16);
            g = Convert.ToByte(hex[2..4], 16);
            b = Convert.ToByte(hex[4..6], 16);
        }
        else if (hex.Length == 8)
        {
            a = Convert.ToByte(hex[..2], 16);
            r = Convert.ToByte(hex[2..4], 16);
            g = Convert.ToByte(hex[4..6], 16);
            b = Convert.ToByte(hex[6..8], 16);
        }
        return Color.FromArgb(a, r, g, b);
    }

    /// <summary>
    /// Removes the last character from the last Run inline in the collection (backspace handling).
    /// </summary>
    private static void RemoveLastRunChar(InlineCollection inlines)
    {
        if (inlines.Count == 0) return;

        var lastInline = inlines.LastOrDefault();
        if (lastInline is Run run && !string.IsNullOrEmpty(run.Text) && run.Text != "\n")
        {
            run.Text = run.Text[..^1];
            if (run.Text.Length == 0)
            {
                inlines.Remove(run);
            }
        }
        else if (lastInline != null && lastInline is Run lastRun && lastRun.Text == "\n")
        {
            // Don't remove newline characters
        }
        else if (lastInline != null)
        {
            inlines.Remove(lastInline);
        }
    }

    // ===================== Auto-Scroll =====================

    /// <summary>
    /// Scrolls the parent ScrollViewer to the bottom.
    /// </summary>
    private static void ScrollToEnd(SelectableTextBlock textBlock)
    {
        // Defer to allow layout to update after inlines are added
        Dispatcher.UIThread.Post(() =>
        {
            var sv = FindParentScrollViewer(textBlock);
            if (sv == null) return;

            var maxOffset = Math.Max(0, sv.Extent.Height - sv.Viewport.Height);
            sv.Offset = new Vector(sv.Offset.X, maxOffset);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Walks up the visual tree to find an ancestor ScrollViewer.
    /// </summary>
    private static ScrollViewer? FindParentScrollViewer(Visual? visual)
    {
        var current = visual?.GetVisualParent();
        while (current != null)
        {
            if (current is ScrollViewer sv) return sv;
            current = current.GetVisualParent();
        }
        return null;
    }

    /// <summary>
    /// Hooks into the ScrollViewer's scroll offset changes to detect user scrolling.
    /// When user scrolls away from the bottom, auto-scroll is disabled.
    /// When user scrolls back to the bottom, auto-scroll is re-enabled.
    /// </summary>
    private static void HookScrollViewerForUserScroll(SelectableTextBlock textBlock)
    {
        var sv = FindParentScrollViewer(textBlock);
        if (sv == null) return;

        // Track whether we're programmatically scrolling to avoid feedback loops
        bool programmaticScroll = false;

        sv.GetObservable(ScrollViewer.OffsetProperty).Subscribe(new AnonymousObserver<Vector>(offset =>
        {
            if (programmaticScroll) return;

            var maxOffset = Math.Max(0, sv.Extent.Height - sv.Viewport.Height);
            bool isAtBottom = offset.Y >= maxOffset - 2.0;

            if (!isAtBottom && GetAutoScroll(textBlock))
            {
                // User scrolled away from bottom - disable auto-scroll
                SetAutoScroll(textBlock, false);
            }
            else if (isAtBottom && !GetAutoScroll(textBlock))
            {
                // User scrolled back to bottom - re-enable auto-scroll
                SetAutoScroll(textBlock, true);
            }
        }));
    }

    // ===================== Inner Classes =====================

    /// <summary>
    /// Per-control state tracking for the behavior.
    /// </summary>
    private sealed class BehaviorState
    {
        public INotifyCollectionChanged? SourceCollection { get; set; }
        public SelectableTextBlock TextBlock { get; set; } = null!;
        public NotifyCollectionChangedEventHandler? CollectionChangedHandler { get; set; }
        public AnsiStyle CurrentStyle { get; set; } = new();
        public int LineCount { get; set; }
    }

    /// <summary>
    /// Tracks the current ANSI styling state during text parsing.
    /// Supports bold, dim, italic, underline, blink, inverse, hidden, strikethrough,
    /// and foreground/background colors.
    /// </summary>
    private sealed class AnsiStyle
    {
        public bool Bold { get; set; }
        public bool Dim { get; set; }
        public bool Italic { get; set; }
        public bool Underline { get; set; }
        public bool Blink { get; set; }
        public bool Inverse { get; set; }
        public bool Hidden { get; set; }
        public bool Strike { get; set; }

        public static readonly Color DefaultForeground = FromHex("#CCCCCC");
        public static readonly Color DefaultBackground = FromHex("#0C0C0C");

        public Color Foreground { get; set; } = DefaultForeground;
        public Color? Background { get; set; }

        public void Reset()
        {
            Bold = false;
            Dim = false;
            Italic = false;
            Underline = false;
            Blink = false;
            Inverse = false;
            Hidden = false;
            Strike = false;
            Foreground = DefaultForeground;
            Background = null;
        }

        /// <summary>
        /// Returns effective foreground and background colors, accounting for inverse video mode.
        /// </summary>
        public (Color fg, Color? bg) GetEffectiveColors()
        {
            if (!Inverse) return (Foreground, Background);
            // Inverse: swap foreground and background
            var fg = Background ?? DefaultBackground;
            var bg = (Color?)Foreground;
            return (fg, bg);
        }
    }

    /// <summary>
    /// Simple IObserver adapter for Avalonia property change observables.
    /// </summary>
    private sealed class PropertyObserver : IObserver<AvaloniaPropertyChangedEventArgs>
    {
        private readonly Action<AvaloniaPropertyChangedEventArgs> _onNext;
        public PropertyObserver(Action<AvaloniaPropertyChangedEventArgs> onNext) => _onNext = onNext;
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(AvaloniaPropertyChangedEventArgs value) => _onNext(value);
    }

    /// <summary>
    /// Simple IObserver adapter for typed observables (used for ScrollViewer offset tracking).
    /// </summary>
    private sealed class AnonymousObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;
        public AnonymousObserver(Action<T> onNext) => _onNext = onNext;
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(T value) => _onNext(value);
    }
}

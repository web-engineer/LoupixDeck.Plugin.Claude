using System.Text.RegularExpressions;
using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.Claude.Rendering;

/// <summary>
/// Text layout helpers shared by the strip-segment and touch-key renderers: numeric detection,
/// single-line shrink-to-fit for numbers, and word wrapping (on spaces and hyphens) for text that
/// only shrinks when a single word is wider than the box.
/// </summary>
internal static partial class TextFit
{
    /// <summary>
    /// A value drawn in the large bold numeric style: "-47.0 dB", "+1.5 dB", "14 %", "0.0", and the
    /// balance amounts "L 2.0" / "R 1.5". Everything else ("C", "Muted", "Optical", "…") is text.
    /// </summary>
    public static bool IsNumeric(string? text) => text is not null && NumericRegex().IsMatch(text.Trim());

    /// <summary>Line height for a font size (the host font's ascent + descent plus 1 px): 11 px → 12 px, 16 px bold → 17 px.</summary>
    public static int LineHeight(float size) => (int)Math.Ceiling(size) + 1;

    /// <summary>Largest size from <paramref name="startSize"/> down to <paramref name="minSize"/> at which the single line fits; the minimum when none does.</summary>
    public static float FitSingleLine(IRenderCanvas canvas, string text, int maxWidth, float startSize, float minSize, bool bold)
    {
        for (var size = startSize; size > minSize; size -= 1f)
        {
            if (canvas.MeasureText(text, size, bold) <= maxWidth) return size;
        }

        return minSize;
    }

    /// <summary>
    /// Wraps <paramref name="text"/> on spaces and hyphens into at most <paramref name="maxLines"/>
    /// lines at <paramref name="size"/>. The size is reduced (never below <paramref name="minSize"/>)
    /// only when a single piece is wider than the box — multi-word text wraps instead of shrinking.
    /// The last allowed line takes whatever is left.
    /// </summary>
    public static (float Size, IReadOnlyList<string> Lines) Wrap(IRenderCanvas canvas, string text, int maxWidth, float size, float minSize, int maxLines, bool bold)
    {
        var pieces = Split(text.Trim());
        if (pieces.Count == 0) return (size, []);

        while (size > minSize && pieces.Max(p => canvas.MeasureText(p, size, bold)) > maxWidth)
        {
            size -= 1f;
        }

        var lines = new List<string>();
        var current = pieces[0];
        for (var i = 1; i < pieces.Count; i++)
        {
            var candidate = Join(current, pieces[i]);
            if (lines.Count < maxLines - 1 && canvas.MeasureText(candidate, size, bold) > maxWidth)
            {
                lines.Add(current);
                current = pieces[i];
            }
            else
            {
                current = candidate;
            }
        }

        lines.Add(current);
        return (size, lines);
    }

    /// <summary>Draws the lines consecutively from <paramref name="top"/>, each horizontally centred; returns the y below the last line.</summary>
    public static int DrawLines(IRenderCanvas canvas, IReadOnlyList<string> lines, float size, bool bold, int x, int top, int width, PluginColor color)
    {
        var lineHeight = LineHeight(size);
        foreach (var line in lines)
        {
            canvas.DrawText(line, x, top, width, lineHeight, color, size, TextHAlign.Center, TextVAlign.Middle, bold);
            top += lineHeight;
        }

        return top;
    }

    /// <summary>Splits on spaces (dropped) and hyphens (kept at the end of the preceding piece: "AnthemLogic-" / "Cinema").</summary>
    private static List<string> Split(string text)
    {
        var pieces = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == ' ')
            {
                if (i > start) pieces.Add(text[start..i]);
                start = i + 1;
            }
            else if (text[i] == '-' && i > start && i + 1 < text.Length)
            {
                pieces.Add(text[start..(i + 1)]);
                start = i + 1;
            }
        }

        if (start < text.Length) pieces.Add(text[start..]);
        return pieces;
    }

    private static string Join(string left, string right) =>
        left.EndsWith('-') ? left + right : left + " " + right;

    [GeneratedRegex(@"^(?:[+-]?\d+(?:\.\d+)?(?: dB| %)?|[LR] \d+(?:\.\d+)?)$")]
    private static partial Regex NumericRegex();
}

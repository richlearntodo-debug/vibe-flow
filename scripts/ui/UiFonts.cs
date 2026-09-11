using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;

// Which font families the interface actually renders with.
//
// Requesting a family that is not installed does not fail: GDI+ silently substitutes its default
// (measured on this machine: "Microsoft Sans Serif" for any unknown name). Two different outcomes
// follow, and they are not equally bad:
//
// - Chinese text still renders, because Windows links the substitute to an installed CJK font.
//   Measured by drawing two different characters and confirming the results differ, which rules out
//   the "two identical replacement boxes" shape.
// - The private-use icon glyphs have no such mapping. Measured by drawing one through the substitute:
//   it comes out as a hollow rectangle, i.e. a page of boxes where the section icons should be. That
//   is the shape a user describes as a garbled interface.
//
// So the families are resolved against what this machine has, and the result is reported in the log
// and in the exported diagnostics. A garbled-interface report can then be answered from the report
// instead of being guessed at.
internal static class UiFonts
{
    // Ordered by how well they cover Simplified Chinese; the last one ships with every Windows.
    private static readonly string[] TextCandidates =
    {
        "Microsoft YaHei UI", "Microsoft YaHei", "SimSun", "Microsoft JhengHei", "Segoe UI"
    };

    // Segoe MDL2 Assets ships with Windows 10/11 and is where the interface's glyphs were chosen
    // from; Segoe Fluent Icons is its Windows 11 successor over the same private-use block;
    // Segoe UI Symbol is the last resort and does not carry every glyph the interface uses.
    private static readonly string[] IconCandidates =
    {
        "Segoe MDL2 Assets", "Segoe Fluent Icons", "Segoe UI Symbol"
    };

    private static readonly List<string> installed = ReadInstalledFamilies();
    private static readonly string textFamily = Resolve(TextCandidates);
    private static readonly string iconFamily = Resolve(IconCandidates);

    internal static string TextFamily { get { return textFamily; } }
    internal static string IconFamily { get { return iconFamily; } }

    // True when the preferred family is the one in use. False means the interface is running on a
    // substitute, which is worth reporting because the icon glyphs are the part that degrades.
    internal static bool TextPreferred { get { return Matches(TextCandidates[0], textFamily); } }
    internal static bool IconPreferred { get { return Matches(IconCandidates[0], iconFamily); } }

    internal static bool TextInstalled { get { return IsInstalled(textFamily); } }
    internal static bool IconInstalled { get { return IsInstalled(iconFamily); } }

    internal static Font Text(float size)
    {
        return new Font(textFamily, size, FontStyle.Regular);
    }

    internal static Font Text(float size, FontStyle style)
    {
        return new Font(textFamily, size, style);
    }

    internal static Font Icon(float size)
    {
        return new Font(iconFamily, size, FontStyle.Regular);
    }

    internal static Font Icon(float size, FontStyle style)
    {
        return new Font(iconFamily, size, style);
    }

    // One line for the log and the exported diagnostics: what the interface is really using, and
    // whether that is the preferred family or a substitute.
    internal static string Describe()
    {
        return "text_font=" + textFamily + " text_installed=" + (TextInstalled ? "yes" : "no") +
            " text_substitute=" + (TextPreferred ? "no" : "yes") +
            " icon_font=" + iconFamily + " icon_installed=" + (IconInstalled ? "yes" : "no") +
            " icon_substitute=" + (IconPreferred ? "no" : "yes");
    }

    private static bool Matches(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsInstalled(string family)
    {
        if (string.IsNullOrWhiteSpace(family)) return false;
        foreach (string name in installed)
        {
            if (Matches(name, family)) return true;
        }
        return false;
    }

    // The first candidate this machine actually has. When none matches, the preferred name is kept so
    // the diagnostic line shows what was wanted; the caller must still receive a non-empty name,
    // because an empty family name throws in the Font constructor.
    private static string Resolve(string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            if (IsInstalled(candidate)) return candidate;
        }
        return candidates.Length > 0 ? candidates[0] : "Segoe UI";
    }

    private static List<string> ReadInstalledFamilies()
    {
        var families = new List<string>();
        try
        {
            using (var collection = new InstalledFontCollection())
            {
                foreach (FontFamily family in collection.Families)
                {
                    try { families.Add(family.Name); }
                    catch { }
                }
            }
        }
        catch { }
        return families;
    }
}

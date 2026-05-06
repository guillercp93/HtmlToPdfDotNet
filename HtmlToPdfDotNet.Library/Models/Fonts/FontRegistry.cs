namespace HtmlToPdfDotNet.Library.Models.Fonts;

/// <summary>
/// Resolves and caches <see cref="EmbeddedFontInfo"/> instances for font files
/// registered by the caller.
///
/// Registration model:
///   The caller adds font files with <see cref="RegisterFont"/>, optionally specifying
///   the CSS family name, bold/italic flags. If no family name is given, it is derived
///   from the file name.
///
/// Resolution model:
///   <see cref="TryResolve"/> attempts to find the best matching face for a given
///   (family, bold, italic) triple. Falls back to regular-weight / non-italic faces when
///   an exact style match is unavailable.
///
/// Parsing is done once per file; subsequent lookups are served from the in-memory cache.
/// </summary>
public sealed class FontRegistry
{
    #region Internal records
    private record FaceKey(string Family, bool Bold, bool Italic);

    private readonly Dictionary<FaceKey, EmbeddedFontInfo> _cache = new();
    #endregion

    #region Registration
    /// <summary>
    /// Registers a font file for embedding.
    /// </summary>
    /// <param name="filePath">Absolute path to the .ttf or .otf file.</param>
    /// <param name="familyName">
    ///   CSS font-family name (e.g. "Roboto").
    ///   When <c>null</c>, the name is derived from the file name without extension.
    /// </param>
    /// <param name="bold">True if this file represents the bold face.</param>
    /// <param name="italic">True if this file represents the italic/oblique face.</param>
    public void RegisterFont(string filePath,
                             string? familyName = null,
                             bool bold = false,
                             bool italic = false)
    {
        if (!File.Exists(filePath)) return;

        string resolvedFamily = (familyName ?? Path.GetFileNameWithoutExtension(filePath)).ToLowerInvariant().Trim();
        FaceKey key = new(resolvedFamily, bold, italic);

        if (_cache.ContainsKey(key)) return; // already registered

        EmbeddedFontInfo info = TrueTypeFontParser.Parse(filePath, resolvedFamily, bold, italic);
        _cache[key] = info;
    }

    /// <summary>
    /// Registers all .ttf and .otf files found in <paramref name="directory"/>.
    ///
    /// File-name conventions for auto-detection of bold/italic:
    ///   • "Roboto-Bold.ttf"          → bold=true
    ///   • "Roboto-Italic.ttf"        → italic=true
    ///   • "Roboto-BoldItalic.ttf"    → bold=true, italic=true
    ///   • "Roboto-Regular.ttf"       → bold=false, italic=false
    ///   • "Roboto.ttf"               → regular
    /// </summary>
    /// <param name="directory">Directory to scan.</param>
    /// <param name="searchPattern">Glob pattern (default: all TTF/OTF).</param>
    public void RegisterDirectory(string directory, string searchPattern = "*.ttf;*.otf")
    {
        if (!Directory.Exists(directory)) return;

        string[] patterns = searchPattern.Split(';');
        foreach (string pattern in patterns)
        {
            foreach (string file in Directory.EnumerateFiles(directory, pattern.Trim(), SearchOption.AllDirectories))
            {
                try
                {
                    DetectStyleFromFileName(file, out string? family, out bool bold, out bool italic);
                    FaceKey key = new(family, bold, italic);
                    if (_cache.ContainsKey(key)) continue;

                    EmbeddedFontInfo info = TrueTypeFontParser.Parse(file, family, bold, italic);
                    _cache[key] = info;
                }
                catch
                {
                    // Skip unparseable files silently
                }
            }
        }
    }
    #endregion

    #region Resolution

    /// <summary>
    /// Tries to find an <see cref="EmbeddedFontInfo"/> for the requested face.
    ///
    /// Fallback order (when exact match not found):
    ///   1. Exact (family, bold, italic)
    ///   2. (family, bold=false, italic)  – drop bold
    ///   3. (family, bold, italic=false)  – drop italic
    ///   4. (family, false, false)        – regular face
    /// </summary>
    /// <param name="familyName">The font family name to resolve.</param>
    /// <param name="bold">Whether the font is bold.</param>
    /// <param name="italic">Whether the font is italic.</param>
    /// <param name="info">The resolved <see cref="EmbeddedFontInfo"/>.</param>
    /// <returns>True if a font was resolved, false otherwise.</returns>
    public bool TryResolve(string familyName,
                           bool bold,
                           bool italic,
                           out EmbeddedFontInfo? info)
    {
        string family = familyName.ToLowerInvariant().Trim();

        // Try aliases: "sans-serif" → "helvetica", "serif" → "times", "monospace" → "courier"
        IEnumerable<string> lookups = FamilyAliases(family);

        foreach (string fam in lookups)
        {
            if (_cache.TryGetValue(new FaceKey(fam, bold, italic), out info)) return true;
            if (bold && _cache.TryGetValue(new FaceKey(fam, false, italic), out info)) return true;
            if (italic && _cache.TryGetValue(new FaceKey(fam, bold, false), out info)) return true;
            if (_cache.TryGetValue(new FaceKey(fam, false, false), out info)) return true;
        }

        info = null;
        return false;
    }

    /// <summary>
    /// Returns true when any font is registered for the given family (any style).
    /// </summary>
    /// <param name="familyName">The font family name to check.</param>
    /// <returns>True if a font was registered for the given family, false otherwise.</returns>
    public bool HasFamily(string familyName)
    {
        string family = familyName.ToLowerInvariant().Trim();
        foreach (string alias in FamilyAliases(family))
        {
            if (_cache.Keys.Any(k => k.Family == alias))
                return true;
        }
        return false;
    }

    /// <summary>Returns all registered fonts (for debugging / listing).</summary>
    /// <returns>An enumerable of all registered fonts.</returns>
    public IEnumerable<EmbeddedFontInfo> AllFonts => _cache.Values;
    #endregion

    #region Helpers
    /// <summary>
    /// Returns a sequence of font family names to try for the given family.
    /// Includes the family itself and any configured aliases.
    /// </summary>
    /// <param name="family">The font family name to resolve.</param>
    /// <returns>An enumerable of font family names to try.</returns>
    private static IEnumerable<string> FamilyAliases(string family)
    {
        yield return family;

        // Generic CSS families → concrete names
        if (family is "sans-serif") { yield return "helvetica"; yield return "arial"; }
        else if (family is "serif") { yield return "times"; yield return "times new roman"; }
        else if (family is "monospace") { yield return "courier"; yield return "courier new"; }
        else if (family is "arial") yield return "helvetica";
        else if (family is "helvetica") yield return "arial";
        else if (family is "times new roman") yield return "times";
        else if (family is "times") yield return "times new roman";
        else if (family is "courier new") yield return "courier";
        else if (family is "courier") yield return "courier new";
    }


    /// <summary>
    /// Detects the style (bold/italic) from the file name.
    /// </summary>
    /// <param name="filePath">The path to the font file.</param>
    /// <param name="family">The detected font family name.</param>
    /// <param name="bold">Whether the font is bold.</param>
    /// <param name="italic">Whether the font is italic.</param>
    private static void DetectStyleFromFileName(string filePath,
                                                out string family,
                                                out bool bold,
                                                out bool italic)
    {
        string name = Path.GetFileNameWithoutExtension(filePath);

        bold = false;
        italic = false;

        // Try to split on '-' or '_' or common separators
        string lower = name.ToLowerInvariant().Replace('-', ' ').Replace('_', ' ');
        string[] parts = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        List<string> familyParts = new List<string>();

        foreach (var part in parts)
        {
            if (part == "bold" || part == "semibold") bold = true;
            if (part == "italic" || part == "oblique") italic = true;
            if (part is "bolditalic" or "boldoblique") { bold = true; italic = true; }
            if (part is "regular" or "light" or "thin"
                     or "medium" or "black" or "extrabold"
                     or "heavy") { /* style token, skip */ }
            else if (part is not ("bold" or "semibold"
                              or "italic" or "oblique"
                              or "bolditalic" or "boldoblique"))
            {
                familyParts.Add(part);
            }
        }

        family = (familyParts.Count > 0
            ? string.Join(" ", familyParts)
            : name).ToLowerInvariant().Trim();
    }
    #endregion
}

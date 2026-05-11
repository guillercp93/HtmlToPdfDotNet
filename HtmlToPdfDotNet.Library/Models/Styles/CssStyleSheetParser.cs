using System.Text;
using System.Text.RegularExpressions;

namespace HtmlToPdfDotNet.Library.Models.Styles;

/// <summary>
/// Parses CSS style sheets (blocks <style> or external .css files)
/// and produces an ordered list of <see cref="CssRule"/>.
///
/// Supports:
///   - Type rules: p { } h1, h2 { }
///   - Compound selectors: div.container, #main, p.intro
///   - Descendant combinator: div p, .container h1
///   - @media (ignored — the entire block is skipped)
///   - CSS comments: /* ... */
/// </summary>
public static class CssStyleSheetParser
{
    /// <summary>
    /// Parses a CSS string and returns the rules.
    /// </summary>
    /// <param name="css">The CSS string to parse.</param>
    /// <returns>An ordered list of <see cref="CssRule"/>.</returns>
    public static IEnumerable<CssRule> Parse(string css)
    {
        if (string.IsNullOrWhiteSpace(css)) return [];

        css = RemoveComments(css);
        css = RemoveAtRules(css);

        return ParseRules(css);
    }

    /// <summary>
    /// Extracts all <style> blocks from an HTML document and parses them.
    /// </summary>
    /// <param name="html">The HTML document to parse.</param>
    /// <returns>An ordered list of <see cref="CssRule"/>.</returns>
    public static IEnumerable<CssRule> ParseFromHtml(string html)
    {
        List<CssRule> rules = new();
        IEnumerable<string> blocks = ExtractStyleBlocks(html);
        foreach (string block in blocks)
            rules.AddRange(Parse(block));
        return rules;
    }

    /// <summary>
    /// Extracts all <style> blocks from an HTML document.
    /// </summary>
    /// <param name="html">The HTML document to extract style blocks from.</param>
    /// <returns>An enumerable collection of style block contents.</returns>
    private static IEnumerable<string> ExtractStyleBlocks(string html)
    {
        MatchCollection matches = Regex.Matches(html,
                                                @"<style[^>]*>(.*?)</style>",
                                                RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match m in matches)
            yield return m.Groups[1].Value;
    }

    /// <summary>
    /// Parses the CSS rules from the given CSS string.
    /// </summary>
    /// <param name="css">The CSS string to parse.</param>
    /// <returns>An ordered list of <see cref="CssRule"/>.</returns>
    private static IEnumerable<CssRule> ParseRules(string css)
    {
        List<CssRule> rules = new();

        // Iterate over blocks: "selector { declarations }"
        int i = 0;
        while (i < css.Length)
        {
            // Find the start of the block
            int braceOpen = css.IndexOf('{', i);
            if (braceOpen < 0) break;

            int braceClose = css.IndexOf('}', braceOpen + 1);
            if (braceClose < 0) break;

            string selectorText = css[i..braceOpen].Trim();
            string declarationText = css[(braceOpen + 1)..braceClose].Trim();

            i = braceClose + 1;

            if (string.IsNullOrWhiteSpace(selectorText)) continue;

            IEnumerable<CssSelector> selectors = ParseSelectors(selectorText);
            Dictionary<string, string> declarations = ParseDeclarations(declarationText);

            if (selectors.Any() && declarations.Any())
                rules.Add(new CssRule
                {
                    Selectors = selectors.ToList(),
                    Declarations = declarations
                });
        }

        return rules;
    }

    /// <summary>
    /// Parses selectors from the given selector text.
    /// </summary>
    /// <param name="selectorText">The selector text to parse.</param>
    /// <returns>An ordered list of <see cref="CssSelector"/>.</returns>
    private static IEnumerable<CssSelector> ParseSelectors(string selectorText)
    {
        List<CssSelector> result = new();

        // Split multiple selectors by comma
        foreach (string raw in selectorText.Split(','))
        {
            string trimmed = raw.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            List<SelectorPart> parts = ParseSelectorParts(trimmed).ToList();
            if (parts.Count == 0) continue;

            (int A, int B, int C) spec = CalculateSpecificity(parts);

            result.Add(new CssSelector
            {
                Raw = trimmed,
                Parts = parts,
                Specificity = spec,
            });
        }

        return result;
    }

    /// <summary>
    /// Divide a selector in parts by descendant combinator (space).
    /// "div.container p.intro" → [div.container, p.intro]
    /// </summary>
    /// <param name="selector">The selector text to parse.</param>
    /// <returns>An ordered list of <see cref="SelectorPart"/>.</returns>
    private static IEnumerable<SelectorPart> ParseSelectorParts(string selector)
    {
        List<SelectorPart> parts = new();
        string[] tokens = selector.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (string token in tokens)
        {
            SelectorPart? part = ParseSinglePart(token);
            if (part != null) parts.Add(part);
        }

        return parts;
    }

    /// <summary>
    /// Parse a single selector part (token): "div", ".class", "#id", "p.intro#main".
    /// </summary>
    /// <param name="token">The token to parse.</param>
    /// <returns>A <see cref="SelectorPart"/>.</returns>
    private static SelectorPart? ParseSinglePart(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        string? tag = null;
        string? id = null;
        List<string> classes = new();

        // Separate the token into components: tag, .class, #id
        // We use regex to find each component
        // Format: [tag]?(.[class])*(#id)?([class])*
        string remaining = token;

        // Extract id
        Match idMatch = Regex.Match(remaining, @"#([a-zA-Z0-9_-]+)");
        if (idMatch.Success)
        {
            id = idMatch.Groups[1].Value;
            remaining = remaining.Remove(idMatch.Index, idMatch.Length);
        }

        // Extract classes
        MatchCollection classMatches = Regex.Matches(remaining, @"\.([a-zA-Z0-9_-]+)");
        foreach (Match m in classMatches)
            classes.Add(m.Groups[1].Value);
        remaining = Regex.Replace(remaining, @"\.([a-zA-Z0-9_-]+)", "");

        // What remains is the tag (if any)
        remaining = remaining.Trim();
        if (!string.IsNullOrEmpty(remaining) && remaining != "*")
            tag = remaining.ToLowerInvariant();

        // Universal selector (*) → tag = null
        if (remaining == "*") tag = null;

        return new SelectorPart { Tag = tag, Id = id, Classes = classes };
    }

    /// <summary>
    /// Calculates the specificity of a selector.
    /// </summary>
    /// <param name="parts">The parts of the selector.</param>
    /// <returns>A tuple representing the specificity (A, B, C).</returns>
    private static (int A, int B, int C) CalculateSpecificity(List<SelectorPart> parts)
    {
        int a = 0, b = 0, c = 0;
        foreach (SelectorPart part in parts)
        {
            if (part.Id != null) a++;
            b += part.Classes.Count;
            if (part.Tag != null) c++;
        }
        return (a, b, c);
    }

    /// <summary>
    /// Parses declarations from the given declaration text.
    /// </summary>
    /// <param name="block">The declaration text to parse.</param>
    /// <returns>A dictionary of declarations.</returns>
    private static Dictionary<string, string> ParseDeclarations(string block)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);

        foreach (string decl in block.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int idx = decl.IndexOf(':');
            if (idx < 0) continue;

            string prop = decl[..idx].Trim().ToLowerInvariant();
            string value = decl[(idx + 1)..].Trim();

            // Remove !important (not implemented, but ignored)
            value = value.Replace("!important", "", StringComparison.OrdinalIgnoreCase).Trim();

            if (!string.IsNullOrEmpty(prop) && !string.IsNullOrEmpty(value))
                result[prop] = value;
        }

        return result;
    }

    /// <summary>
    /// Removes comments from CSS.
    /// </summary>
    /// <param name="css">The CSS text.</param>
    /// <returns>The CSS text without comments.</returns>
    private static string RemoveComments(string css)
        => Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);

    /// <summary>
    /// Removes @rules from CSS.
    /// </summary>
    /// <param name="css">The CSS text.</param>
    /// <returns>The CSS text without @rules.</returns>
    private static string RemoveAtRules(string css)
    {
        // Remove @media, @keyframes, @font-face, @import, etc.
        // For blocks with nested braces, a balance of braces is performed
        StringBuilder sb = new(css.Length);
        int i = 0;
        int len = css.Length;

        while (i < len)
        {
            // Detect @rule
            if (css[i] == '@')
            {
                // Detect delimiter: ';' (without block) or '{...}' (with block)
                int semi = css.IndexOf(';', i);
                int brace = css.IndexOf('{', i);

                if (brace >= 0 && (semi < 0 || brace < semi))
                {
                    // Skip balanced braces block
                    int depth = 0;
                    int j = brace;
                    while (j < len)
                    {
                        if (css[j] == '{') depth++;
                        else if (css[j] == '}') { depth--; if (depth == 0) { i = j + 1; goto next; } }
                        j++;
                    }
                    i = len;
                }
                else if (semi >= 0)
                {
                    i = semi + 1; // Skip @import url(...);
                }
                else
                {
                    i = len;
                }
            }
            else
            {
                sb.Append(css[i]);
                i++;
            }
        next:;
        }

        return sb.ToString();
    }
}
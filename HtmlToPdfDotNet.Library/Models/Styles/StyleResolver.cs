using HtmlToPdfDotNet.Library.Commons;
using HtmlAgilityPack;

namespace HtmlToPdfDotNet.Library.Models.Styles;

/// <summary>
/// Resolves the styles of the entire tree and calculate each node's computed style
/// applying the following rules in order:
/// <para>1. Inheritance: start from the parent's style.</para>
/// <para>2. HTML tag defaults.</para>
/// <para>3. Inline style attribute.</para>
/// It doesn't process CSS files or &lt;style&gt; blocks.
/// </summary>
public sealed class StyleResolver
{
    #region Tag Defaults
    // Non-heritable properties that certain tags override by default.
    private static readonly Dictionary<string, Action<ComputedStyle>> _TagDefaults =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Typography
            // Typography & Display
            ["h1"] = s => { s.FontSize = 24f; s.FontWeight = FontWeight.Bold; s.Margin = Helpers.DefaultMargin(12f); },
            ["h2"] = s => { s.FontSize = 18.7f; s.FontWeight = FontWeight.Bold; s.Margin = Helpers.DefaultMargin(10f); },
            ["h3"] = s => { s.FontSize = 16f; s.FontWeight = FontWeight.Bold; s.Margin = Helpers.DefaultMargin(8f); },
            ["h4"] = s => { s.FontSize = 14f; s.FontWeight = FontWeight.Bold; s.Margin = Helpers.DefaultMargin(7f); },
            ["h5"] = s => { s.FontSize = Constants.DefaultFontSize; s.FontWeight = FontWeight.Bold; s.Margin = Helpers.DefaultMargin(6f); },
            ["h6"] = s => { s.FontSize = 10f; s.FontWeight = FontWeight.Bold; s.Margin = Helpers.DefaultMargin(5f); },
            ["b"] = s => { s.FontWeight = FontWeight.Bold; s.Display = DisplayType.Inline; },
            ["strong"] = s => { s.FontWeight = FontWeight.Bold; s.Display = DisplayType.Inline; },
            ["i"] = s => { s.FontStyle = FontStyle.Italic; s.Display = DisplayType.Inline; },
            ["em"] = s => { s.FontStyle = FontStyle.Italic; s.Display = DisplayType.Inline; },
            ["small"] = s => { s.FontSize = 9f; s.Display = DisplayType.Inline; },
            ["span"] = s => s.Display = DisplayType.Inline,
            ["a"] = s => s.Display = DisplayType.Inline,
            ["label"] = s => s.Display = DisplayType.Inline,
            ["abbr"] = s => s.Display = DisplayType.Inline,
            ["cite"] = s => s.Display = DisplayType.Inline,
            ["code"] = s => { s.Display = DisplayType.Inline; s.FontFamily = "Courier"; },
            ["kbd"] = s => { s.Display = DisplayType.Inline; s.FontFamily = "Courier"; },
            ["tt"] = s => { s.Display = DisplayType.Inline; s.FontFamily = "Courier"; },
            ["pre"] = s => s.FontFamily = "Courier",

            // Layout
            ["p"] = s => { s.Margin = Helpers.DefaultMargin(8f); },
            ["ul"] = s => { s.Margin = Helpers.DefaultMargin(8f); s.Padding = Helpers.DefaultPadding(30f); },
            ["ol"] = s => { s.Margin = Helpers.DefaultMargin(8f); s.Padding = Helpers.DefaultPadding(30f); },
            ["blockquote"] = s => { s.Margin = Helpers.DefaultMargin(8f); s.Padding = Helpers.DefaultPadding(30f); },
            ["li"] = s => s.Display = DisplayType.Block,

            // Display none
            ["head"] = s => s.Display = DisplayType.None,
            ["script"] = s => s.Display = DisplayType.None,
            ["style"] = s => s.Display = DisplayType.None,
            ["meta"] = s => s.Display = DisplayType.None,
            ["link"] = s => s.Display = DisplayType.None,
            ["title"] = s => s.Display = DisplayType.None,

            // Table
            ["table"] = s => s.Display = DisplayType.Table,
            ["tr"] = s => s.Display = DisplayType.Block,
            ["td"] = s => s.Display = DisplayType.Block,
            ["th"] = s => { s.Display = DisplayType.Block; s.FontWeight = FontWeight.Bold; },
        };

    // Properties inherited from the parent if the child does not define them.
    // Reference: https://www.w3.org/TR/CSS22/propidx.html
    private static readonly HashSet<string> _InheritedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "color", "font-family", "font-size", "font-weight", "font-style",
        "line-height", "text-align", "visibility",
    };
    #endregion

    /// <summary>
    /// Resolves the styles of the entire tree.
    /// </summary>
    /// <param name="root">Root of the DOM (usually the &lt;html&gt; node).</param>
    /// <returns>Dictionary node → computed style.</returns>
    public Dictionary<HtmlNode, ComputedStyle> Resolve(HtmlNode root)
    {
        Dictionary<HtmlNode, ComputedStyle> result = new();
        ComputedStyle initial = new(); // initial values
        ResolveNode(root, initial, result);
        return result;
    }

    #region Recursion
    /// <summary>
    /// Resolves the styles of the entire tree.
    /// </summary>
    /// <param name="node">Root of the DOM (usually the &lt;html&gt; node).</param>
    /// <param name="parentStyle">Computed style of the parent node.</param>
    /// <param name="result">Dictionary to store the computed style of each node.</param>
    private static void ResolveNode(HtmlNode node,
                                    ComputedStyle parentStyle,
                                    Dictionary<HtmlNode, ComputedStyle> result)
    {
        if (node.NodeType == HtmlNodeType.Document)
        {
            // The document node has no style of its own; we only traverse children
            foreach (HtmlNode child in node.ChildNodes)
                ResolveNode(child, parentStyle, result);
            return;
        }

        if (node.NodeType == HtmlNodeType.Text)
        {
            // Text nodes inherit the style of the parent directly
            result[node] = parentStyle;
            return;
        }

        if (node.NodeType != HtmlNodeType.Element)
            return;

        // 1. Inheritance: start from the parent's style
        ComputedStyle style = InheritFrom(parentStyle);

        // 2. HTML tag defaults
        ApplyTagDefaults(node.Name, style);

        // 3. Inline style attribute
        ApplyInlineStyle(node, style);

        result[node] = style;

        // 4. Recursion in children
        foreach (HtmlNode child in node.ChildNodes)
            ResolveNode(child, style, result);
    }
    #endregion

    #region Inheritance
    /// <summary>
    /// Creates a new <see cref="ComputedStyle"/> by inheriting from the parent's style.
    /// Only inheritable properties are copied.
    /// </summary>
    /// <param name="parent">The parent's computed style.</param>
    /// <returns>A new <see cref="ComputedStyle"/> with inherited properties.</returns>
    private static ComputedStyle InheritFrom(ComputedStyle parent)
    {
        // Start with default values and copy only inheritable properties.
        ComputedStyle s = new()
        {
            Color = parent.Color,
            FontFamily = parent.FontFamily,
            FontSize = parent.FontSize,
            FontWeight = parent.FontWeight,
            FontStyle = parent.FontStyle,
            LineHeight = parent.LineHeight,
            TextAlign = parent.TextAlign,
        };
        return s;
    }
    #endregion

    #region Tag defaults
    /// <summary>
    /// Applies the default styles for a specific HTML tag.
    /// </summary>
    /// <param name="tagName">The name of the HTML tag.</param>
    /// <param name="style">The computed style to apply defaults to.</param>
    private static void ApplyTagDefaults(string tagName, ComputedStyle style)
    {
        if (_TagDefaults.TryGetValue(tagName, out Action<ComputedStyle>? apply))
            apply(style);
    }
    #endregion

    #region Inline style
    /// <summary>
    /// Applies inline styles from an HTML node to the computed style.
    /// </summary>
    /// <param name="node">The HTML node containing the inline style attribute.</param>
    /// <param name="style">The computed style to apply the inline styles to.</param>
    private static void ApplyInlineStyle(HtmlNode node, ComputedStyle style)
    {
        string attr = node.GetAttributeValue("style", string.Empty);
        if (string.IsNullOrWhiteSpace(attr)) return;

        // Parse "prop: value; prop: value"
        string[] declarations = attr.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (string decl in declarations)
        {
            int idx = decl.IndexOf(':');
            if (idx < 0) continue;

            string prop = decl[..idx].Trim().ToLowerInvariant();
            string value = decl[(idx + 1)..].Trim();

            ApplyDeclaration(prop, value, style);
        }
    }
    #endregion

    #region Apply declaration
    /// <summary>
    /// Applies a single declaration to the computed style.
    /// </summary>
    /// <param name="prop">The property name.</param>
    /// <param name="value">The property value.</param>
    /// <param name="style">The computed style.</param>
    internal static void ApplyDeclaration(string prop, string value, ComputedStyle style)
    {
        switch (prop)
        {
            // Display
            case "display":
                style.Display = CssValueParser.ParseDisplay(value); break;

            // Dimensions
            case "width":
                style.Width = CssValueParser.ParseLength(value, style.FontSize); break;
            case "height":
                style.Height = CssValueParser.ParseLength(value, style.FontSize); break;

            // Margin shorthand and individual sides
            case "margin":
                style.Margin = CssValueParser.ParseEdges(value, style.FontSize); break;
            case "margin-top":
                style.Margin = new CssEdges(
                    CssValueParser.ParseLength(value, style.FontSize),
                    style.Margin.Right, style.Margin.Bottom, style.Margin.Left); break;
            case "margin-right":
                style.Margin = new CssEdges(
                    style.Margin.Top,
                    CssValueParser.ParseLength(value, style.FontSize),
                    style.Margin.Bottom, style.Margin.Left); break;
            case "margin-bottom":
                style.Margin = new CssEdges(
                    style.Margin.Top, style.Margin.Right,
                    CssValueParser.ParseLength(value, style.FontSize),
                    style.Margin.Left); break;
            case "margin-left":
                style.Margin = new CssEdges(
                    style.Margin.Top, style.Margin.Right, style.Margin.Bottom,
                    CssValueParser.ParseLength(value, style.FontSize)); break;

            // Padding shorthand and individual sides
            case "padding":
                style.Padding = CssValueParser.ParseEdges(value, style.FontSize); break;
            case "padding-top":
                style.Padding = new CssEdges(
                    CssValueParser.ParseLength(value, style.FontSize),
                    style.Padding.Right, style.Padding.Bottom, style.Padding.Left); break;
            case "padding-right":
                style.Padding = new CssEdges(
                    style.Padding.Top,
                    CssValueParser.ParseLength(value, style.FontSize),
                    style.Padding.Bottom, style.Padding.Left); break;
            case "padding-bottom":
                style.Padding = new CssEdges(
                    style.Padding.Top, style.Padding.Right,
                    CssValueParser.ParseLength(value, style.FontSize),
                    style.Padding.Left); break;
            case "padding-left":
                style.Padding = new CssEdges(
                    style.Padding.Top, style.Padding.Right, style.Padding.Bottom,
                    CssValueParser.ParseLength(value, style.FontSize)); break;

            // Border shorthand
            case "border":
                CssBorderSide side = CssValueParser.ParseBorderSide(value, style.FontSize);
                style.BorderTop = style.BorderRight = style.BorderBottom = style.BorderLeft = side;
                break;
            case "border-top":
                style.BorderTop = CssValueParser.ParseBorderSide(value, style.FontSize); break;
            case "border-right":
                style.BorderRight = CssValueParser.ParseBorderSide(value, style.FontSize); break;
            case "border-bottom":
                style.BorderBottom = CssValueParser.ParseBorderSide(value, style.FontSize); break;
            case "border-left":
                style.BorderLeft = CssValueParser.ParseBorderSide(value, style.FontSize); break;

            // Colors
            case "color":
                style.Color = CssValueParser.ParseColor(value); break;
            case "background-color":
            case "background":
                // For "background" only extract color if no image is declared
                if (!value.Contains("url(", StringComparison.OrdinalIgnoreCase))
                    style.BackgroundColor = CssValueParser.ParseColor(value);
                break;

            // Typography
            case "font-size":
                style.FontSize = CssValueParser.ParseFontSize(value, style.FontSize); break;
            case "font-weight":
                style.FontWeight = CssValueParser.ParseFontWeight(value); break;
            case "font-style":
                style.FontStyle = CssValueParser.ParseFontStyle(value); break;
            case "font-family":
                style.FontFamily = NormalizeFontFamily(value); break;
            case "font":
                ParseFontShorthand(value, style); break;
            case "line-height":
                style.LineHeight = ParseLineHeight(value, style.FontSize); break;

            // Text
            case "text-align":
                style.TextAlign = CssValueParser.ParseTextAlign(value); break;

            // Page breaks
            case "page-break-before":
                style.PageBreakBefore = value.Trim().ToLowerInvariant() == "always"; break;
            case "page-break-after":
                style.PageBreakAfter = value.Trim().ToLowerInvariant() == "always"; break;
        }
    }
    #endregion

    #region Font shorthand
    /// <summary>
    /// Parses the font shorthand property and applies it to the computed style.
    /// </summary>
    /// <param name="value">The font shorthand value.</param>
    /// <param name="style">The computed style to apply the font shorthand to.</param>
    private static void ParseFontShorthand(string value, ComputedStyle style)
    {
        // Simplified: look for weight, style, size and family
        // Format: [style] [weight] size[/line-height] family
        string[] parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (string part in parts)
        {
            string p = part.ToLowerInvariant().TrimEnd(',');

            if (p is "italic" or "oblique") { style.FontStyle = CssValueParser.ParseFontStyle(p); continue; }
            if (p is "bold" or "bolder") { style.FontWeight = FontWeight.Bold; continue; }
            if (p is "normal") { continue; }

            // size/line-height
            if (p.Contains('/'))
            {
                string[] sl = p.Split('/');
                style.FontSize = CssValueParser.ParseFontSize(sl[0], style.FontSize);
                style.LineHeight = ParseLineHeight(sl[1], style.FontSize);
                continue;
            }

            // Is it a size?
            CssLength len = CssValueParser.ParseLength(p, style.FontSize);
            if (!len.IsZero && !len.IsAuto) { style.FontSize = len.Points; continue; }

            float fs = CssValueParser.ParseFontSize(p, style.FontSize);
            if (Math.Abs(fs - style.FontSize) > 0.01f) { style.FontSize = fs; continue; }
        }

        // The family is the rest after size
        // To simplify: take the first part that was not recognized as a keyword
    }

    /// <summary>
    /// Parses the line-height property and applies it to the computed style.
    /// </summary>
    /// <param name="value">The line-height value.</param>
    /// <param name="fontSize">The font size.</param>
    /// <returns>The line height as a multiplier of the font size.</returns>
    private static float ParseLineHeight(string value, float fontSize)
    {
        string v = value.Trim();
        if (v == "normal") return 1.2f;

        // No unit = multiplier
        if (float.TryParse(v, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float mult))
            return mult;

        // With unit → convert to multiplier
        CssLength len = CssValueParser.ParseLength(v, fontSize);
        return fontSize > 0f ? len.Points / fontSize : 1.2f;
    }

    /// <summary>
    /// Normalizes the font-family property by removing quotes and mapping generic
    /// font families to specific PDF font names.
    /// </summary>
    /// <param name="value">The font-family value.</param>
    /// <returns>The normalized font-family value.</returns>
    private static string NormalizeFontFamily(string value)
    {
        // Take the first family from the list and remove quotes
        string[] families = value.Split(',');
        string first = families[0].Trim().Trim('"', '\'');

        return first.ToLowerInvariant() switch
        {
            "serif" => "Times-Roman",
            "sans-serif" => "Helvetica",
            "monospace" => "Courier",
            "times new roman" => "Times-Roman",
            "times" => "Times-Roman",
            "helvetica" => "Helvetica",
            "arial" => "Helvetica",
            "courier new" => "Courier",
            "courier" => "Courier",
            _ => first,
        };
    }
    #endregion
}
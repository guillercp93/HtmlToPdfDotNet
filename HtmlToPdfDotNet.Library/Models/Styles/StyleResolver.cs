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
    private readonly List<CssRule> _stylesheetRules;

    /// <summary>
    /// Initializes a new instance of the <see cref="StyleResolver"/> class.
    /// </summary>
    /// <param name="stylesheetRules">The stylesheet rules to apply.</param>
    public StyleResolver(IEnumerable<CssRule>? stylesheetRules = null)
    {
        _stylesheetRules = stylesheetRules is null ?
            [] :
            [.. stylesheetRules];
    }

    #region Tag Defaults
    // Non-heritable properties that certain tags override by default.
    private static readonly Dictionary<string, Action<ComputedStyle>> _TagDefaults =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Typography & Display
            ["h1"] = s => { s.FontSize = 24f; s.FontWeight = FontWeight.Bold; s.Margin = Helpers.DefaultMargin(12f); },
            ["h2"] = s => { s.FontSize = 18.7f; s.FontWeight = FontWeight.Bold; s.Margin = Helpers.DefaultMargin(10f); },
            ["h3"] = s => { s.FontSize = 16f; s.FontWeight = FontWeight.Bold; s.Margin = Helpers.DefaultMargin(Constants.DefaultMargin); },
            ["h4"] = s => { s.FontSize = 14f; s.FontWeight = FontWeight.Bold; s.Margin = Helpers.DefaultMargin(7f); },
            ["h5"] = s => { s.FontSize = Constants.DefaultFontSize; s.FontWeight = FontWeight.Bold; s.Margin = Helpers.DefaultMargin(6f); },
            ["h6"] = s => { s.FontSize = 10f; s.FontWeight = FontWeight.Bold; s.Margin = Helpers.DefaultMargin(5f); },
            ["b"] = s => { s.FontWeight = FontWeight.Bold; s.Display = DisplayType.Inline; },
            ["strong"] = s => { s.FontWeight = FontWeight.Bold; s.Display = DisplayType.Inline; },
            ["i"] = s => { s.FontStyle = FontStyle.Italic; s.Display = DisplayType.Inline; },
            ["em"] = s => { s.FontStyle = FontStyle.Italic; s.Display = DisplayType.Inline; },
            ["small"] = s => { s.FontSize = 9f; s.Display = DisplayType.Inline; },
            ["span"] = s => s.Display = DisplayType.Inline,
            ["a"] = s => { s.Display = DisplayType.Inline; s.Color = CssValueParser.ParseColor("#0066CC"); s.TextDecoration = TextDecoration.Underline; },
            ["label"] = s => s.Display = DisplayType.Inline,
            ["abbr"] = s => s.Display = DisplayType.Inline,
            ["cite"] = s => s.Display = DisplayType.Inline,
            ["code"] = s => { s.Display = DisplayType.Inline; s.FontFamily = "Courier"; },
            ["kbd"] = s => { s.Display = DisplayType.Inline; s.FontFamily = "Courier"; },
            ["tt"] = s => { s.Display = DisplayType.Inline; s.FontFamily = "Courier"; },
            ["pre"] = s => s.FontFamily = "Courier",
            ["hr"] = s => { s.Display = DisplayType.Block; s.Margin = new CssEdges(new CssLength(Constants.DefaultMargin)); s.BorderBottom = new CssBorderSide(new CssLength(1f), BorderStyle.Solid, new CssColor(0.5f, 0.5f, 0.5f)); },

            // Layout
            ["p"] = s => { s.Margin = Helpers.DefaultMargin(Constants.DefaultMargin); },
            ["ul"] = s => { s.Margin = new CssEdges(new CssLength(Constants.DefaultMargin)); s.Padding = Helpers.DefaultPadding(20f); },
            ["ol"] = s => { s.Margin = new CssEdges(new CssLength(Constants.DefaultMargin)); s.Padding = Helpers.DefaultPadding(20f); },
            ["li"] = s => s.Display = DisplayType.Block,
            ["blockquote"] = s => { s.Margin = new CssEdges(new CssLength(8f), new CssLength(32f)); s.Padding = new CssEdges(new CssLength(Constants.DefaultPadding)); },

            // Display none
            ["head"] = s => s.Display = DisplayType.None,
            ["script"] = s => s.Display = DisplayType.None,
            ["style"] = s => s.Display = DisplayType.None,
            ["meta"] = s => s.Display = DisplayType.None,
            ["link"] = s => s.Display = DisplayType.None,
            ["title"] = s => s.Display = DisplayType.None,

            // Table
            ["table"] = s => { s.Display = DisplayType.Table; },
            ["thead"] = s => s.Display = DisplayType.Block,
            ["tbody"] = s => s.Display = DisplayType.Block,
            ["tfoot"] = s => s.Display = DisplayType.Block,
            ["tr"] = s => s.Display = DisplayType.Block,
            ["td"] = s => { s.Display = DisplayType.Block; s.Padding = new CssEdges(new CssLength(Constants.DefaultPadding * 0.5f)); },
            ["th"] = s => { s.Display = DisplayType.Block; s.FontWeight = FontWeight.Bold; s.TextAlign = TextAlign.Center; s.Padding = new CssEdges(new CssLength(Constants.DefaultPadding * 0.5f)); },
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
    private void ResolveNode(HtmlNode node,
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

        // 3 Apply Stylesheet rules
        ApplyStylesheetRules(node, style);

        // 3.1 Inline style attribute
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
            TextDecoration = parent.TextDecoration,
            TextTransForm = parent.TextTransForm,
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

    #region  StyleSheet rules
    private void ApplyStylesheetRules(HtmlNode node, ComputedStyle style)
    {
        if (_stylesheetRules.Count != 0)
        {
            List<CssRule> matchingRules = CssSelectorMatcher.GetMatchingRules(node, _stylesheetRules).ToList();
            foreach (CssRule rule in matchingRules)
                foreach ((string prop, string value) in rule.Declarations)
                    ApplyDeclaration(prop, value, style);
        }
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
            case "display":
                style.Display = CssValueParser.ParseDisplay(value);
                break;
            case "width":
                style.Width = CssValueParser.ParseLength(value, style.FontSize);
                break;
            case "height":
                style.Height = CssValueParser.ParseLength(value, style.FontSize);
                break;
            case "margin":
                style.Margin = CssValueParser.ParseEdges(value, style.FontSize);
                break;
            case "margin-top":
                style.Margin = new CssEdges(CssValueParser.ParseLength(value, style.FontSize),
                                            style.Margin.Right,
                                            style.Margin.Bottom,
                                            style.Margin.Left);
                break;
            case "margin-right":
                style.Margin = new CssEdges(style.Margin.Top,
                                            CssValueParser.ParseLength(value, style.FontSize),
                                            style.Margin.Bottom,
                                            style.Margin.Left);
                break;
            case "margin-bottom":
                style.Margin = new CssEdges(style.Margin.Top,
                                            style.Margin.Right,
                                            CssValueParser.ParseLength(value, style.FontSize),
                                            style.Margin.Left);
                break;
            case "margin-left":
                style.Margin = new CssEdges(style.Margin.Top,
                                            style.Margin.Right,
                                            style.Margin.Bottom,
                                            CssValueParser.ParseLength(value, style.FontSize));
                break;
            case "padding":
                style.Padding = CssValueParser.ParseEdges(value, style.FontSize);
                break;
            case "padding-top":
                style.Padding = new CssEdges(CssValueParser.ParseLength(value, style.FontSize),
                                            style.Padding.Right,
                                            style.Padding.Bottom,
                                            style.Padding.Left);
                break;
            case "padding-right":
                style.Padding = new CssEdges(style.Padding.Top,
                                            CssValueParser.ParseLength(value, style.FontSize),
                                            style.Padding.Bottom,
                                            style.Padding.Left);
                break;
            case "padding-bottom":
                style.Padding = new CssEdges(style.Padding.Top,
                                            style.Padding.Right,
                                            CssValueParser.ParseLength(value, style.FontSize),
                                            style.Padding.Left);
                break;
            case "padding-left":
                style.Padding = new CssEdges(style.Padding.Top,
                                            style.Padding.Right,
                                            style.Padding.Bottom,
                                            CssValueParser.ParseLength(value, style.FontSize));
                break;
            case "border":
                {
                    CssBorderSide s = CssValueParser.ParseBorderSide(value, style.FontSize);
                    style.BorderTop = s;
                    style.BorderRight = s;
                    style.BorderBottom = s;
                    style.BorderLeft = s;
                    break;
                }
            case "border-top":
                style.BorderTop = CssValueParser.ParseBorderSide(value, style.FontSize);
                break;
            case "border-right":
                style.BorderRight = CssValueParser.ParseBorderSide(value, style.FontSize);
                break;
            case "border-bottom":
                style.BorderBottom = CssValueParser.ParseBorderSide(value, style.FontSize);
                break;
            case "border-left":
                style.BorderLeft = CssValueParser.ParseBorderSide(value, style.FontSize);
                break;
            case "border-radius":
                style.BorderRadius = CssValueParser.ParseLength(value, style.FontSize);
                break;
            case "color":
                style.Color = CssValueParser.ParseColor(value);
                break;
            case "background-color":
            case "background":
                if (!value.Contains("url(", StringComparison.OrdinalIgnoreCase))
                    style.BackgroundColor = CssValueParser.ParseColor(value);
                break;
            case "font-size":
                style.FontSize = CssValueParser.ParseFontSize(value, style.FontSize);
                break;
            case "font-weight":
                style.FontWeight = CssValueParser.ParseFontWeight(value);
                break;
            case "font-style":
                style.FontStyle = CssValueParser.ParseFontStyle(value);
                break;
            case "font-family":
                style.FontFamily = NormalizeFontFamily(value);
                break;
            case "line-height":
                style.LineHeight = ParseLineHeight(value, style.FontSize);
                break;
            case "text-transform":
                style.TextTransForm = CssValueParser.ParseTextTransform(value);
                break;
            case "text-decoration":
                style.TextDecoration = CssValueParser.ParseTextDecoration(value);
                break;
            case "text-align":
                style.TextAlign = CssValueParser.ParseTextAlign(value);
                break;
            case "page-break-before":
                style.PageBreakBefore = ParsePageBreakAction(value);
                break;
            case "page-break-after":
                style.PageBreakAfter = ParsePageBreakAction(value);
                break;
            case "page-break-inside":
                style.PageBreakInside = ParsePageBreakInside(value);
                break;
            case "flex-direction":
                style.FlexDirection = ParseFlexDirection(value);
                break;
            case "justify-content":
                style.JustifyContent = ParseJustifyContent(value);
                break;
            case "align-items":
                style.AlignItems = ParseAlignItems(value);
                break;
        }
    }
    #endregion

    #region Font shorthand
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

    #region Page break & Flexbox parsers
    /// <summary>
    /// Parses a page-break-before or page-break-after value.
    /// </summary>
    private static PageBreakAction ParsePageBreakAction(string value)
    {
        string v = value.Trim().ToLowerInvariant();
        return v switch
        {
            "always" => PageBreakAction.Always,
            "avoid" => PageBreakAction.Avoid,
            _ => PageBreakAction.Auto,
        };
    }

    /// <summary>
    /// Parses a page-break-inside value.
    /// </summary>
    private static PageBreakInside ParsePageBreakInside(string value)
    {
        string v = value.Trim().ToLowerInvariant();
        return v switch
        {
            "avoid" => PageBreakInside.Avoid,
            _ => PageBreakInside.Auto,
        };
    }

    /// <summary>
    /// Parses a flex-direction value.
    /// </summary>
    private static FlexDirection ParseFlexDirection(string value)
    {
        string v = value.Trim().ToLowerInvariant();
        return v switch
        {
            "row" => FlexDirection.Row,
            "column" => FlexDirection.Column,
            _ => FlexDirection.Row,
        };
    }

    /// <summary>
    /// Parses a justify-content value.
    /// </summary>
    private static JustifyContent ParseJustifyContent(string value)
    {
        string v = value.Trim().ToLowerInvariant();
        return v switch
        {
            "flex-start" => JustifyContent.FlexStart,
            "flex-end" => JustifyContent.FlexEnd,
            "center" => JustifyContent.Center,
            "space-between" => JustifyContent.SpaceBetween,
            "space-around" => JustifyContent.SpaceAround,
            _ => JustifyContent.FlexStart,
        };
    }

    /// <summary>
    /// Parses an align-items value.
    /// </summary>
    private static AlignItems ParseAlignItems(string value)
    {
        string v = value.Trim().ToLowerInvariant();
        return v switch
        {
            "flex-start" => AlignItems.FlexStart,
            "flex-end" => AlignItems.FlexEnd,
            "center" => AlignItems.Center,
            "stretch" => AlignItems.Stretch,
            _ => AlignItems.Stretch,
        };
    }
    #endregion
}
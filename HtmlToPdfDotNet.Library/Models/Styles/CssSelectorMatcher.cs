using HtmlAgilityPack;

namespace HtmlToPdfDotNet.Library.Models.Styles;

/// <summary>
/// Evaluate if a CSS selector matches an element.
///
/// Supports:
///   - Type selector:        p, h1, div
///   - Class selector:       .intro, .container
///   - ID selector:          #main, #header
///   - Universal selector:      *
///   - Compound selectors:   p.intro, div#main
///   - Descendant combinator:  div p  (p inside div at any level)
///   - Multiple selectors:    h1, h2, h3  (handled in CssStyleSheetParser)
/// </summary>
public static class CssSelectorMatcher
{
    /// <summary>
    /// Returns true if the DOM node matches the selector.
    /// </summary>
    /// <param name="node">Node to match</param>
    /// <param name="selector">Selector to match</param>
    /// <returns>True if the node matches the selector, false otherwise</returns>
    public static bool Matches(HtmlNode node, CssSelector selector)
    {
        if (!selector.Parts.Any()) return false;

        // The last part must match the current node
        return MatchDescendant(node, selector.Parts, selector.Parts.Count - 1);
    }

    #region Matching logic methods

    /// <summary>
    /// Recursively checks if the node and its ancestors match the selector parts.
    /// </summary>
    /// <param name="node">Node to match</param>
    /// <param name="parts">Selector parts</param>
    /// <param name="partIndex">Current part index</param>
    /// <returns>True if the node matches the selector parts, false otherwise</returns>
    private static bool MatchDescendant(HtmlNode node, IReadOnlyList<SelectorPart> parts, int partIndex)
    {
        // The current node must match the current part
        if (!MatchesPart(node, parts[partIndex]))
            return false;

        // If it's the first part, the node matches completely
        if (partIndex == 0)
            return true;

        // Search for an ancestor that matches the previous part (descendant combinator)
        HtmlNode ancestor = node.ParentNode;
        while (ancestor != null && ancestor.NodeType == HtmlNodeType.Element)
        {
            if (MatchDescendant(ancestor, parts, partIndex - 1))
                return true;
            ancestor = ancestor.ParentNode;
        }

        return false;
    }

    /// <summary>
    /// Verifies if a node matches a simple part of the selector (without combinator).
    /// </summary>
    /// <param name="node">Node to match</param>
    /// <param name="part">Selector part</param>
    /// <returns>True if the node matches the selector part, false otherwise</returns>
    private static bool MatchesPart(HtmlNode node, SelectorPart part)
    {
        if (node.NodeType != HtmlNodeType.Element) return false;

        // Check tag
        if (part.Tag != null &&
            !node.Name.Equals(part.Tag, StringComparison.OrdinalIgnoreCase))
            return false;

        // Check ID
        if (part.Id != null)
        {
            string nodeId = node.GetAttributeValue("id", "");
            if (!nodeId.Equals(part.Id, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Check classes
        if (part.Classes.Count > 0)
        {
            string classAttr = node.GetAttributeValue("class", "");
            string[] nodeClasses = classAttr.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (string cls in part.Classes)
                if (!nodeClasses.Any(nc => nc.Equals(cls, StringComparison.OrdinalIgnoreCase)))
                    return false;
        }

        return true;
    }
    #endregion

    #region Filtering rules

    /// <summary>
    /// Returns the rules that apply to the node, ordered from lowest to highest specificity.
    /// The order ensures that more specific rules override less specific rules.
    /// </summary>
    /// <param name="node">Node to match</param>
    /// <param name="allRules">All rules to consider</param>
    /// <returns>Ordered list of matching rules</returns>
    public static IEnumerable<CssRule> GetMatchingRules(HtmlNode node, IEnumerable<CssRule> allRules)
    {
        List<(CssRule Rule, int Score)> matches = new();

        foreach (CssRule rule in allRules)
        {
            foreach (CssSelector selector in rule.Selectors)
            {
                // A rule can only apply once per node
                if (Matches(node, selector))
                {
                    matches.Add((rule, selector.SpecificityScore));
                    break;
                }
            }
        }

        return matches
            .OrderBy(m => m.Score)
            .Select(m => m.Rule);
    }
    #endregion
}
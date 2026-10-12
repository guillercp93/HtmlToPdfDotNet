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
        if (selector.Parts.Count == 0) return false;

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
            ReadOnlySpan<char> classAttr = node.GetAttributeValue("class", "");
            IReadOnlyList<string> classes = part.Classes;

            for (int i = 0; i < classes.Count; i++)
                if (!ContainsClassToken(classAttr, classes[i]))
                    return false;
        }

        return true;
    }

    /// <summary>
    /// Checks whether a space-separated class attribute value contains the given class token,
    /// without allocating. Only the space character separates tokens and the comparison is
    /// case-insensitive (ordinal).
    /// </summary>
    /// <param name="classAttr">Value of the class attribute</param>
    /// <param name="cls">Class name to look for</param>
    /// <returns>True if a token equals <paramref name="cls"/>, false otherwise</returns>
    private static bool ContainsClassToken(ReadOnlySpan<char> classAttr, string cls)
    {
        if (cls.Length == 0) return false;

        while (!classAttr.IsEmpty)
        {
            int separator = classAttr.IndexOf(' ');
            ReadOnlySpan<char> token = separator < 0 ? classAttr : classAttr[..separator];

            if (token.Equals(cls, StringComparison.OrdinalIgnoreCase))
                return true;

            if (separator < 0) break;
            classAttr = classAttr[(separator + 1)..];
        }

        return false;
    }
    #endregion

    #region Filtering rules

    /// <summary>
    /// A rule that matched a node, with the data needed to order it in the cascade.
    /// </summary>
    /// <param name="Rule">The matching rule</param>
    /// <param name="Specificity">Specificity score of the first selector of the rule that matched</param>
    /// <param name="Order">Source order of the rule</param>
    internal readonly record struct MatchedRule(CssRule Rule, int Specificity, int Order);

    /// <summary>
    /// Returns the rules that apply to the node, ordered from lowest to highest specificity.
    /// The order ensures that more specific rules override less specific rules.
    /// </summary>
    /// <param name="node">Node to match</param>
    /// <param name="allRules">All rules to consider</param>
    /// <returns>Ordered list of matching rules</returns>
    public static IEnumerable<CssRule> GetMatchingRules(HtmlNode node, IEnumerable<CssRule> allRules)
    {
        IReadOnlyList<CssRule> rules = allRules as IReadOnlyList<CssRule> ?? [.. allRules];
        List<MatchedRule> matches = new();
        CollectMatchingRules(node, rules, matches);

        List<CssRule> result = new(matches.Count);
        foreach (MatchedRule match in matches)
            result.Add(match.Rule);
        return result;
    }

    /// <summary>
    /// Fills <paramref name="buffer"/> with the rules that apply to the node, ordered from lowest
    /// to highest specificity and, for equal specificity, by source order (stable cascade order).
    /// The buffer is cleared first so callers can reuse it across nodes without allocating.
    /// </summary>
    /// <param name="node">Node to match</param>
    /// <param name="allRules">All rules to consider, in source order</param>
    /// <param name="buffer">Buffer that receives the ordered matches</param>
    internal static void CollectMatchingRules(HtmlNode node, IReadOnlyList<CssRule> allRules, List<MatchedRule> buffer)
    {
        buffer.Clear();

        for (int order = 0; order < allRules.Count; order++)
        {
            CssRule rule = allRules[order];
            if (TryMatchRule(node, rule, out int specificity))
                buffer.Add(new MatchedRule(rule, specificity, order));
        }

        SortCascade(buffer);
    }

    /// <summary>
    /// Checks whether any selector of the rule matches the node. A rule applies at most once per
    /// node and takes the specificity of the first of its selectors (in rule order) that matches.
    /// </summary>
    /// <param name="node">Node to match</param>
    /// <param name="rule">Rule whose selectors are tested</param>
    /// <param name="specificity">Specificity score of the first matching selector, or 0</param>
    /// <returns>True if any selector of the rule matches the node, false otherwise</returns>
    internal static bool TryMatchRule(HtmlNode node, CssRule rule, out int specificity)
    {
        IReadOnlyList<CssSelector> selectors = rule.Selectors;

        for (int i = 0; i < selectors.Count; i++)
        {
            if (Matches(node, selectors[i]))
            {
                specificity = selectors[i].SpecificityScore;
                return true;
            }
        }

        specificity = 0;
        return false;
    }

    /// <summary>
    /// Sorts matches into cascade order: specificity ascending, then source order.
    /// </summary>
    /// <param name="buffer">Matches to sort in place</param>
    internal static void SortCascade(List<MatchedRule> buffer)
    {
        // List.Sort is unstable: the source order tiebreak keeps equal-specificity rules in cascade order.
        if (buffer.Count > 1)
            buffer.Sort(static (x, y) => x.Specificity != y.Specificity
                ? x.Specificity.CompareTo(y.Specificity)
                : x.Order.CompareTo(y.Order));
    }
    #endregion
}

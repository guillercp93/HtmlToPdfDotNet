using HtmlAgilityPack;

namespace HtmlToPdfDotNet.Library.Models.Styles;

/// <summary>
/// Pre-filters stylesheet rules by the subject (last part) of their selectors so that each element
/// is only tested against rules that can possibly match it.
///
/// Each selector of a rule is placed in exactly one bucket, chosen from its subject part:
///   - by id, when the subject has an id
///   - otherwise by its first class, when it has classes
///   - otherwise by tag, when it has a tag
///   - otherwise in the universal list
/// Candidates are then verified with the full <see cref="CssSelectorMatcher"/> logic, so the result
/// is identical to testing every rule.
/// </summary>
internal sealed class CssRuleIndex
{
    private readonly IReadOnlyList<CssRule> _rules;
    private readonly Dictionary<string, int[]> _byId;
    private readonly Dictionary<string, int[]> _byClass;
    private readonly Dictionary<string, int[]> _byTag;
    private readonly Dictionary<string, int[]>.AlternateLookup<ReadOnlySpan<char>> _byClassSpan;
    private readonly int[] _universal;

    /// <summary>
    /// Builds the index. The rules must not change afterwards.
    /// </summary>
    /// <param name="rules">Rules in source order; the position of each rule is its source order.</param>
    public CssRuleIndex(IReadOnlyList<CssRule> rules)
    {
        _rules = rules;

        Dictionary<string, List<int>> byId = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<int>> byClass = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<int>> byTag = new(StringComparer.OrdinalIgnoreCase);
        List<int> universal = [];

        for (int order = 0; order < rules.Count; order++)
        {
            foreach (CssSelector selector in rules[order].Selectors)
            {
                // A selector without parts never matches
                if (selector.Parts.Count == 0) continue;

                SelectorPart subject = selector.Parts[^1];
                if (subject.Id != null)
                    AddToBucket(byId, subject.Id, order);
                else if (subject.Classes.Count > 0)
                    AddToBucket(byClass, subject.Classes[0], order);
                else if (subject.Tag != null)
                    AddToBucket(byTag, subject.Tag, order);
                else
                    AddUnique(universal, order);
            }
        }

        _byId = Freeze(byId);
        _byClass = Freeze(byClass);
        _byTag = Freeze(byTag);
        _byClassSpan = _byClass.GetAlternateLookup<ReadOnlySpan<char>>();
        _universal = [.. universal];
    }

    #region Lookup
    /// <summary>
    /// Fills <c>buffer.Matches</c> with the rules that apply to the node, in cascade order
    /// (specificity ascending, then source order). Same result as
    /// <see cref="CssSelectorMatcher.CollectMatchingRules"/> over all rules, without allocating
    /// once the buffer has grown.
    /// </summary>
    /// <param name="node">Node to match</param>
    /// <param name="buffer">Reusable scratch space; overwritten on each call</param>
    public void CollectMatchingRules(HtmlNode node, CssRuleMatchBuffer buffer)
    {
        List<int> candidates = buffer.Candidates;
        List<CssSelectorMatcher.MatchedRule> matches = buffer.Matches;
        candidates.Clear();
        matches.Clear();

        if (node.NodeType != HtmlNodeType.Element) return;

        if (_byId.Count > 0 && _byId.TryGetValue(node.GetAttributeValue("id", ""), out int[]? idBucket))
            candidates.AddRange(idBucket);

        if (_byClass.Count > 0)
        {
            ReadOnlySpan<char> classAttr = node.GetAttributeValue("class", "");
            while (!classAttr.IsEmpty)
            {
                int separator = classAttr.IndexOf(' ');
                ReadOnlySpan<char> token = separator < 0 ? classAttr : classAttr[..separator];

                if (!token.IsEmpty && _byClassSpan.TryGetValue(token, out int[]? classBucket))
                    candidates.AddRange(classBucket);

                if (separator < 0) break;
                classAttr = classAttr[(separator + 1)..];
            }
        }

        if (_byTag.Count > 0 && _byTag.TryGetValue(node.Name, out int[]? tagBucket))
            candidates.AddRange(tagBucket);

        candidates.AddRange(_universal);

        // Dedupe: a rule can be reached from several buckets (comma lists, repeated class tokens).
        // Sorting also visits candidates in source order, like the brute-force scan.
        candidates.Sort();
        int previous = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            int order = candidates[i];
            if (order == previous) continue;
            previous = order;

            CssRule rule = _rules[order];
            if (CssSelectorMatcher.TryMatchRule(node, rule, out int specificity))
                matches.Add(new CssSelectorMatcher.MatchedRule(rule, specificity, order));
        }

        CssSelectorMatcher.SortCascade(matches);
    }

    /// <summary>
    /// Source orders of the rules indexed under the given id (for tests and diagnostics).
    /// </summary>
    internal IReadOnlyList<int> GetIdBucket(string id) => _byId.GetValueOrDefault(id) ?? [];

    /// <summary>
    /// Source orders of the rules indexed under the given class (for tests and diagnostics).
    /// </summary>
    internal IReadOnlyList<int> GetClassBucket(string cls) => _byClass.GetValueOrDefault(cls) ?? [];

    /// <summary>
    /// Source orders of the rules indexed under the given tag (for tests and diagnostics).
    /// </summary>
    internal IReadOnlyList<int> GetTagBucket(string tag) => _byTag.GetValueOrDefault(tag) ?? [];

    /// <summary>
    /// Source orders of the rules whose subject has no id, class or tag (for tests and diagnostics).
    /// </summary>
    internal IReadOnlyList<int> Universal => _universal;
    #endregion

    #region Build helpers
    /// <summary>
    /// Adds a rule to a keyed bucket, once.
    /// </summary>
    private static void AddToBucket(Dictionary<string, List<int>> buckets, string key, int order)
    {
        if (!buckets.TryGetValue(key, out List<int>? bucket))
        {
            bucket = [];
            buckets[key] = bucket;
        }
        AddUnique(bucket, order);
    }

    /// <summary>
    /// Appends the rule unless it is already the last entry. Rules are added in source order,
    /// so this keeps buckets duplicate-free and sorted.
    /// </summary>
    private static void AddUnique(List<int> bucket, int order)
    {
        if (bucket.Count == 0 || bucket[^1] != order)
            bucket.Add(order);
    }

    /// <summary>
    /// Converts the build-time buckets to compact arrays.
    /// </summary>
    private static Dictionary<string, int[]> Freeze(Dictionary<string, List<int>> buckets)
    {
        Dictionary<string, int[]> frozen = new(buckets.Count, StringComparer.OrdinalIgnoreCase);
        foreach ((string key, List<int> bucket) in buckets)
            frozen[key] = [.. bucket];
        return frozen;
    }
    #endregion
}

/// <summary>
/// Reusable scratch space for <see cref="CssRuleIndex.CollectMatchingRules"/>. Not thread-safe:
/// create one per <see cref="StyleResolver.Resolve"/> call.
/// </summary>
internal sealed class CssRuleMatchBuffer
{
    /// <summary>
    /// Matching rules in cascade order after a lookup.
    /// </summary>
    public List<CssSelectorMatcher.MatchedRule> Matches { get; } = [];

    /// <summary>
    /// Candidate rule source orders gathered from the index buckets.
    /// </summary>
    public List<int> Candidates { get; } = [];
}

namespace HtmlToPdfDotNet.Library.Models.Styles;

/// <summary>
/// Represents a CSS rule, which consists of selectors and declarations.
/// </summary>
public sealed class CssRule
{
    /// <summary>
    /// Each selector is evaluated independently.
    /// These selectors are separated by commas in the CSS.
    /// </summary>
    public IReadOnlyList<CssSelector> Selectors { get; init; } = [];

    /// <summary>
    /// Declaration in block: property -> value
    /// </summary>
    public IReadOnlyDictionary<string, string> Declarations { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// A CSS selector with its specificity calculated.
/// Supports: tag, .class, #id, combinations and descendant.
/// </summary>
public sealed class CssSelector
{
    /// <summary>
    /// Original text selector. Ex: "div.container p"
    /// </summary>
    public string Raw { get; init; } = string.Empty;

    /// <summary>
    /// Specificity of the selector. A: id, B: class and attributes, C: tag and pseudo elements
    /// It's compared lexicographically. (1,0,0) > (0,99,99)
    /// </summary>
    public (int A, int B, int C) Specificity { get; init; }

    /// <summary>
    /// Parts of the selector divided by descendant combinator
    /// Example: "div.container p" -> ["div", ".container", "p"]
    /// </summary>
    public IReadOnlyList<SelectorPart> Parts { get; init; } = [];

    /// <summary>
    /// Specificity like unique integer number for quick sort (max 99 by component)
    /// </summary>
    public int SpecificityScore => (Specificity.A * 10_000) + (Specificity.B * 100) + Specificity.C;
}

/// <summary>
/// A part of a selector, separated by descendant combinator.
/// Example: "div.container p" -> ["div", ".container", "p"]
/// </summary>
public sealed class SelectorPart
{
    /// <summary>
    /// Tag name in lower case, like "div", "p", "a"
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// Id of the element, like "container"
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Classes of the element, like ["container", "item"]
    /// </summary>
    public IReadOnlyList<string> Classes { get; init; } = [];
}
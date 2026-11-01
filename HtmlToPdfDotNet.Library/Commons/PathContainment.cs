namespace HtmlToPdfDotNet.Library.Commons;

/// <summary>
/// Single containment check for every file the library reads from or writes to under
/// <see cref="ConversionOptions.BasePath"/>.
/// </summary>
/// <remarks>
/// A lexical prefix check is not enough: a case-insensitive comparison lets
/// <c>/data/Assets</c> pass for base <c>/data/assets</c> on a case-sensitive filesystem,
/// and a symbolic link inside the base can point anywhere. This check resolves symbolic
/// links to their physical location and compares paths with
/// <see cref="Path.GetRelativePath(string, string)"/>, which follows the host platform's
/// case rules. A link swapped between the check and the file access (a race) is not covered;
/// that requires no-follow opens the .NET file APIs do not expose portably.
/// </remarks>
internal static class PathContainment
{
    private const int MaxLinkHops = 40;

    /// <summary>
    /// Throws <see cref="UnauthorizedAccessException"/> unless <paramref name="fullPath"/>,
    /// after resolving symbolic links, lies inside <paramref name="baseDirectory"/>.
    /// </summary>
    /// <param name="baseDirectory">The directory that must contain the path.</param>
    /// <param name="fullPath">The absolute, lexically normalized path to check.</param>
    /// <param name="displayPath">The path to show in the error message (never the resolved path).</param>
    internal static void EnsureInside(string baseDirectory, string fullPath, string displayPath)
    {
        int hops = 0;
        string physicalBase = ResolvePhysicalPath(Path.GetFullPath(baseDirectory), ref hops);
        string physicalPath = ResolvePhysicalPath(Path.GetFullPath(fullPath), ref hops);

        if (!IsInside(physicalBase, physicalPath))
        {
            throw new UnauthorizedAccessException(
                $"Access to the path '{displayPath}' is denied. It lies outside the allowed base directory.");
        }
    }

    /// <summary>True when <paramref name="path"/> is <paramref name="root"/> or below it.</summary>
    private static bool IsInside(string root, string path)
    {
        string relative = Path.GetRelativePath(root, path);
        return relative == "."
               || (!Path.IsPathRooted(relative)
                   && relative != ".."
                   && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                   && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal));
    }

    /// <summary>
    /// Replaces every symbolic link along <paramref name="fullPath"/> with its final target.
    /// Components that do not exist yet (for example a file about to be written) are kept as-is.
    /// </summary>
    private static string ResolvePhysicalPath(string fullPath, ref int hops)
    {
        string root = Path.GetPathRoot(fullPath) ?? string.Empty;
        string[] parts = fullPath[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        string current = root;
        foreach (string part in parts)
        {
            string next = Path.Combine(current, part);
            FileInfo entry = new(next);
            if (entry.LinkTarget is not null)
            {
                if (++hops > MaxLinkHops)
                {
                    throw new IOException($"Too many levels of symbolic links in '{fullPath}'.");
                }

                FileSystemInfo? target = entry.ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null)
                {
                    // The target's own parent directories may also be links.
                    next = ResolvePhysicalPath(Path.GetFullPath(target.FullName), ref hops);
                }
            }

            current = next;
        }

        return current;
    }
}

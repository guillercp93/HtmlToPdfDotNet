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
    /// Resolves <paramref name="fullPath"/> the way the operating system does: one component at a
    /// time, replacing each symbolic link with its raw target and applying <c>..</c> to the
    /// already-resolved directory. A link target is never normalized as text, because in
    /// <c>link/..</c> the kernel follows <c>link</c> before going up. Components that do not exist
    /// yet (for example a file about to be written) are kept as-is.
    /// </summary>
    private static string ResolvePhysicalPath(string fullPath, ref int hops)
    {
        string current = Path.GetPathRoot(fullPath) ?? string.Empty;
        LinkedList<string> pending = new(SplitComponents(fullPath[current.Length..]));

        while (pending.First is { } node)
        {
            string part = node.Value;
            pending.RemoveFirst();

            if (part == ".") continue;
            if (part == "..")
            {
                current = Path.GetDirectoryName(current) ?? current; // ".." at the root stays at the root
                continue;
            }

            string next = Path.Combine(current, part);
            string? linkTarget = new FileInfo(next).LinkTarget;
            if (linkTarget is null)
            {
                current = next;
                continue;
            }

            if (++hops > MaxLinkHops)
            {
                throw new IOException($"Too many levels of symbolic links in '{fullPath}'.");
            }

            // Continue with the link's target followed by the components still pending.
            // A relative target is resolved from the directory that contains the link (current).
            string targetRoot = Path.GetPathRoot(linkTarget) ?? string.Empty;
            if (Path.IsPathRooted(linkTarget))
            {
                current = targetRoot;
            }

            string[] targetParts = SplitComponents(linkTarget[targetRoot.Length..]);
            for (int i = targetParts.Length - 1; i >= 0; i--)
            {
                pending.AddFirst(targetParts[i]);
            }
        }

        return current;
    }

    private static string[] SplitComponents(string path)
        => path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
}

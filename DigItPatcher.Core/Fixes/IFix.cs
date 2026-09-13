namespace DigItPatcher.Core.Fixes;

/// <summary>What the scan read at a fix's own site, which is what licenses the fix or withdraws it.</summary>
public enum FixState
{
    /// <summary>Original vanilla bytes are present.</summary>
    Original,

    /// <summary>Patched bytes are present.</summary>
    Patched,

    /// <summary>Bytes match neither original nor patched sequence.</summary>
    Modified,

    /// <summary>Target stream is smaller than patch byte range.</summary>
    FileTooSmall,

    /// <summary>The file this fix targets is not in the install.</summary>
    FileMissing,

    /// <summary>The file does not carry the structure this fix's site is addressed against.</summary>
    SiteNotFound,
}

/// <summary>What a fix's <see cref="IFix.Offset"/> is measured from, since a segment starts wherever its own build puts it.</summary>
public enum FixOrigin
{
    /// <summary>The start of the target file.</summary>
    File,

    /// <summary>The start of <c>MAIN.EXE</c>'s third segment.</summary>
    Seg3,
}

/// <summary>One fix: the bytes it expects at a fixed offset in one of the game's files, and the bytes it writes there.</summary>
public interface IFix
{
    /// <summary>Stable identifier this fix is looked up by, in <see cref="FixCatalog"/> and in the backup's own record.</summary>
    string Id { get; }

    /// <summary>The game file this fix writes to, by name.</summary>
    string TargetFile { get; }

    /// <summary>The builds this fix was worked out against, which is what separates our gap from a file somebody has been in.</summary>
    IReadOnlyList<string> DerivedFor { get; }

    /// <summary>What <see cref="Offset"/> is measured from.</summary>
    FixOrigin Origin { get; }

    /// <summary>Byte offset of this fix's own site, from <see cref="Origin"/>.</summary>
    long Offset { get; }

    /// <summary>The vanilla bytes expected at this fix's site before this patch is applied.</summary>
    ReadOnlyMemory<byte> OriginalBytes { get; }

    /// <summary>The bytes written at this fix's site when this patch is applied.</summary>
    ReadOnlyMemory<byte> PatchedBytes { get; }

    /// <summary>Determines patch state of the specified stream.</summary>
    FixState GetState(Stream stream);

    /// <summary>Applies patch bytes to the specified stream.</summary>
    void Apply(Stream stream);

    /// <summary>Reverts patch bytes in the specified stream.</summary>
    void Revert(Stream stream);
}

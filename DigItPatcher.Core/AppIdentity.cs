namespace DigItPatcher.Core;

/// <summary>The build's own name, version, and author, read once at the composition root and passed inward.</summary>
public sealed record AppIdentity(string Product, string Version, string Author);

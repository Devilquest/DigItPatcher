namespace DigItPatcher.Tests;

/// <summary>What a test needs of the copy of the game it is pointed at.</summary>
[Flags]
public enum GameNeeds
{
    /// <summary>Any folder holding the game's archives, whichever build it is.</summary>
    AnyCopy = 0,

    /// <summary>A build this tool's fixes and rebuilt resources were derived against.</summary>
    SupportedBuild = 1,
}

/// <summary>A fact that reports itself skipped, not passed, when the copy of the game will not do.</summary>
public sealed class RequiresGameFactAttribute : FactAttribute
{
    public RequiresGameFactAttribute(GameNeeds needs = GameNeeds.AnyCopy) => Skip = TestPaths.SkipReasonFor(needs);
}

/// <summary>The <see cref="TheoryAttribute"/> counterpart of <see cref="RequiresGameFactAttribute"/>.</summary>
public sealed class RequiresGameTheoryAttribute : TheoryAttribute
{
    public RequiresGameTheoryAttribute(GameNeeds needs = GameNeeds.AnyCopy) => Skip = TestPaths.SkipReasonFor(needs);
}

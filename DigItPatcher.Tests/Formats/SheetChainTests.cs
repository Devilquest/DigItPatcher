using DigItPatcher.Core.Formats;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the structural test that tells an intact image entry from a displaced one.</summary>
public class SheetChainTests
{
    private static SheetWalk WalkOf(byte[] sheet) => SheetChain.Walk(sheet, new byte[FrameCodec.WorkingSize]);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void ASheetThatWasBuiltWholeWalksWhole(int frameCount)
        => Assert.True(WalkOf(SheetArchiveBuilder.Sheet(frameCount)).IsIntact);

    [Fact]
    public void SomethingTooShortToHoldAHeaderIsNotASheet()
        => Assert.False(WalkOf([1, 2, 3]).IsIntact);

    [Fact]
    public void AByteInsertedIntoAFrameBreaksTheWalkAndBracketsItself()
    {
        var sheet = SheetArchiveBuilder.Sheet(2);
        int frameStart = 772 + 2;

        var walk = WalkOf(SheetArchiveBuilder.WithByteInserted(sheet, frameStart + 1, 0xF4)[..sheet.Length]);

        Assert.False(walk.IsIntact);
        Assert.InRange(frameStart + 1, walk.FaultStart, walk.FaultEnd);
    }

    [Fact]
    public void AFrameCountThatDoesNotMatchTheSecondFrameOffsetBreaksTheWalk()
    {
        var sheet = SheetArchiveBuilder.Sheet(2);
        sheet[2]++;

        Assert.False(WalkOf(sheet).IsIntact);
    }
}

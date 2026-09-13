using DigItPatcher.Core.Fixes;
using DigItPatcher.Core.Patching;

namespace DigItPatcher.App.ViewModels;

/// <summary>One row of the fix list: a name, what the scan read, and the explanation behind its (i).</summary>
internal sealed class FixRowViewModel
{
    private readonly FixText _text;

    public FixRowViewModel(FixScan scan)
    {
        _text = FixText.For(scan.Fix.Id);
        Outcome = scan.Outcome;
    }

    public string Name => _text.Name;

    public FixOutcome Outcome { get; }

    /// <summary>What the row says about the copy, which is nothing when the copy is in the ordinary state.</summary>
    public string StateText => Outcome switch
    {
        FixOutcome.AlreadyFixed => "Already fixed",
        FixOutcome.NotAvailable => "Not available for this release",
        FixOutcome.UnexpectedBytes => "Unexpected bytes",
        _ => string.Empty
    };

    public string WhatIsWrong => _text.WhatIsWrong;

    public string WhyItHappens => _text.WhyItHappens;

    public string WhatChanges => _text.WhatChanges;
}

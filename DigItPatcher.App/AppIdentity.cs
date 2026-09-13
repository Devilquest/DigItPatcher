using System.Reflection;
using DigItPatcher.Core;

namespace DigItPatcher.App;

/// <summary>Builds the tool's <see cref="AppIdentity"/> from the running assembly's own attributes.</summary>
internal static class AppIdentityFactory
{
    /// <summary>Reads the identity from the entry assembly, the one place Core itself never looks.</summary>
    public static AppIdentity FromEntryAssembly()
    {
        var assembly = Assembly.GetEntryAssembly();

        return new AppIdentity(
            assembly?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? string.Empty,
            assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty,
            assembly?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty);
    }
}

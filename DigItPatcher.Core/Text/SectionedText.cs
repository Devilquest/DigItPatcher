namespace DigItPatcher.Core.Text;

/// <summary>An embedded text file split into <c># Section</c> blocks, each collapsed to one line.</summary>
internal static class SectionedText
{
    /// <summary>Reads every <paramref name="allowed"/> section out of <paramref name="contents"/>, failing on an
    /// unknown, repeated, empty, or missing one.</summary>
    public static Dictionary<string, string> Read(string sourceName, string[] allowed, string contents)
    {
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        string? current = null;
        var body = new List<string>();

        void Close()
        {
            if (current is null) return;

            var text = string.Join(" ", body.Where(line => line.Length > 0));
            if (text.Length == 0) throw new InvalidOperationException($"Section '{current}' of '{sourceName}' is empty.");

            sections[current] = text;
            body.Clear();
        }

        foreach (var line in contents.Split('\n').Select(line => line.TrimEnd('\r').Trim()))
        {
            if (!line.StartsWith("# ", StringComparison.Ordinal))
            {
                body.Add(line);
                continue;
            }

            Close();
            current = line[2..].Trim();

            if (!allowed.Contains(current, StringComparer.Ordinal))
                throw new InvalidOperationException($"'{sourceName}' has an unknown section '{current}'.");

            if (sections.ContainsKey(current))
                throw new InvalidOperationException($"'{sourceName}' repeats the section '{current}'.");
        }

        Close();

        var missing = allowed.FirstOrDefault(section => !sections.ContainsKey(section));
        return missing is null
            ? sections
            : throw new InvalidOperationException($"'{sourceName}' is missing the section '{missing}'.");
    }
}

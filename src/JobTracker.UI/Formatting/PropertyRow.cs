using Avalonia;

namespace JobTracker.UI.Formatting;

/// <summary>
/// One line of a free-form JSON object as the detail view shows it: a name, indented by how deep it sits, and a
/// value, or no value for a line that only heads the nested lines under it.
/// </summary>
/// <param name="Depth">0 for a top-level property, one more for each level of nesting.</param>
/// <param name="Name">The property name, made readable ("techStack" becomes "Tech stack").</param>
/// <param name="Value">The value as text; null for an object or a list of objects, whose lines follow.</param>
public sealed record PropertyRow(int Depth, string Name, string? Value)
{
    private const double IndentPerLevel = 16;

    /// <summary>The left margin the view gives the name.</summary>
    public Thickness Indent => new(Depth * IndentPerLevel, 0, 0, 0);
}

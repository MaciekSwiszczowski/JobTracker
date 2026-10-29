using System.Globalization;
using System.Text;
using System.Text.Json;

namespace JobTracker.UI.Formatting;

/// <summary>
/// Turns a free-form JSON object (an application's extra part, an event's details) into rows the view can list
/// without knowing anything about its shape. This is the whole reason the UI can show properties nobody
/// designed for: whatever the skill stores, this shows.
/// </summary>
internal static class PropertyFlattener
{
    /// <summary>The rows for <paramref name="element"/>; none for null, undefined, or an empty object.</summary>
    public static IReadOnlyList<PropertyRow> Flatten(JsonElement? element)
    {
        var rows = new List<PropertyRow>();
        switch (element)
        {
            case null:
            case { ValueKind: JsonValueKind.Undefined or JsonValueKind.Null }:
                break;
            case { ValueKind: JsonValueKind.Object } value:
                AddProperties(value, 0, rows);
                break;
            case { } value:
                AddValue("Value", value, 0, rows);
                break;
        }

        return rows;
    }

    /// <summary>
    /// "techStack" becomes "Tech stack", "salary_max" "Salary max". A run of capitals stays one word, so
    /// "sourceURL" becomes "Source URL".
    /// </summary>
    public static string Humanize(string name)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c is '_' or '-' or ' ')
            {
                Flush();
                continue;
            }

            var startsWord = char.IsUpper(c) && i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]));
            if (startsWord)
            {
                Flush();
            }

            word.Append(c);
        }

        Flush();
        var text = string.Join(' ', words.Select(static w => w.Length > 1 && w.All(char.IsUpper) ? w : w.ToLowerInvariant()));
        return text.Length == 0 ? name : char.ToUpperInvariant(text[0]) + text[1..];

        void Flush()
        {
            if (word.Length > 0)
            {
                words.Add(word.ToString());
                word.Clear();
            }
        }
    }

    private static void AddProperties(JsonElement value, int depth, List<PropertyRow> rows)
    {
        // The enumerator is a disposable struct; disposing it ourselves is what IDISP004 asks for.
        using var properties = value.EnumerateObject();
        foreach (var property in properties)
        {
            AddValue(Humanize(property.Name), property.Value, depth, rows);
        }
    }

    private static void AddValue(string name, JsonElement value, int depth, List<PropertyRow> rows)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                rows.Add(new PropertyRow(depth, name, null));
                AddProperties(value, depth + 1, rows);
                break;
            case JsonValueKind.Array when ItemsOf(value).TrueForAll(static item => item.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array)):
                rows.Add(new PropertyRow(depth, name, string.Join(", ", ItemsOf(value).Select(Text))));
                break;
            case JsonValueKind.Array:
                rows.Add(new PropertyRow(depth, name, null));
                var position = 1;
                foreach (var item in ItemsOf(value))
                {
                    AddValue(string.Create(CultureInfo.InvariantCulture, $"#{position++}"), item, depth + 1, rows);
                }

                break;
            default:
                rows.Add(new PropertyRow(depth, name, Text(value)));
                break;
        }
    }

    // By index rather than EnumerateArray, whose disposable enumerator cannot be disposed from inside a LINQ query.
    private static List<JsonElement> ItemsOf(JsonElement array)
    {
        var items = new List<JsonElement>(array.GetArrayLength());
        for (var i = 0; i < items.Capacity; i++)
        {
            items.Add(array[i]);
        }

        return items;
    }

    private static string Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.True => "yes",
        JsonValueKind.False => "no",
        JsonValueKind.Null => "(none)",
        _ => value.GetRawText(),
    };
}

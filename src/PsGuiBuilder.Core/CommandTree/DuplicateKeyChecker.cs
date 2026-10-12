// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text;
using System.Text.Json;

namespace PsGuiBuilder.Core.CommandTree;

internal static class DuplicateKeyChecker
{
    public static void Check(string json, IList<CommandTreeError> errors)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false
        });

        var frames = new Stack<Frame>();
        Frame? current = null;
        string? pendingName = null;

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    Push(frames, ref current, ref pendingName, isArray: false);
                    break;
                case JsonTokenType.StartArray:
                    Push(frames, ref current, ref pendingName, isArray: true);
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    frames.Pop();
                    current = frames.Count == 0 ? null : frames.Peek();
                    pendingName = null;
                    break;
                case JsonTokenType.PropertyName:
                    var name = reader.GetString() ?? "";
                    if (current is null || current.Names is null)
                    {
                        errors.Add(new CommandTreeError("$", "A property name appeared outside an object."));
                        return;
                    }

                    if (!current.Names.Add(name))
                    {
                        errors.Add(new CommandTreeError(
                            current.Path,
                            $"Duplicate property \"{name}\"."));
                    }

                    pendingName = name;
                    break;
                case JsonTokenType.String:
                case JsonTokenType.Number:
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                    if (current is { IsArray: true })
                        current.Index++;
                    pendingName = null;
                    break;
            }
        }
    }

    private static void Push(Stack<Frame> frames, ref Frame? current, ref string? pendingName, bool isArray)
    {
        var path = NextPath(current, pendingName);
        if (current is { IsArray: true })
            current.Index++;

        var frame = new Frame(path, isArray);
        frames.Push(frame);
        current = frame;
        pendingName = null;
    }

    private static string NextPath(Frame? parent, string? pendingName)
    {
        if (parent is null)
            return "$";

        if (parent.IsArray)
            return JsonPath.Index(parent.Path, parent.Index);

        return pendingName is null ? parent.Path : JsonPath.Property(parent.Path, pendingName);
    }

    private sealed class Frame
    {
        public Frame(string path, bool isArray)
        {
            Path = path;
            IsArray = isArray;
            Names = isArray ? null : new HashSet<string>(StringComparer.Ordinal);
        }

        public string Path { get; }

        public bool IsArray { get; }

        public HashSet<string>? Names { get; }

        public int Index { get; set; }
    }
}

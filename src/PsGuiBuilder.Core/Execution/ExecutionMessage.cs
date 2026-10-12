// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace PsGuiBuilder.Core.Execution;

public static class ExecutionKinds
{
    public const string Script = "script";
    public const string Cancel = "cancel";
    public const string Stdout = "stdout";
    public const string Stderr = "stderr";
    public const string Exit = "exit";
    public const string Error = "error";
    public const string Cancelled = "cancelled";
}

public sealed class ExecutionMessage
{
    public string Kind { get; set; } = "";

    public string? Text { get; set; }

    public string? Script { get; set; }

    public int? Code { get; set; }

    public string? Message { get; set; }

    public static ExecutionMessage ScriptMessage(string script) => new() { Kind = ExecutionKinds.Script, Script = script };

    public static ExecutionMessage CancelMessage() => new() { Kind = ExecutionKinds.Cancel };

    public static ExecutionMessage StdoutMessage(string text) => new() { Kind = ExecutionKinds.Stdout, Text = text };

    public static ExecutionMessage StderrMessage(string text) => new() { Kind = ExecutionKinds.Stderr, Text = text };

    public static ExecutionMessage ExitMessage(int code) => new() { Kind = ExecutionKinds.Exit, Code = code };

    public static ExecutionMessage ErrorMessage(string message) => new() { Kind = ExecutionKinds.Error, Message = message };

    public static ExecutionMessage CancelledMessage() => new() { Kind = ExecutionKinds.Cancelled };
}

public static class ExecutionMessageCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static byte[] ToUtf8(ExecutionMessage message) =>
        JsonSerializer.SerializeToUtf8Bytes(message, Options);

    public static ExecutionMessage FromUtf8(ReadOnlySpan<byte> utf8)
    {
        var message = JsonSerializer.Deserialize<ExecutionMessage>(utf8, Options)
            ?? throw new InvalidDataException("Empty execution message.");
        if (string.IsNullOrWhiteSpace(message.Kind))
            throw new InvalidDataException("Execution message is missing kind.");

        return message;
    }
}

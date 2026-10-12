// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

namespace PsGuiBuilder.Core.Execution;

public interface IExecutionOutput
{
    void WriteStdout(string text);

    void WriteStderr(string text);
}

public sealed class ScriptRequest
{
    private static readonly IReadOnlyDictionary<string, string> NoSecrets =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public ScriptRequest(string script, IReadOnlyDictionary<string, string>? secrets = null, bool interactive = false)
    {
        Script = script ?? "";
        Secrets = secrets is null || secrets.Count == 0
            ? NoSecrets
            : new Dictionary<string, string>(secrets, StringComparer.Ordinal);
        Interactive = interactive;
    }

    public string Script { get; }

    public IReadOnlyDictionary<string, string> Secrets { get; }

    public bool Interactive { get; }

    public static ScriptRequest Plain(string script) => new(script);
}

public interface IScriptInvoker
{
    Task<int> InvokeAsync(ScriptRequest request, IExecutionOutput output, CancellationToken cancellationToken);
}

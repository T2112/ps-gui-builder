// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.CommandTree;

namespace PsGuiBuilder.Core.Execution;

public enum LaunchStage
{
    CredentialPrompt,
    Elevation,
    SignInWindow,
    Command
}

public enum LaunchStatus
{
    Completed,
    Cancelled,
    Failed
}

public sealed class LaunchOutcome
{
    private LaunchOutcome(LaunchStatus status, int exitCode, string? error)
    {
        Status = status;
        ExitCode = exitCode;
        Error = error;
    }

    public LaunchStatus Status { get; }

    public int ExitCode { get; }

    public string? Error { get; }

    public static LaunchOutcome Completed(int exitCode) => new(LaunchStatus.Completed, exitCode, null);

    public static LaunchOutcome Cancelled() => new(LaunchStatus.Cancelled, 0, null);

    public static LaunchOutcome Failed(string error) => new(LaunchStatus.Failed, 0, error);
}

public static class LaunchPlan
{
    public static IReadOnlyList<LaunchStage> Stages(bool credentialSignIn, bool interactiveSignIn, bool elevate)
    {
        var stages = new List<LaunchStage>();
        if (credentialSignIn)
            stages.Add(LaunchStage.CredentialPrompt);

        if (elevate)
            stages.Add(LaunchStage.Elevation);

        stages.Add(interactiveSignIn ? LaunchStage.SignInWindow : LaunchStage.Command);
        return stages;
    }
}

public sealed class StageGate
{
    private readonly object _lock = new();
    private LaunchStage? _open;

    public LaunchStage? OpenStage
    {
        get
        {
            lock (_lock)
                return _open;
        }
    }

    public void Enter(LaunchStage stage)
    {
        lock (_lock)
        {
            if (_open is not null)
                throw new InvalidOperationException($"Cannot open {stage} while {_open} is still open.");

            _open = stage;
        }
    }

    public void Exit(LaunchStage stage)
    {
        lock (_lock)
        {
            if (_open != stage)
                throw new InvalidOperationException($"Cannot close {stage}.");

            _open = null;
        }
    }
}

public sealed class CredentialAnswer
{
    private char[] _password;

    public CredentialAnswer(string username, char[] password)
    {
        Username = username ?? "";
        _password = password is null ? Array.Empty<char>() : (char[])password.Clone();
    }

    public string Username { get; private set; }

    public string CopyPassword() => new(_password);

    public void Clear()
    {
        Array.Clear(_password, 0, _password.Length);
        _password = Array.Empty<char>();
        Username = "";
    }
}

public interface ICredentialPrompt
{
    CredentialAnswer? Ask(SignInRequirement requirement);
}

public sealed class RunCredentials
{
    private CredentialAnswer? _held;

    public CredentialAnswer? Prompt(SignInRequirement requirement, Func<SignInRequirement, CredentialAnswer?> ask)
    {
        if (requirement is null)
            throw new ArgumentNullException(nameof(requirement));
        if (ask is null)
            throw new ArgumentNullException(nameof(ask));
        if (requirement.Mode != SignInMode.Credential)
            throw new InvalidOperationException("Only a credential sign-in asks for a password.");

        Discard();
        var answer = ask(requirement);
        _held = answer;
        return answer;
    }

    public void Discard()
    {
        _held?.Clear();
        _held = null;
    }
}

public static class LaunchDriver
{
    public static async Task<LaunchOutcome> RunAsync(
        bool credentialSignIn,
        bool interactiveSignIn,
        bool elevate,
        SignInRequirement? signIn,
        IReadOnlyDictionary<string, string> commandSecrets,
        StageGate gate,
        Func<SignInRequirement, CredentialAnswer?> ask,
        Func<CancellationToken, Task<string?>>? prepare,
        Func<CancellationToken, Task> elevateProcess,
        Func<string?, IReadOnlyDictionary<string, string>, CancellationToken, Task<int>> execute,
        CancellationToken cancellationToken,
        Action<LaunchStage>? stageStarting = null)
    {
        if (commandSecrets is null)
            throw new ArgumentNullException(nameof(commandSecrets));
        if (gate is null)
            throw new ArgumentNullException(nameof(gate));
        if (ask is null)
            throw new ArgumentNullException(nameof(ask));
        if (elevateProcess is null)
            throw new ArgumentNullException(nameof(elevateProcess));
        if (execute is null)
            throw new ArgumentNullException(nameof(execute));

        var secrets = new Dictionary<string, string>(commandSecrets, StringComparer.Ordinal);
        var credentials = new RunCredentials();
        string? username = null;
        var prepared = false;
        try
        {
            foreach (var stage in LaunchPlan.Stages(credentialSignIn, interactiveSignIn, elevate))
            {
                if (!prepared && stage != LaunchStage.CredentialPrompt)
                {
                    prepared = true;
                    var prepareError = prepare is null ? null : await prepare(cancellationToken);
                    if (prepareError is not null)
                        return LaunchOutcome.Failed(prepareError);
                }

                gate.Enter(stage);
                LaunchOutcome? outcome = null;
                try
                {
                    stageStarting?.Invoke(stage);
                    switch (stage)
                    {
                        case LaunchStage.CredentialPrompt:
                            if (signIn is null)
                                throw new InvalidOperationException("Credential sign-in is missing its requirement.");

                            var answer = credentials.Prompt(signIn, ask);
                            if (answer is null)
                            {
                                outcome = LaunchOutcome.Cancelled();
                                break;
                            }

                            username = answer.Username;
                            secrets[SignInRequirement.SecretKey] = answer.CopyPassword();
                            answer.Clear();
                            credentials.Discard();
                            break;
                        case LaunchStage.Elevation:
                            await elevateProcess(cancellationToken);
                            break;
                        case LaunchStage.SignInWindow:
                        case LaunchStage.Command:
                            outcome = LaunchOutcome.Completed(await execute(username, secrets, cancellationToken));
                            break;
                    }
                }
                finally
                {
                    gate.Exit(stage);
                }

                if (outcome is not null)
                    return outcome;
            }

            return LaunchOutcome.Failed("The launch plan did not run a command.");
        }
        finally
        {
            credentials.Discard();
            secrets.Clear();
        }
    }
}

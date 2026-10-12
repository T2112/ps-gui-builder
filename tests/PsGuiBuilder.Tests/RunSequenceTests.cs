// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.Core.Execution;

namespace PsGuiBuilder.Tests;

public class RunSequenceTests
{
    private static readonly SignInRequirement Credential = new(
        "Active Directory",
        SignInMode.Credential,
        "Enter credentials.",
        "Import-Module ActiveDirectory");

    [Fact]
    public void Credential_prompt_finishes_before_elevation_and_the_command()
    {
        Assert.Equal(
            new[] { LaunchStage.CredentialPrompt, LaunchStage.Elevation, LaunchStage.Command },
            LaunchPlan.Stages(credentialSignIn: true, interactiveSignIn: false, elevate: true));
    }

    [Fact]
    public void Interactive_sign_in_opens_only_after_elevation_closes()
    {
        Assert.Equal(
            new[] { LaunchStage.Elevation, LaunchStage.SignInWindow },
            LaunchPlan.Stages(credentialSignIn: false, interactiveSignIn: true, elevate: true));
    }

    [Fact]
    public void A_credential_sign_in_is_never_optional()
    {
        Assert.Equal(
            new[] { LaunchStage.CredentialPrompt, LaunchStage.Command },
            LaunchPlan.Stages(credentialSignIn: true, interactiveSignIn: false, elevate: false));
        Assert.DoesNotContain(
            LaunchStage.CredentialPrompt,
            LaunchPlan.Stages(credentialSignIn: false, interactiveSignIn: true, elevate: false));
    }

    [Fact]
    public async Task Each_run_asks_again_and_forgets_the_previous_password()
    {
        var pending = new Queue<string>(new[] { "first-secret", "second-secret" });
        var seen = new List<string>();
        var asks = 0;

        CredentialAnswer? Ask(SignInRequirement requirement)
        {
            asks++;
            Assert.Equal(Credential, requirement);
            return new CredentialAnswer("ada", pending.Dequeue().ToCharArray());
        }

        Task<int> Execute(string? username, IReadOnlyDictionary<string, string> secrets, CancellationToken cancellationToken)
        {
            Assert.Equal("ada", username);
            seen.Add(secrets[SignInRequirement.SecretKey]);
            return Task.FromResult(0);
        }

        for (var run = 0; run < 2; run++)
        {
            var outcome = await LaunchDriver.RunAsync(
                credentialSignIn: true,
                interactiveSignIn: false,
                elevate: true,
                Credential,
                new Dictionary<string, string>(),
                new StageGate(),
                Ask,
                prepare: null,
                elevateProcess: _ => Task.CompletedTask,
                Execute,
                CancellationToken.None);

            Assert.Equal(LaunchStatus.Completed, outcome.Status);
        }

        Assert.Equal(2, asks);
        Assert.Equal(new[] { "first-secret", "second-secret" }, seen);
    }

    [Fact]
    public async Task Cancelling_the_prompt_does_not_elevate_or_run()
    {
        var elevated = false;
        var executed = false;
        var outcome = await LaunchDriver.RunAsync(
            credentialSignIn: true,
            interactiveSignIn: false,
            elevate: true,
            Credential,
            new Dictionary<string, string>(),
            new StageGate(),
            _ => null,
            prepare: null,
            _ =>
            {
                elevated = true;
                return Task.CompletedTask;
            },
            (_, _, _) =>
            {
                executed = true;
                return Task.FromResult(0);
            },
            CancellationToken.None);

        Assert.Equal(LaunchStatus.Cancelled, outcome.Status);
        Assert.False(elevated);
        Assert.False(executed);
    }

    [Fact]
    public async Task Nothing_else_opens_while_the_credential_dialog_is_open()
    {
        var gate = new StageGate();
        var outcome = await LaunchDriver.RunAsync(
            credentialSignIn: true,
            interactiveSignIn: false,
            elevate: true,
            Credential,
            new Dictionary<string, string>(),
            gate,
            _ =>
            {
                Assert.Equal(LaunchStage.CredentialPrompt, gate.OpenStage);
                Assert.Throws<InvalidOperationException>(() => gate.Enter(LaunchStage.Elevation));
                return new CredentialAnswer("ada", "S3cret-Value!".ToCharArray());
            },
            prepare: null,
            _ => Task.CompletedTask,
            (_, secrets, _) =>
            {
                Assert.Equal("S3cret-Value!", secrets[SignInRequirement.SecretKey]);
                return Task.FromResult(3);
            },
            CancellationToken.None);

        Assert.Equal(LaunchStatus.Completed, outcome.Status);
        Assert.Equal(3, outcome.ExitCode);
        Assert.Null(gate.OpenStage);
    }

    [Fact]
    public async Task Elevation_closes_before_the_sign_in_window_opens()
    {
        var gate = new StageGate();
        var sawElevation = false;
        var outcome = await LaunchDriver.RunAsync(
            credentialSignIn: false,
            interactiveSignIn: true,
            elevate: true,
            signIn: null,
            new Dictionary<string, string>(),
            gate,
            _ => throw new InvalidOperationException("Interactive sign-in does not ask for a password."),
            prepare: _ => Task.FromResult<string?>(null),
            _ =>
            {
                sawElevation = true;
                Assert.Equal(LaunchStage.Elevation, gate.OpenStage);
                return Task.CompletedTask;
            },
            (_, _, _) =>
            {
                Assert.True(sawElevation);
                Assert.Equal(LaunchStage.SignInWindow, gate.OpenStage);
                return Task.FromResult(0);
            },
            CancellationToken.None);

        Assert.Equal(LaunchStatus.Completed, outcome.Status);
    }

    [Fact]
    public async Task A_prepare_failure_does_not_open_elevation()
    {
        var elevated = false;
        var outcome = await LaunchDriver.RunAsync(
            credentialSignIn: true,
            interactiveSignIn: false,
            elevate: true,
            Credential,
            new Dictionary<string, string>(),
            new StageGate(),
            _ => new CredentialAnswer("ada", "S3cret-Value!".ToCharArray()),
            _ => Task.FromResult<string?>("PowerShell was not found."),
            _ =>
            {
                elevated = true;
                return Task.CompletedTask;
            },
            (_, _, _) => Task.FromResult(0),
            CancellationToken.None);

        Assert.Equal(LaunchStatus.Failed, outcome.Status);
        Assert.Equal("PowerShell was not found.", outcome.Error);
        Assert.False(elevated);
    }

    [Fact]
    public void Clearing_an_answer_drops_the_password()
    {
        var answer = new CredentialAnswer("ada", "S3cret-Value!".ToCharArray());
        Assert.Equal("S3cret-Value!", answer.CopyPassword());

        answer.Clear();

        Assert.Equal("", answer.Username);
        Assert.Equal("", answer.CopyPassword());
    }
}

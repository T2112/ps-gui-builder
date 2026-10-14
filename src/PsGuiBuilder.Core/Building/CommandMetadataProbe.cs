// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Diagnostics;
using System.Text.RegularExpressions;
using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.Core.Execution;

namespace PsGuiBuilder.Core.Building;

public sealed class CommandProbeResult
{
    public CommandProbeResult(
        CommandDefinition? command,
        string? error,
        string? notice,
        string? snapshotJson = null,
        IReadOnlyList<string>? parameterSets = null,
        string? parameterSet = null)
    {
        Command = command;
        Error = error;
        Notice = notice;
        SnapshotJson = snapshotJson;
        ParameterSets = parameterSets ?? Array.Empty<string>();
        ParameterSet = parameterSet;
    }

    public CommandDefinition? Command { get; }

    public string? Error { get; }

    public string? Notice { get; }

    public string? SnapshotJson { get; }

    public IReadOnlyList<string> ParameterSets { get; }

    public string? ParameterSet { get; }
}

public sealed partial class CommandMetadataProbe
{
    public const string CommandEnvironmentVariable = "PSGUIBUILDER_COMMAND";

    public Task<CommandProbeResult> LookupAsync(string name, CancellationToken cancellationToken) =>
        LookupAsync(name, PowerShellEdition.PowerShell7, cancellationToken);

    public async Task<CommandProbeResult> LookupAsync(string name, PowerShellEdition edition, CancellationToken cancellationToken)
    {
        if (!CommandName().IsMatch(name ?? ""))
        {
            return new CommandProbeResult(
                null,
                "Enter a cmdlet name like Get-ChildItem, or Module\\Get-Command.",
                null);
        }

        var resolution = PowerShellLocator.Resolve(edition);
        if (resolution.Error is not null || resolution.Shell is null)
            return new CommandProbeResult(null, resolution.Error ?? "PowerShell was not found.", null);

        var encoded = PowerShellCommandEncoding.ToEncodedCommand(ProbeScript);
        var startInfo = new ProcessStartInfo
        {
            FileName = resolution.Shell.FileName,
            Arguments = resolution.Shell.Arguments + " -EncodedCommand " + encoded,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.Environment[CommandEnvironmentVariable] = name;

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                return new CommandProbeResult(null, "Could not start " + resolution.Shell.FileName + ".", resolution.Notice);
        }
        catch (Exception ex)
        {
            return new CommandProbeResult(null, ex.Message, resolution.Notice);
        }

        var stdout = ProcessOutputText.ReadAllAsync(process.StandardOutput.BaseStream, cancellationToken);
        var stderr = ProcessOutputText.ReadAllAsync(process.StandardError.BaseStream, cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var output = await stdout.ConfigureAwait(false);
        var error = await stderr.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(error) ? output : error;
            return new CommandProbeResult(
                null,
                string.IsNullOrWhiteSpace(detail) ? "Get-Command failed for " + name + "." : detail.Trim(),
                resolution.Notice);
        }

        try
        {
            var view = CommandMetadataMapper.Read(output, shell: edition);
            return new CommandProbeResult(view.Command, null, resolution.Notice, output.Trim(), view.ParameterSets, view.ParameterSet);
        }
        catch (ArgumentException ex)
        {
            return new CommandProbeResult(null, ex.Message, resolution.Notice);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    [GeneratedRegex(@"^(?:[A-Za-z][A-Za-z0-9._-]*\\)?[A-Za-z][A-Za-z0-9]*-[A-Za-z][A-Za-z0-9]*$")]
    private static partial Regex CommandName();

    internal const string ProbeScript = """
        $ErrorActionPreference = 'Stop'
        $ProgressPreference = 'SilentlyContinue'
        $ConfirmPreference = 'None'
        $requested = $env:PSGUIBUILDER_COMMAND
        if ([string]::IsNullOrWhiteSpace($requested)) { throw 'PSGUIBUILDER_COMMAND is required.' }
        $command = Get-Command -Name $requested
        $synopsis = ''
        try {
          $help = Get-Help -Name $command.Name -ErrorAction SilentlyContinue
          if ($null -ne $help -and $null -ne $help.Synopsis) {
            $synopsis = ([string]$help.Synopsis).Trim()
          }
        } catch {
          $synopsis = ''
        }
        $common = @(
          'Verbose','Debug','ErrorAction','ErrorVariable','WarningAction','WarningVariable',
          'InformationAction','InformationVariable','OutVariable','OutBuffer','PipelineVariable','ProgressAction'
        )
        $setRows = New-Object System.Collections.Generic.List[object]
        foreach ($set in $command.ParameterSets) {
          $setRows.Add([pscustomobject]@{
            name = [string]$set.Name
            isDefault = [bool]$set.IsDefault
          }) | Out-Null
        }
        function Get-Kind([type]$type) {
          if ($null -eq $type) { return 'string' }
          if ($type.IsGenericType -and $type.GetGenericTypeDefinition().FullName -eq 'System.Nullable`1') {
            $type = $type.GetGenericArguments()[0]
          }
          $name = $type.FullName
          if ($name -eq 'System.Management.Automation.SwitchParameter') { return 'switch' }
          if ($name -eq 'System.Boolean') { return 'bool' }
          if ($name -eq 'System.Security.SecureString') { return 'securestring' }
          if ($name -eq 'System.Int16' -or $name -eq 'System.Int32' -or $name -eq 'System.Int64' -or $name -eq 'System.UInt16' -or $name -eq 'System.UInt32' -or $name -eq 'System.UInt64' -or $name -eq 'System.Byte' -or $name -eq 'System.SByte') { return 'int' }
          if ($name -eq 'System.Decimal' -or $name -eq 'System.Double' -or $name -eq 'System.Single') { return 'number' }
          if ($type.IsEnum) { return 'choice' }
          if ($type.IsArray) { return 'array' }
          return 'string'
        }
        $rows = New-Object System.Collections.Generic.List[object]
        foreach ($parameter in $command.Parameters.Values) {
          $skip = $false
          foreach ($known in $common) { if ($known -eq $parameter.Name) { $skip = $true } }
          if ($skip) { continue }
          $mandatory = $true
          $seen = $false
          foreach ($set in $command.ParameterSets) {
            foreach ($setParameter in $set.Parameters) {
              if ($setParameter.Name -eq $parameter.Name) {
                $seen = $true
                if (-not $setParameter.IsMandatory) { $mandatory = $false }
              }
            }
          }
          if (-not $seen) { $mandatory = $false }
          $kind = Get-Kind $parameter.ParameterType
          $enumType = $parameter.ParameterType
          if ($enumType.IsArray) { $enumType = $enumType.GetElementType() }
          $choiceNames = $null
          if ($null -ne $enumType -and $enumType.IsEnum) {
            $choiceNames = [enum]::GetNames($enumType)
            if ($parameter.ParameterType.IsArray) { $kind = 'array' } else { $kind = 'choice' }
          }
          foreach ($attribute in $parameter.Attributes) {
            if ($attribute -is [System.Management.Automation.ValidateSetAttribute]) {
              $valid = @($attribute.ValidValues)
              if ($null -eq $choiceNames) { $choiceNames = $valid } else { $choiceNames = @($choiceNames) + $valid }
              if ($kind -ne 'array' -and $kind -ne 'switch') { $kind = 'choice' }
            }
          }
          $setNames = New-Object System.Collections.Generic.List[string]
          $mandatoryNames = New-Object System.Collections.Generic.List[string]
          foreach ($set in $command.ParameterSets) {
            foreach ($setParameter in $set.Parameters) {
              if ($setParameter.Name -ne $parameter.Name) { continue }
              $setNames.Add([string]$set.Name) | Out-Null
              if ($setParameter.IsMandatory) { $mandatoryNames.Add([string]$set.Name) | Out-Null }
            }
          }
          $rows.Add([pscustomobject]@{
            name = $parameter.Name
            kind = $kind
            mandatory = [bool]$mandatory
            choices = $choiceNames
            sets = $setNames
            mandatorySets = $mandatoryNames
          }) | Out-Null
        }
        $moduleName = $command.ModuleName
        [pscustomobject]@{
          name = $command.Name
          module = $moduleName
          synopsis = $synopsis
          parameterSets = $setRows
          parameters = $rows
        } | ConvertTo-Json -Depth 6 -Compress
        """;
}

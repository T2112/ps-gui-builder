// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using PsGuiBuilder.Core.CommandTree;
using PsGuiBuilder.Core.Execution;

namespace PsGuiBuilder.Core.Building;

public sealed class CommandProbeResult
{
    public CommandProbeResult(CommandDefinition? command, string? error, string? notice)
    {
        Command = command;
        Error = error;
        Notice = notice;
    }

    public CommandDefinition? Command { get; }

    public string? Error { get; }

    public string? Notice { get; }
}

public sealed partial class CommandMetadataProbe
{
    public const string CommandEnvironmentVariable = "PSGUIBUILDER_COMMAND";

    public async Task<CommandProbeResult> LookupAsync(string name, CancellationToken cancellationToken)
    {
        if (!CommandName().IsMatch(name ?? ""))
        {
            return new CommandProbeResult(
                null,
                "Enter a cmdlet name like Get-ChildItem, or Module\\Get-Command.",
                null);
        }

        var resolution = PowerShellLocator.Resolve(PowerShellEdition.PowerShell7);
        if (resolution.Error is not null || resolution.Shell is null)
            return new CommandProbeResult(null, resolution.Error ?? "PowerShell 7 (pwsh) was not found.", null);

        var encoded = PowerShellCommandEncoding.ToEncodedCommand(ProbeScript);
        var startInfo = new ProcessStartInfo
        {
            FileName = resolution.Shell.FileName,
            Arguments = resolution.Shell.Arguments + " -EncodedCommand " + encoded,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = resolution.Shell.Encoding,
            StandardErrorEncoding = resolution.Shell.Encoding
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

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
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
            return new CommandProbeResult(CommandMetadataMapper.FromSnapshot(output), null, resolution.Notice);
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

    [GeneratedRegex(@"^(?:[A-Za-z][A-Za-z0-9.]*\\)?[A-Za-z][A-Za-z0-9]*-[A-Za-z][A-Za-z0-9]*$")]
    private static partial Regex CommandName();

    internal const string ProbeScript = """
        $ErrorActionPreference = 'Stop'
        $ProgressPreference = 'SilentlyContinue'
        $requested = $env:PSGUIBUILDER_COMMAND
        if ([string]::IsNullOrWhiteSpace($requested)) { throw 'PSGUIBUILDER_COMMAND is required.' }
        $command = Get-Command -Name $requested
        $common = @(
          'Verbose','Debug','ErrorAction','ErrorVariable','WarningAction','WarningVariable',
          'InformationAction','InformationVariable','OutVariable','OutBuffer','PipelineVariable','ProgressAction'
        )
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
          $rows.Add([pscustomobject]@{
            name = $parameter.Name
            kind = $kind
            mandatory = [bool]$mandatory
            choices = $choiceNames
          }) | Out-Null
        }
        $moduleName = $command.ModuleName
        [pscustomobject]@{
          name = $command.Name
          module = $moduleName
          parameters = $rows
        } | ConvertTo-Json -Depth 5 -Compress
        """;
}

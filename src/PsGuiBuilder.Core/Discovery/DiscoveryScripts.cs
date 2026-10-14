// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

namespace PsGuiBuilder.Core.Discovery;

public static class DiscoveryScripts
{
    public const string Catalog = """
        $ErrorActionPreference = 'Continue'
        $ProgressPreference = 'SilentlyContinue'
        $ConfirmPreference = 'None'
        $mods = New-Object System.Collections.Generic.List[object]
        $cmds = New-Object System.Collections.Generic.List[object]
        $seen = @{}
        $available = @(Get-Module -ListAvailable | Sort-Object Name, @{ Expression = { $_.Version }; Descending = $true })
        foreach ($m in $available) {
          $name = [string]$m.Name
          if ([string]::IsNullOrWhiteSpace($name)) { continue }
          if ($seen.ContainsKey($name)) { continue }
          $seen[$name] = $true
          $mods.Add([pscustomobject]@{ name = $name; version = [string]$m.Version }) | Out-Null
          $exported = @()
          try {
            $exported = @(Get-Command -Module $name -ErrorAction SilentlyContinue)
          } catch {
            $exported = @()
          }
          foreach ($c in $exported) {
            $type = [string]$c.CommandType
            if ($type -ne 'Cmdlet' -and $type -ne 'Function' -and $type -ne 'Filter' -and $type -ne 'Alias') { continue }
            $commandName = [string]$c.Name
            if ([string]::IsNullOrWhiteSpace($commandName)) { continue }
            $verb = [string]$c.Verb
            $noun = [string]$c.Noun
            if ([string]::IsNullOrWhiteSpace($verb) -or [string]::IsNullOrWhiteSpace($noun)) {
              $dash = $commandName.IndexOf('-')
              if ($dash -gt 0) {
                if ([string]::IsNullOrWhiteSpace($verb)) { $verb = $commandName.Substring(0, $dash) }
                if ([string]::IsNullOrWhiteSpace($noun)) { $noun = $commandName.Substring($dash + 1) }
              }
            }
            $cmds.Add([pscustomobject]@{
              name = $commandName
              module = $name
              verb = $verb
              noun = $noun
              commandType = $type
            }) | Out-Null
          }
        }
        [pscustomobject]@{ modules = $mods.ToArray(); commands = $cmds.ToArray() } | ConvertTo-Json -Depth 5 -Compress
        """;
}

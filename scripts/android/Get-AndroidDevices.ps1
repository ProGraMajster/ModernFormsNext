[CmdletBinding()]
param(
    [switch]$IncludeUnavailable
)

$adb = & (Join-Path $PSScriptRoot 'Resolve-Adb.ps1') -PathOnly
$lines = & $adb devices -l
$devices = foreach ($line in $lines | Select-Object -Skip 1) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line.Trim() -split '\s+'
    if ($parts.Count -lt 2) { continue }

    $properties = @{}
    foreach ($part in $parts | Select-Object -Skip 2) {
        $separator = $part.IndexOf(':')
        if ($separator -gt 0) {
            $properties[$part.Substring(0, $separator)] = $part.Substring($separator + 1)
        }
    }

    [pscustomobject]@{
        Serial = $parts[0]
        Status = $parts[1]
        Kind = if ($parts[0] -like 'emulator-*') { 'Emulator' } else { 'Physical' }
        Model = $properties['model']
        Product = $properties['product']
        Device = $properties['device']
        TransportId = $properties['transport_id']
    }
}

$selected = if ($IncludeUnavailable) { $devices } else { $devices | Where-Object Status -eq 'device' }
if (-not $selected) {
    Write-Warning 'No usable Android device is connected. Check USB debugging, authorization, or start an emulator.'
}
$selected

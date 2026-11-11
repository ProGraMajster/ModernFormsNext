<#
.SYNOPSIS
Observes whether a specific ModernFormsNext Designer version is publicly listed.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('\A[0-9]{1,9}(?:\.[0-9]{1,9}){2,3}\z')][string]$VsixVersion
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$publisher = 'ProGraMajster'
$extension = 'ModernFormsNextDesigner'
# Gallery Name filter and IncludeVersions plus ExcludeNonValidated flags.
$query = @{
    filters = @(@{ criteria = @(@{ filterType = 7; value = "$publisher.$extension" }); pageNumber = 1; pageSize = 1 })
    flags = 33
} | ConvertTo-Json -Depth 5
$status = 'unavailable'
try {
    $response = Invoke-RestMethod -Method Post -ContentType 'application/json' -TimeoutSec 15 -MaximumRedirection 0 -Body $query -Uri 'https://marketplace.visualstudio.com/_apis/public/gallery/extensionquery?api-version=7.1-preview.1'
    if (@($response.results).Count -eq 1) {
        $entries = @($response.results[0].extensions)
        if ($entries.Count -eq 0) { $status = 'not-listed' }
        elseif ($entries.Count -eq 1 -and $entries[0].publisher.publisherName -ieq $publisher -and
            $entries[0].extensionName -ieq $extension) {
            $status = if (@($entries[0].versions.version) -ccontains $VsixVersion) { 'published' } else { 'not-listed' }
        }
    }
}
catch {
    Write-Verbose 'The public Gallery query did not provide a verifiable result.'
}
[ordered]@{
    status = $status
    version = $VsixVersion
    listing = "https://marketplace.visualstudio.com/items?itemName=$publisher.$extension"
    observedAtUtc = [datetime]::UtcNow.ToString('o')
}

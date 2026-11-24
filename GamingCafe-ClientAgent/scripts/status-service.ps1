<#
.SYNOPSIS
    Reports status of the Gaming Cafe Client Agent Windows Service.
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "GamingCafeClientAgent"
)

$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $svc) {
    Write-Host "Service '$ServiceName' is NOT INSTALLED." -ForegroundColor Red
}
else {
    Write-Host "Service:       $($svc.Name)"
    Write-Host "Display Name:  $($svc.DisplayName)"
    Write-Host "Status:        $($svc.Status)" -ForegroundColor ($svc.Status -eq 'Running' ? 'Green' : 'Yellow')
    Write-Host "Start Type:    $($svc.StartType)"
}

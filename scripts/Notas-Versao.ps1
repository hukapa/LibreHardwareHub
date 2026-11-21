# LibreHardwareHub - leitura das notas de uma versao no CHANGELOG.md.
# Usado por Lancar-Release.ps1 (verifica que as notas existem) e pelo workflow de release (corpo da Release).
#
# Uso:  . "$PSScriptRoot\Notas-Versao.ps1"
#       $notas = Get-ReleaseNotesSection -Version "0.11.0" -Path "CHANGELOG.md"
#
# Aceita os titulos "## v0.11.0 - 2026-12-01", "## [0.11.0]" e "## 0.11.0". A seccao termina no proximo titulo "## ".

function Get-ReleaseNotesSection {
    param(
        [Parameter(Mandatory = $true)][string]$Version,
        [Parameter(Mandatory = $true)][string]$Path
    )

    if (-not (Test-Path $Path)) {
        return $null
    }

    $heading = '^##\s+\[?v?' + [regex]::Escape($Version) + '\]?(\s|$)'
    $lines = Get-Content -Path $Path -Encoding UTF8
    $found = $false
    $body = New-Object System.Collections.Generic.List[string]

    foreach ($line in $lines) {
        if (-not $found) {
            if ($line -match $heading) {
                $found = $true
            }
            continue
        }

        if ($line -match '^##\s') {
            break
        }

        $body.Add($line)
    }

    if (-not $found) {
        return $null
    }

    while ($body.Count -gt 0 -and [string]::IsNullOrWhiteSpace($body[0])) { $body.RemoveAt(0) }
    while ($body.Count -gt 0 -and [string]::IsNullOrWhiteSpace($body[$body.Count - 1])) { $body.RemoveAt($body.Count - 1) }

    if ($body.Count -eq 0) {
        return $null
    }

    return ($body -join "`n")
}

$ErrorActionPreference = 'Stop'

$obfuscarVersion = '2.2.39'
$obfuscarRoot = Join-Path $PSScriptRoot "..\tools\obfuscar"
$obfuscarPackageRoot = Join-Path $obfuscarRoot $obfuscarVersion
$obfuscarPath = Join-Path $obfuscarPackageRoot 'tools\Obfuscar.Console.exe'

function Get-ObfuscarExecutable {
    if (Test-Path -LiteralPath $obfuscarPath) {
        return $obfuscarPath
    }

    New-Item -ItemType Directory -Path $obfuscarRoot -Force | Out-Null
    $archivePath = Join-Path $obfuscarRoot "Obfuscar.$obfuscarVersion.zip"

    Write-Host "Downloading Obfuscar $obfuscarVersion..."
    Invoke-WebRequest `
        -Uri "https://api.nuget.org/v3-flatcontainer/obfuscar/$obfuscarVersion/obfuscar.$obfuscarVersion.nupkg" `
        -OutFile $archivePath

    if (Test-Path -LiteralPath $obfuscarPackageRoot) {
        Remove-Item -LiteralPath $obfuscarPackageRoot -Recurse -Force
    }
    Expand-Archive -LiteralPath $archivePath -DestinationPath $obfuscarPackageRoot -Force
    Remove-Item -LiteralPath $archivePath -Force

    if (!(Test-Path -LiteralPath $obfuscarPath)) {
        throw "Obfuscar download completed, but its executable was not found: $obfuscarPath"
    }

    return $obfuscarPath
}

function Invoke-ApplicationObfuscation {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ApplicationPath
    )

    if (!(Test-Path -LiteralPath $ApplicationPath)) {
        throw "Cannot obfuscate a missing application: $ApplicationPath"
    }

    $obfuscarExecutable = Get-ObfuscarExecutable
    $applicationFileName = Split-Path -Leaf $ApplicationPath
    $temporaryOutputPath = Join-Path ([System.IO.Path]::GetTempPath()) ("AMacQ-Obfuscar-" + [Guid]::NewGuid().ToString('N'))
    $configurationPath = Join-Path $temporaryOutputPath 'Obfuscar.xml'

    New-Item -ItemType Directory -Path $temporaryOutputPath -Force | Out-Null
    try {
        $escapedApplicationPath = [System.Security.SecurityElement]::Escape($ApplicationPath)
        $escapedOutputPath = [System.Security.SecurityElement]::Escape($temporaryOutputPath)
        @"
<?xml version="1.0" encoding="utf-8" ?>
<Obfuscator>
  <Var name="OutPath" value="$escapedOutputPath" />
  <!-- Preserve the WPF public surface and binding property names. -->
  <Var name="KeepPublicApi" value="true" />
  <Var name="HidePrivateApi" value="true" />
  <Var name="RenameProperties" value="false" />
  <Var name="RenameEvents" value="false" />
  <Var name="RenameFields" value="true" />
  <Var name="UseUnicodeNames" value="false" />
  <Module file="$escapedApplicationPath" />
</Obfuscator>
"@ | Set-Content -LiteralPath $configurationPath -Encoding UTF8

        Write-Host "Obfuscating: $applicationFileName"
        & $obfuscarExecutable $configurationPath
        if ($LASTEXITCODE -ne 0) {
            throw "Obfuscar failed for $applicationFileName, exit code: $LASTEXITCODE"
        }

        $obfuscatedApplicationPath = Join-Path $temporaryOutputPath $applicationFileName
        if (!(Test-Path -LiteralPath $obfuscatedApplicationPath)) {
            throw "Obfuscar did not produce the expected file: $obfuscatedApplicationPath"
        }

        Move-Item -LiteralPath $obfuscatedApplicationPath -Destination $ApplicationPath -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryOutputPath) {
            Remove-Item -LiteralPath $temporaryOutputPath -Recurse -Force
        }
    }
}

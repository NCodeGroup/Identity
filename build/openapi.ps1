#!/usr/bin/env pwsh
#requires -version 7

<#
.SYNOPSIS
    Generates the reference host's OpenAPI document at build time and writes it to artifacts/openapi/v1.json.
.DESCRIPTION
    The script builds the reference host (unless -NoBuild), then runs the GenerateOpenApiDocuments target of
    Microsoft.Extensions.ApiDescription.Server in the Staging environment with LocalDevelopment:Enabled, an ephemeral
    root key and a static issuer, so the host starts without a database or a secret store. The document describes the
    product whatever the ApiReference switches say (ADR-1481). With -Version the script sets info.version of the
    document; the publish workflow stamps the artifact version this way (ADR-1505). A version with a character outside
    [0-9A-Za-z.-] is refused before any build. The script prints the document's path.
.PARAMETER Configuration
    The build configuration. Defaults to Release.
.PARAMETER NoBuild
    Skips the build of the host and generates from its existing output.
.PARAMETER Version
    The version to stamp into info.version, such as 0.0.1-alpha.57. Empty keeps the version the host reports.
.EXAMPLE
    ./build/openapi.ps1 -Version 0.0.1-alpha.57
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$NoBuild,
    [string]$Version = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Version -and $Version -cnotmatch '^[0-9A-Za-z.-]+$') { throw "VERSION '$Version' is not a version." }

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$hostDirectory = Join-Path $repositoryRoot 'hosts' 'NCode.Identity.OpenId.Host'
$hostProject = Join-Path $hostDirectory 'NCode.Identity.OpenId.Host.csproj'
$documentPath = Join-Path $repositoryRoot 'artifacts' 'openapi' 'v1.json'

if (-not $NoBuild) {
    & dotnet build $hostProject -c $Configuration -m:2 --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }
}

$environment = [ordered]@{
    'ASPNETCORE_ENVIRONMENT'                         = 'Staging'
    'OpenId__TenantResolution__StaticSingle__Issuer' = 'https://identity.example'
    'SecretProtection__RootKeyProvider'              = 'ephemeral'
    'LocalDevelopment__Enabled'                      = 'true'
}
$saved = @{}
foreach ($name in $environment.Keys) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name)
}

try {
    foreach ($name in $environment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $environment[$name])
    }

    $cache = Join-Path $hostDirectory 'obj' 'NCode.Identity.OpenId.Host.OpenApiFiles.cache'
    if (Test-Path -LiteralPath $cache) { Remove-Item -LiteralPath $cache -Force }

    & dotnet msbuild $hostProject -t:GenerateOpenApiDocuments "-p:Configuration=$Configuration" -nologo
    if ($LASTEXITCODE -ne 0) { throw "GenerateOpenApiDocuments failed with exit code $LASTEXITCODE." }
}
finally {
    # A variable that was unset is removed, because PowerShell passes $null to a .NET string as '', which Linux keeps
    # as an empty value.
    foreach ($name in $environment.Keys) {
        if ($null -eq $saved[$name]) {
            Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        }
        else {
            [Environment]::SetEnvironmentVariable($name, $saved[$name])
        }
    }
}

if (-not (Test-Path -LiteralPath $documentPath)) { throw "${documentPath}: the OpenAPI document was not generated." }

if ($Version) {
    $document = [System.Text.Json.Nodes.JsonNode]::Parse([System.IO.File]::ReadAllText($documentPath))
    $document['info']['version'] = $Version
    $options = [System.Text.Json.JsonSerializerOptions]::new()
    $options.WriteIndented = $true
    $text = $document.ToJsonString($options).ReplaceLineEndings("`n")
    [System.IO.File]::WriteAllText($documentPath, $text, [System.Text.UTF8Encoding]::new($false))
}

Write-Host $documentPath

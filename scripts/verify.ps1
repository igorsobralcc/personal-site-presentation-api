param(
    [switch]$SkipPostgres,
    [switch]$SkipContainer
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)

function Invoke-Check {
    param([string]$Name, [scriptblock]$Command)
    Write-Host "Checking $Name"
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

if (-not $SkipPostgres -and [string]::IsNullOrWhiteSpace($env:PRESENTATION_TEST_CONNECTION_STRING)) {
    throw 'Set PRESENTATION_TEST_CONNECTION_STRING to a disposable PostgreSQL database. Use -SkipPostgres only for explicitly incomplete local verification.'
}
if (-not $SkipContainer -and -not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker is required. Use -SkipContainer only for explicitly incomplete local verification.'
}

$originalRequirement = $env:PRESENTATION_REQUIRE_POSTGRES
try {
    $env:PRESENTATION_REQUIRE_POSTGRES = if ($SkipPostgres) { 'false' } else { 'true' }
    Invoke-Check 'repository policy' { python scripts/validate-repository.py }
    Invoke-Check 'validator positive and negative fixtures' { python -m unittest discover -s scripts -p 'test_*.py' }
    Invoke-Check 'generated contract and client drift' { python scripts/contract-artifacts.py --check }
    Invoke-Check 'restore' { dotnet restore PersonalSite.Presentation.slnx }
    Invoke-Check 'read-only format' { dotnet format PersonalSite.Presentation.slnx --verify-no-changes --no-restore --verbosity quiet }
    Invoke-Check 'Release build and analyzers' { dotnet build PersonalSite.Presentation.slnx --configuration Release --no-restore --warnaserror }
    Invoke-Check 'HTTP, contract, PostgreSQL tests and repository coverage' {
        dotnet test PersonalSite.Presentation.slnx --configuration Release --no-build --no-restore `
            --logger 'trx;LogFileName=verification.trx' `
            /p:CollectCoverage=true /p:CoverletOutput=../../TestResults/coverage/ `
            /p:CoverletOutputFormat=cobertura /p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=total
    }
    Invoke-Check 'changed-feature coverage' { python scripts/check-feature-coverage.py TestResults/coverage/coverage.cobertura.xml }
    Invoke-Check 'migration model drift' {
        dotnet ef migrations has-pending-model-changes --project src/PersonalSite.Presentation.Api --configuration Release --no-build
    }
    Invoke-Check 'idempotent migration SQL' {
        dotnet ef migrations script --idempotent --project src/PersonalSite.Presentation.Api --configuration Release --no-build --output TestResults/migrations.sql
    }
    Invoke-Check 'production publish' {
        dotnet publish src/PersonalSite.Presentation.Api --configuration Release --no-build --no-restore --output TestResults/publish
    }
    if (-not $SkipContainer) {
        Invoke-Check 'production image build' { docker build --tag personal-site-presentation-api:ci . }
        Invoke-Check 'container identity, health and database failure' { bash scripts/smoke-container.sh personal-site-presentation-api:ci }
    }
    if ($SkipPostgres -or $SkipContainer) {
        Write-Warning 'Local verification is partial: explicitly skipped infrastructure remains required before publication.'
    }
} finally {
    $env:PRESENTATION_REQUIRE_POSTGRES = $originalRequirement
}

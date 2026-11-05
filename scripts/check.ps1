<#
.SYNOPSIS
    The repository's checks, one command per tier, so nobody has to remember which tool to run.

.DESCRIPTION
    fast     Before every commit. Format, the work board, the documentation checks (links, MAP.md, primers), the front-end preset tests, the Python tool tests and
             style, the convention tests, and a secret scan of what is staged. A minute or two.
    full     Everything CI runs, so CI confirms what you already know: the fast checks, then a warnings-as-errors build and every .NET
             test (the SDK's tests pack it and build real samples, so this is the slow part).
    release  Before a release or a big change: the full checks, then mutation testing of Platform.Conventions (tools/mutation.py), the
             dependency report (a known vulnerability fails; a newer version is only listed) and a secret scan of the whole history.

    Every step runs even after one fails, then a summary lists each step as PASSED, FAILED or SKIPPED. A step is SKIPPED, loudly, when
    the tool it needs is not installed or not running; a skipped step is a check you did not do. The script exits 1 if any step failed.
    The list of steps is held to the repository by tools/test_check_script.py (every npm script and path named here exists, and everything CI runs is here).

.EXAMPLE
    ./scripts/check.ps1 fast
    ./scripts/check.ps1 full -Stop
#>
param(
    [Parameter(Position = 0)]
    [ValidateSet('fast', 'full', 'release')]
    [string]$Tier = 'fast',

    # Stop at the first failed step instead of running the rest.
    [switch]$Stop
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Rank = @{ fast = 1; full = 2; release = 3 }

# One step per line: the tier it first runs in (a later tier runs it too), a name, the folder to run in, the command, and what it needs.
$Steps = @(
    @{ Tier = 'fast'; Name = 'Format'; Dir = '.'; Cmd = 'npm run format:check' },
    @{ Tier = 'fast'; Name = 'Work board is valid'; Dir = '.'; Cmd = 'python tools/board.py check' },
    @{ Tier = 'fast'; Name = 'Documentation checks'; Dir = '.'; Cmd = 'python tools/doc_checks/run.py' },
    @{ Tier = 'fast'; Name = 'Front-end preset tests'; Dir = '.'; Cmd = 'npm run test:frontend' },
    @{ Tier = 'fast'; Name = 'Python tool tests'; Dir = '.'; Cmd = 'python -m unittest discover tools' },
    @{ Tier = 'fast'; Name = 'Python lint'; Dir = '.'; Cmd = 'ruff check tools'; Needs = 'ruff' },
    @{ Tier = 'fast'; Name = 'Python format'; Dir = '.'; Cmd = 'ruff format --check tools'; Needs = 'ruff' },
    @{ Tier = 'fast'; Name = 'Convention tests'; Dir = '.'; Cmd = 'dotnet test tests/Platform.Conventions.Tests --nologo -v q' },
    @{ Tier = 'fast'; Name = 'Secret scan, staged changes'; Dir = '.'; Cmd = 'gitleaks git --staged --redact --no-banner'; Needs = 'gitleaks' },

    @{ Tier = 'full'; Name = 'Build, warnings as errors'; Dir = '.'; Cmd = 'dotnet build Platform.sln -c Release -warnaserror --nologo -v q' },
    @{ Tier = 'full'; Name = '.NET tests'; Dir = '.'; Cmd = 'dotnet test Platform.sln -c Release --no-build --nologo' },

    @{ Tier = 'release'; Name = 'Mutation, Platform.Conventions'; Dir = '.'; Cmd = 'python tools/mutation.py run Platform.Conventions' },
    @{ Tier = 'release'; Name = 'Dependency report'; Dir = '.'; Cmd = 'python tools/dependency_report.py' },
    @{ Tier = 'release'; Name = 'Secret scan, whole history'; Dir = '.'; Cmd = 'gitleaks git --redact --no-banner'; Needs = 'gitleaks' }
)

# What the tool a step needs is, how to tell whether it is there, and what to do when it is not.
$Hints = @{
    ruff     = 'install ruff 0.14.0 (pip install ruff==0.14.0; CI uses pipx run --spec ruff==0.14.0)'
    gitleaks = 'install gitleaks (winget install gitleaks, or scoop install gitleaks)'
}

function Test-Needs([string]$Need) {
    switch ($Need) {
        'ruff' { return [bool](Get-Command ruff -ErrorAction SilentlyContinue) }
        'gitleaks' { return [bool](Get-Command gitleaks -ErrorAction SilentlyContinue) }
        default { return $true }
    }
}

$Results = @()
$Chosen = $Steps | Where-Object { $Rank[$_.Tier] -le $Rank[$Tier] }
Write-Host "check.ps1 $Tier : $($Chosen.Count) steps" -ForegroundColor Cyan

foreach ($Step in $Chosen) {
    $Result = [ordered]@{ Step = $Step.Name; Result = ''; Seconds = 0; Note = '' }
    if ($Step.Needs -and -not (Test-Needs $Step.Needs)) {
        $Result.Result = 'SKIPPED'
        $Result.Note = $Hints[$Step.Needs]
        Write-Host "-- $($Step.Name): SKIPPED ($($Result.Note))" -ForegroundColor Yellow
        $Results += [pscustomobject]$Result
        continue
    }

    Write-Host "== $($Step.Name): $($Step.Cmd)" -ForegroundColor Cyan
    $Clock = [System.Diagnostics.Stopwatch]::StartNew()
    Push-Location (Join-Path $Root $Step.Dir)
    try {
        $global:LASTEXITCODE = 0
        Invoke-Expression $Step.Cmd
        $Result.Result = if ($LASTEXITCODE -eq 0) { 'PASSED' } else { 'FAILED' }
    }
    catch {
        Write-Host $_.Exception.Message -ForegroundColor Red
        $Result.Result = 'FAILED'
    }
    finally {
        Pop-Location
        $Clock.Stop()
    }
    $Result.Seconds = [math]::Round($Clock.Elapsed.TotalSeconds, 1)
    $Results += [pscustomobject]$Result
    if ($Result.Result -eq 'FAILED' -and $Stop) { break }
}

Write-Host ''
$Results | Format-Table -AutoSize
$Failed = @($Results | Where-Object { $_.Result -eq 'FAILED' })
$Skipped = @($Results | Where-Object { $_.Result -eq 'SKIPPED' })
if ($Skipped.Count -gt 0) {
    Write-Host "$($Skipped.Count) check(s) SKIPPED: they were not done. Install or start what they need (see Note) and run again." -ForegroundColor Yellow
}
if ($Failed.Count -gt 0) {
    Write-Host "$($Failed.Count) check(s) FAILED." -ForegroundColor Red
    exit 1
}
Write-Host "check.ps1 $Tier : no step failed." -ForegroundColor Green
exit 0

[CmdletBinding()]
param(
    [ValidateSet('Preflight', 'Run', 'Verify')]
    [string]$Action = 'Preflight',

    [string]$SynchronizedLayout = '',

    [string]$ExpectedLayoutSha256 = '43C664A3AC2997E9C8248FD67773EFD2C01A85A02182E5E85192C9B4092D9111',

    [string]$AuthorizationReference = 'TASK-20260829-KUKA-EXACT-C01-NATIVE-LOOP',

    [bool]$AdoptRunningLabVm = $false,

    [string]$Receipt = ''
)

$ErrorActionPreference = 'Stop'
$workspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$labRoot = Join-Path $workspaceRoot '04_kuka_lab'
$assetRoot = 'C:/kuka-validation-lab/vendor/KUKA Simulation'
$cliProject = Join-Path $labRoot 'src\KukaLab.Cli\KukaLab.Cli.csproj'
$cliAssembly = Join-Path $labRoot 'src\KukaLab.Cli\bin\Release\net8.0\kuka-lab.dll'
$defaultLayout = Join-Path $workspaceRoot 'artifacts\kuka-lab\official-workflow\WP11R-20260902-OFFICIAL-FRESH-C01-01\exact-c01-controller-synced.vcmx'
$outputRoot = Join-Path $workspaceRoot 'artifacts\kuka-lab\official-workflow\exact-c01-loop-runs'
$enginePath = 'C:\Program Files\KUKA\KUKA.Sim 4.10\VisualComponents.Engine.exe'
$launcherPath = 'C:\Program Files\KUKA\KUKA.Sim 4.10\VisualComponents.Engine.Launcher.exe'
$bootstrapPath = 'C:\Program Files\KUKA\KUKA.Sim 4.10\Plugin.KukaLab.KukaSim410.Bootstrap.dll'
$compilerPath = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$frameworkReferenceRoot = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
$probeSourcePath = Join-Path $labRoot 'probes\kukasim\officelite-virtual-loop.cs'
$vmxPath = 'C:/kuka-validation-lab/vendor/KUKA Simulation\OfficeLite-Work\8.7.8-build04\runs\exact-c01-goal-20260829\Exact-C01-Goal.vmx'

if ([string]::IsNullOrWhiteSpace($SynchronizedLayout)) {
    $SynchronizedLayout = $defaultLayout
}
$layoutPath = [IO.Path]::GetFullPath($SynchronizedLayout)

function Get-KukaSimProcesses {
    # Win32_Process can retain a short-lived phantom row after the process object
    # has already disappeared.  Ownership checks must use a live process handle,
    # otherwise a non-terminable stale WMI row blocks the next isolated run.
    return @(Get-Process -Name 'VisualComponents.Engine', 'VisualComponents.Engine.Launcher' -ErrorAction SilentlyContinue |
        Where-Object {
            if ($_.HasExited) {
                return $false
            }
            $processPath = $_.Path
            $processPath -and (
                [string]::Equals([IO.Path]::GetFullPath($processPath), $enginePath, [StringComparison]::OrdinalIgnoreCase) -or
                [string]::Equals([IO.Path]::GetFullPath($processPath), $launcherPath, [StringComparison]::OrdinalIgnoreCase)
            )
        } |
        Sort-Object Id)
}

function Assert-File([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label is missing: $Path"
    }
}

function Invoke-ProbeCompilePreflight {
    $compileOutput = Join-Path $outputRoot ('probe-compile-preflight-' + [Guid]::NewGuid().ToString('N') + '.dll')
    $arguments = @(
        '/nologo',
        '/target:library',
        "/out:$compileOutput",
        "/reference:$(Join-Path $frameworkReferenceRoot 'PresentationFramework.dll')",
        "/reference:$(Join-Path $frameworkReferenceRoot 'PresentationCore.dll')",
        "/reference:$(Join-Path $frameworkReferenceRoot 'WindowsBase.dll')",
        "/reference:$(Join-Path $frameworkReferenceRoot 'System.Xaml.dll')",
        "/reference:$(Join-Path (Split-Path -Parent $enginePath) 'Create3D.Shared.dll')",
        "/reference:$(Join-Path (Split-Path -Parent $enginePath) 'Caliburn.Micro.dll')",
        $probeSourcePath
    )
    try {
        $compilerOutput = & $compilerPath @arguments 2>&1
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $compileOutput -PathType Leaf)) {
            throw "KUKA.Sim probe compile preflight failed with exit code ${LASTEXITCODE}: $($compilerOutput -join [Environment]::NewLine)"
        }
    }
    finally {
        if (Test-Path -LiteralPath $compileOutput -PathType Leaf) {
            Remove-Item -LiteralPath $compileOutput
        }
    }
}

function Invoke-Preflight {
    foreach ($required in @(
        @($layoutPath, 'controller-synchronized layout'),
        @($enginePath, 'KUKA.Sim engine'),
        @($launcherPath, 'KUKA.Sim launcher'),
        @($bootstrapPath, 'KUKA.Sim bootstrap'),
        @($compilerPath, '.NET Framework C# compiler'),
        @($probeSourcePath, 'KUKA.Sim OfficeLite loop probe source'),
        @($vmxPath, 'Exact-C01 OfficeLite clone'),
        @($cliProject, 'KUKA Lab CLI project')
    )) {
        Assert-File -Path $required[0] -Label $required[1]
    }

    $layoutHash = (Get-FileHash -LiteralPath $layoutPath -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($layoutHash -ne $ExpectedLayoutSha256.ToUpperInvariant()) {
        throw "Controller-synchronized layout SHA-256 mismatch. Expected $ExpectedLayoutSha256; observed $layoutHash."
    }

    $running = Get-KukaSimProcesses
    [pscustomobject]@{
        action = 'Preflight'
        synchronizedLayout = $layoutPath
        synchronizedLayoutSha256 = $layoutHash
        officeLiteVmx = $vmxPath
        servicePorts = @(49003, 49004)
        serviceWaitSeconds = 180
        servicePollSeconds = 5
        kukaSimProcessCount = $running.Count
        kukaSimProcesses = @($running | ForEach-Object {
            [pscustomobject]@{
                pid = [int]$_.Id
                executablePath = $_.Path
                creationDate = $_.StartTime
            }
        })
        readyToRun = $running.Count -eq 0
    }
}

switch ($Action) {
    'Preflight' {
        Invoke-Preflight | ConvertTo-Json -Depth 6
    }

    'Run' {
        $preflight = Invoke-Preflight
        if (-not $preflight.readyToRun) {
            throw "KUKA.Sim is already running. Close the current UI instance before the owned automated run. PIDs: $($preflight.kukaSimProcesses.pid -join ', ')."
        }

        dotnet build $cliProject -c Release --no-restore
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $cliAssembly -PathType Leaf)) {
            throw "KUKA Lab CLI build failed with exit code $LASTEXITCODE."
        }

        [IO.Directory]::CreateDirectory($outputRoot) | Out-Null
        Invoke-ProbeCompilePreflight
        $attemptId = 'exact-c01-official-loop-' + (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssfff')
        $receiptPath = Join-Path $outputRoot ($attemptId + '.json')
        $arguments = @(
            $cliAssembly,
            'kukasim', 'officelite-loop',
            '--asset-root', $assetRoot,
            '--lab-root', $labRoot,
            '--layout', $layoutPath,
            '--vmx', $vmxPath,
            '--output', $receiptPath,
            '--attempt-id', $attemptId,
            '--timeout-seconds', '240',
            '--observation-seconds', '180',
            # TCP readiness does not prove that RuntimeManager can already answer
            # GetInterpreter. The embedded public-facade warmup is bounded at 180s.
            '--runner-timeout-seconds', '240',
            '--kukasim-timeout-seconds', '600',
            '--allow-gui', 'true',
            '--authorization-ref', $AuthorizationReference,
            '--adopt-running-lab-vm', $AdoptRunningLabVm.ToString().ToLowerInvariant()
        )
        & dotnet @arguments
        $exitCode = $LASTEXITCODE
        if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
            throw "The exact-C01 loop produced no receipt. Exit code: $exitCode."
        }
        [pscustomobject]@{
            action = 'Run'
            exitCode = $exitCode
            receipt = $receiptPath
            receiptSha256 = (Get-FileHash -LiteralPath $receiptPath -Algorithm SHA256).Hash.ToUpperInvariant()
        } | ConvertTo-Json -Depth 4
        exit $exitCode
    }

    'Verify' {
        if ([string]::IsNullOrWhiteSpace($Receipt)) {
            throw 'Verify requires -Receipt.'
        }
        $receiptPath = [IO.Path]::GetFullPath($Receipt)
        Assert-File -Path $receiptPath -Label 'exact-C01 loop receipt'
        Assert-File -Path $cliAssembly -Label 'KUKA Lab CLI assembly'
        & dotnet $cliAssembly 'kukasim' 'officelite-loop-receipt' 'verify' '--receipt' $receiptPath
        exit $LASTEXITCODE
    }
}

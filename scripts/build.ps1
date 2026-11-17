param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$Deploy,
    [switch]$Test,
    [string]$ManagedPath,
    [string]$LocalModDir
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
}

if ($Deploy -and (Get-Process Cities2 -ErrorAction SilentlyContinue)) {
    throw 'Cities2 正在运行。请先正常退出游戏，再执行 -Deploy。'
}

$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'

Push-Location $projectRoot
try {
    $buildArgs = @('build', 'VehicleCapUnlock.csproj', '-c', $Configuration, '--nologo',
        '-p:DeployOnBuild=false')
    if ($ManagedPath) { $buildArgs += ('-p:ManagedPath=' + $ManagedPath) }
    if ($LocalModDir) { $buildArgs += ('-p:LocalModDir=' + $LocalModDir) }
    & $dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { throw "mod 构建失败，退出码 $LASTEXITCODE" }
    if ($Test) {
        & $dotnet run --project 'tests\CeilingMath.Tests\CeilingMath.Tests.csproj' -c $Configuration
        if ($LASTEXITCODE -ne 0) { throw "回归验证失败，退出码 $LASTEXITCODE" }
        & $dotnet run --project 'tests\Safety.Tests\Safety.Tests.csproj' -c $Configuration
        if ($LASTEXITCODE -ne 0) { throw "安全回归验证失败，退出码 $LASTEXITCODE" }
    }
    if ($Deploy) {
        # Check again after build/tests, immediately before touching the deployment.
        if (Get-Process Cities2 -ErrorAction SilentlyContinue) {
            throw 'Cities2 在编译期间启动了。请正常退出后重新执行 -Deploy。'
        }
        if (-not $LocalModDir) {
            $LocalModDir = Join-Path $env:USERPROFILE 'AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\VehicleCapUnlock'
        }
        $dll = Join-Path $projectRoot "bin\$Configuration\VehicleCapUnlock.dll"
        New-Item -ItemType Directory -Path $LocalModDir -Force | Out-Null
        Copy-Item -LiteralPath $dll -Destination $LocalModDir -Force
        $deployed = Join-Path $LocalModDir 'VehicleCapUnlock.dll'
        if ((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $deployed -Algorithm SHA256).Hash) {
            throw '部署后 DLL 哈希不一致。'
        }
        Write-Host "已部署并校验：$deployed"
    }
}
finally { Pop-Location }

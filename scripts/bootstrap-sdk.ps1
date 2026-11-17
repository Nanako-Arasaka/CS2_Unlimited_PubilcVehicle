$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$toolsRoot = Join-Path $projectRoot '.tools'
$sdkRoot = Join-Path $toolsRoot 'dotnet'
$version = '8.0.428'
$expectedHash = 'cce1dc5cfb2c5b73ddd9d3658f6009deb85be3a58dbec44a6dfe3910bb32b044ad2fc6bb28c77aded70c73270a204d207fe790dcb82ffca6c7b42c1335b51c21'
$archive = Join-Path $toolsRoot 'dotnet-sdk-win-x64.zip'
$dotnet = Join-Path $sdkRoot 'dotnet.exe'
if (Test-Path -LiteralPath $dotnet) {
    & $dotnet --list-sdks
    if ($LASTEXITCODE -ne 0) { throw '现有本地 SDK 无法运行' }
    return
}
New-Item -ItemType Directory -Path $toolsRoot -Force | Out-Null
if (-not (Test-Path -LiteralPath $archive)) {
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest -Uri "https://dotnetcli.blob.core.windows.net/dotnet/Sdk/$version/dotnet-sdk-$version-win-x64.zip" -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $expectedHash) {
    throw 'SDK 压缩包 SHA512 与微软发布元数据不符，未解压。请检查或移走不完整的压缩包后重试。'
}
Expand-Archive -LiteralPath $archive -DestinationPath $sdkRoot -Force
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_HOME = Join-Path $toolsRoot 'cli-home'
& $dotnet --info
if ($LASTEXITCODE -ne 0) { throw 'SDK 安装后验证失败' }

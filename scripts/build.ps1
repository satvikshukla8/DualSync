param([switch]$Package, [string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    & $Dotnet restore DualSync.slnx --locked-mode --disable-parallel -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
    & $Dotnet build DualSync.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & $Dotnet 'tests/DualSync.Tests/bin/Release/net10.0/DualSync.Tests.dll'
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    if ($Package) {
        $version = ([xml](Get-Content -Raw 'Directory.Build.props')).Project.PropertyGroup.Version
        $outputPath = Join-Path $projectRoot "artifacts/portable-$version-win-x64"
        & $Dotnet publish src/DualSync.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:RestoreLockedMode=true -p:UseSharedCompilation=false -m:1 -o $outputPath
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
        Copy-Item -LiteralPath 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'README.md' -Destination $outputPath
        $licensePath = Join-Path $outputPath 'licenses'
        New-Item -ItemType Directory -Path $licensePath -Force | Out-Null
        $packageCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages' }
        $dependencyInfo = Get-Content -Raw (Join-Path $outputPath 'DualSync.deps.json') | ConvertFrom-Json
        foreach ($libraryName in $dependencyInfo.libraries.PSObject.Properties.Name) {
            $isRuntime = $libraryName.StartsWith('runtimepack.')
            if (-not $isRuntime -and -not $libraryName.StartsWith('System.Numerics.Tensors/')) { continue }
            $packageName = if ($isRuntime) { $libraryName.Substring('runtimepack.'.Length) } else { $libraryName }
            $parts = $packageName.Split('/')
            $packagePath = Join-Path $packageCache ($parts[0].ToLowerInvariant() + '/' + $parts[1])
            $noticeFiles = @(Get-ChildItem -LiteralPath $packagePath -File | Where-Object { $_.Name -match '^(LICENSE|THIRD.PARTY.NOTICES)' })
            if ($noticeFiles.Count -eq 0) { throw "Missing license files for $packageName" }
            foreach ($noticeFile in $noticeFiles) {
                Copy-Item -LiteralPath $noticeFile.FullName -Destination (Join-Path $licensePath ($parts[0] + '-' + $parts[1] + '-' + $noticeFile.Name))
            }
        }
        $docsPath = Join-Path $outputPath 'docs'
        New-Item -ItemType Directory -Path $docsPath -Force | Out-Null
        Copy-Item -Path 'docs/*.md' -Destination $docsPath
        Get-ChildItem -LiteralPath $outputPath -File -Filter '*.pdb' | Remove-Item
        $archive = Join-Path $projectRoot "artifacts/DualSync-$version-win-x64.zip"
        Compress-Archive -Path (Join-Path $outputPath '*') -DestinationPath $archive -Force
        Get-FileHash -Algorithm SHA256 -LiteralPath $archive | Format-List
    }
} finally { Pop-Location }

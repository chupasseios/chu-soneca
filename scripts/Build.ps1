param([string]$ExpectedVersion = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location -LiteralPath $root
$version = (Get-Content -LiteralPath 'VERSION' -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must be X.Y.Z' }
if ($ExpectedVersion -and $ExpectedVersion -ne $version) { throw "Tag $ExpectedVersion differs from VERSION $version" }
function Run([string]$exe, [string[]]$arguments) {
 & $exe @arguments
 if ($LASTEXITCODE -ne 0) { throw "$exe failed with exit code $LASTEXITCODE" }
}
function Download([string]$url,[string]$file,[string]$sha) {
 if (-not (Test-Path -LiteralPath $file)) { Invoke-WebRequest -Uri $url -OutFile $file }
 if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $sha) { throw "SHA256 mismatch: $file" }
}
$build = Join-Path $root '.build'
New-Item -ItemType Directory -Path "$build/downloads","$build/sdk" -Force | Out-Null
$deps = Get-Content -LiteralPath 'scripts/dependencies.json' -Raw | ConvertFrom-Json
Download $deps.vpilot.url "$build/downloads/vpilot.exe" $deps.vpilot.sha256
Download $deps.node.url "$build/downloads/node.zip" $deps.node.sha256
$seven = Join-Path $env:ProgramFiles '7-Zip/7z.exe'
if (-not (Test-Path -LiteralPath $seven)) { $seven = (Get-Command 7z -ErrorAction Stop).Source }
Run $seven @('x',"$build/downloads/vpilot.exe","-o$build/sdk",'RossCarlson.Vatsim.Vpilot.Plugins.dll','Microsoft.FlightSimulator.SimConnect.dll','-y')
Expand-Archive -LiteralPath "$build/downloads/node.zip" -DestinationPath "$build/node" -Force
$node = "$build/node/node-v24.12.0-win-x64/node.exe"
$npm = "$build/node/node-v24.12.0-win-x64/node_modules/npm/bin/npm-cli.js"
Run $node @($npm,'ci','--prefix','engine')
Run $node @($npm,'run','build','--prefix','engine')
Run $node @($npm,'test','--prefix','engine')
Run $node @('tests/boundary-fixtures.mjs')
Run 'dotnet' @('run','--project','tests/StateTests.csproj','-c','Release')
foreach ($project in @('tests/IdentityTests.csproj','app/FlightWatch.csproj','app/Demo.csproj','app/TelemetryHost.csproj','app/Launcher.csproj','app/XpilotCompanion.csproj','xpilot/ChuSoneca.Xpilot.csproj','tests/XpilotTests.csproj')) {
 Run 'dotnet' @('build',$project,'-c','Release','--nologo',"-p:VpilotSdkDir=$build/sdk")
}
Run "$root/tests/bin/Release/net48/IdentityTests.exe" @()
Run 'dotnet' @('tests/bin/Release/net10.0-windows/XpilotTests.dll')
# Fresh staging: copy only tracked sources and explicitly selected artifacts.
$stage = Join-Path $build ('stage-' + [Guid]::NewGuid().ToString('N'))
$payload = Join-Path $stage 'setup/payload'
New-Item -ItemType Directory -Path "$payload/plugin/FlightWatch","$payload/xpilot/ChuSoneca.Xpilot/FlightWatch","$payload/app/source","$payload/app/licenses" -Force | Out-Null
$tracked = & git ls-files
if ($LASTEXITCODE -ne 0 -or -not $tracked) { throw 'Build requires tracked source files' }
foreach ($relative in $tracked) {
 $source = Join-Path $root $relative
 $dest = Join-Path $stage $relative
 New-Item -ItemType Directory -Path (Split-Path $dest) -Force | Out-Null
 Copy-Item -LiteralPath $source -Destination $dest -Force
 $publishedSource = Join-Path "$payload/app/source" $relative
 New-Item -ItemType Directory -Path (Split-Path $publishedSource) -Force | Out-Null
 Copy-Item -LiteralPath $source -Destination $publishedSource -Force
}
Copy-Item -LiteralPath 'app/bin/Release/net48/FlightWatch.dll' -Destination "$payload/plugin"
Copy-Item -LiteralPath 'engine/dist/coverage.cjs' -Destination "$payload/plugin/FlightWatch"
Copy-Item -LiteralPath $node -Destination "$payload/plugin/FlightWatch/node.exe"
Copy-Item -LiteralPath "$build/node/node-v24.12.0-win-x64/LICENSE" -Destination "$payload/plugin/FlightWatch/LICENSE-Node.txt"
foreach ($name in @('FlightWatch.Telemetry.exe','FlightWatch.Telemetry.exe.config')) { Copy-Item -LiteralPath "app/bin/Release/net48/$name" -Destination "$payload/plugin/FlightWatch" }
foreach ($name in @('FlightWatch.Demo.exe','FlightWatch.Demo.exe.config','Chu-Soneca.exe','Chu-Soneca.exe.config')) { Copy-Item -LiteralPath "app/bin/Release/net48/$name" -Destination "$payload/app" }
Copy-Item -LiteralPath 'xpilot/bin/Release/net10.0-windows/ChuSoneca.Xpilot.dll' -Destination "$payload/xpilot/ChuSoneca.Xpilot"
foreach ($name in @('ChuSoneca.XpilotUI.exe','ChuSoneca.XpilotUI.exe.config')) { Copy-Item -LiteralPath "app/bin/Release/net48/$name" -Destination "$payload/xpilot/ChuSoneca.Xpilot" }
Copy-Item -LiteralPath 'engine/dist/coverage.cjs' -Destination "$payload/xpilot/ChuSoneca.Xpilot/FlightWatch"
Copy-Item -LiteralPath $node -Destination "$payload/xpilot/ChuSoneca.Xpilot/FlightWatch/node.exe"
Copy-Item -LiteralPath 'xpilot/sdk/LICENSE' -Destination "$payload/app/licenses/LICENSE-xPilot-SDK.txt"
Copy-Item -Path 'packaging/licenses/*'  -Destination "$payload/app/licenses"
Copy-Item -LiteralPath 'README.md','ATRIBUICOES.md','packaging/CHANGELOG.txt' -Destination "$payload/app"
Run 'dotnet' @('build',"$stage/setup/Uninstall.csproj",'-c','Release','--nologo')
Copy-Item -LiteralPath "$stage/setup/bin/Release/net48/FlightWatch-Uninstall.exe" -Destination "$payload/app"
& "$stage/setup/Pack.ps1"
Run 'dotnet' @('build',"$stage/setup/Setup.csproj",'-c','Release','--nologo')
New-Item -ItemType Directory -Path 'artifacts' -Force | Out-Null
$installer = Join-Path $root "artifacts/Chu-Soneca-Setup-$version.exe"
Copy-Item -LiteralPath "$stage/setup/bin/Release/net48/FlightWatch-Setup.exe" -Destination $installer -Force
$report = Join-Path $root "artifacts/Chu-Soneca-Validacao-$version.txt"
$p = Start-Process -FilePath $installer -ArgumentList @('--self-test', ('"'+$report+'"')) -WindowStyle Hidden -PassThru
if (-not $p.WaitForExit(120000)) { $p.Kill(); throw 'Installer self-test timed out' }
if ($p.ExitCode -ne 0) { throw "Installer self-test failed: $report" }
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path $installer -Leaf)" | Set-Content -LiteralPath "artifacts/Chu-Soneca-Setup-$version.sha256" -Encoding ascii
Get-Content -LiteralPath $report
Write-Host "Build verified: $installer"

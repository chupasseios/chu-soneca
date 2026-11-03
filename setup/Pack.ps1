$ErrorActionPreference = 'Stop'
$payloadRoot = Join-Path $PSScriptRoot 'payload'
$hashes = @{}
Get-ChildItem -LiteralPath $payloadRoot -Recurse -File | ForEach-Object {
    $key = $_.FullName.Substring($payloadRoot.Length+1).Replace('\','/')
    $hashes[$key] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
}
$hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'hashes.json') -Encoding utf8
Compress-Archive -LiteralPath (Join-Path $payloadRoot 'plugin'),(Join-Path $payloadRoot 'xpilot'),(Join-Path $payloadRoot 'app') -DestinationPath (Join-Path $PSScriptRoot 'payload.zip') -Force

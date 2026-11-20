param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '1.1.0')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$dist = Join-Path $projectRoot 'dist'
$archiveName = "GanSo-v$Version-windows-x64.zip"
$archivePath = Join-Path $dist $archiveName
if (Test-Path -LiteralPath $archivePath) { throw "Package already exists: $archivePath" }
if (!(Test-Path -LiteralPath (Join-Path $projectRoot 'GanSo.exe'))) { throw 'Build GanSo before packaging.' }
$staging = Join-Path $dist ('.pack-' + [Guid]::NewGuid().ToString('N'))
$portable = Join-Path $staging 'GanSo'
New-Item -ItemType Directory -Force -Path $portable | Out-Null
$files = @('GanSo.exe','GanSo.exe.config','README.md','COPYRIGHT.md','LICENSE','Send-GanSoTask.ps1','tasks.example.json')
foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $projectRoot $file) -Destination $portable }
New-Item -ItemType Directory -Force -Path (Join-Path $portable 'assets'),(Join-Path $portable 'docs') | Out-Null
foreach ($folder in @('cutouts','sounds')) { Copy-Item -LiteralPath (Join-Path $projectRoot ('assets\' + $folder)) -Destination (Join-Path $portable 'assets') -Recurse }
Copy-Item -LiteralPath (Join-Path $projectRoot 'assets\ganso.ico') -Destination (Join-Path $portable 'assets')
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\random-cues-video.md') -Destination (Join-Path $portable 'docs')
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($staging,$archivePath,[System.IO.Compression.CompressionLevel]::Optimal,$false)
$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText((Join-Path $dist 'SHA256SUMS.txt'), "$hash  $archiveName`n", [System.Text.UTF8Encoding]::new($false))
Write-Output $archivePath
Write-Output "SHA256: $hash"

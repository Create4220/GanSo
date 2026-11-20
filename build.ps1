$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$refs = @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Xaml.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$refs += @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path (Join-Path $framework 'WPF') $_) }
$common = @('/nologo','/optimize+','/platform:anycpu','/codepage:65001',('/resource:'+(Join-Path $projectRoot 'src\Main.xaml')+',GanSo.Main.xaml'),('/win32icon:'+(Join-Path $projectRoot 'assets\ganso.ico'))) + $refs
if (Get-Process GanSo -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $projectRoot 'GanSo.exe') }) { throw '请先关闭此目录的 GanSo，再重新构建。' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
& $compiler @common /target:winexe ('/out:'+(Join-Path $projectRoot 'GanSo.exe')) @sources
if ($LASTEXITCODE -ne 0) { throw 'GanSo 编译失败。' }
& $compiler @common /target:exe /define:TEST_RUNNER ('/out:'+(Join-Path $projectRoot 'GanSo.Tests.exe')) @sources (Join-Path $projectRoot 'tests\Tests.cs')
if ($LASTEXITCODE -ne 0) { throw '测试程序编译失败。' }
Write-Output 'GanSo.exe 构建完成。'

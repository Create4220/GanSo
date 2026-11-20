param(
 [string]$Task,
 [ValidateRange(0,999)][int]$Minutes = 25,
 [ValidateRange(0,59)][int]$Seconds = 0,
 [switch]$Start,
 [string]$ImportFile
)
$ErrorActionPreference = 'Stop'
$executable = Join-Path $PSScriptRoot 'GanSo.exe'
if ($ImportFile) { & $executable --import-tasks $ImportFile }
elseif ($Task) {
 $arguments = @('--task', $Task, '--minutes', $Minutes.ToString(), '--seconds', $Seconds.ToString())
 if ($Start) { $arguments += '--start' }
 & $executable @arguments
}
else { & $executable --show }

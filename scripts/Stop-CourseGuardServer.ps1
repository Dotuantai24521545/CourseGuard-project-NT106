$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$pidFile=Join-Path $root '.local/server.pid'
if(!(Test-Path -LiteralPath $pidFile)){Write-Output 'Không có server local do script này quản lý.';return}
$serverProcessId=0
if(![int]::TryParse((Get-Content -LiteralPath $pidFile -Raw).Trim(),[ref]$serverProcessId)){throw 'File server.pid không hợp lệ.'}
$process=Get-Process -Id $serverProcessId -ErrorAction SilentlyContinue
$expected=[IO.Path]::GetFullPath((Join-Path $root '.local/server/CourseGuard.Server.exe'))
if($process){
    if(![string]::Equals($process.Path,$expected,[StringComparison]::OrdinalIgnoreCase)){throw 'PID hiện thuộc tiến trình khác; không dừng.'}
    Stop-Process -Id $serverProcessId
}
Remove-Item -LiteralPath $pidFile
Write-Output 'Đã dừng server local của repo này.'

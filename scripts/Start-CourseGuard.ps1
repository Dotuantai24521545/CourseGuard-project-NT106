param(
    [string]$ApiUrl='http://127.0.0.1:5080',
    [switch]$ClientOnly,
    [switch]$ServerOnly,
    [switch]$BuildOnly
)
$ErrorActionPreference='Stop'
if($ClientOnly -and $ServerOnly){throw 'Chọn ClientOnly hoặc ServerOnly.'}
$root=Split-Path $PSScriptRoot -Parent
$uri=[Uri]$ApiUrl
if($uri.Scheme -ne 'https' -and !($uri.Scheme -eq 'http' -and $uri.IsLoopback)){throw 'API phải dùng HTTPS, trừ localhost.'}
$local=Join-Path $root '.local'
New-Item -ItemType Directory -Path $local -Force|Out-Null
if(!$ClientOnly){
    $serverEnv=Join-Path $root 'CourseGuard/CourseGuard.Server/.env'
    if(!(Test-Path -LiteralPath $serverEnv)){throw 'Tạo CourseGuard/CourseGuard.Server/.env theo file mẫu trước khi chạy server.'}
    if($BuildOnly -or !(Test-Path (Join-Path $local 'server/CourseGuard.Server.exe'))){
        & dotnet publish (Join-Path $root 'CourseGuard/CourseGuard.Server/CourseGuard.Server.csproj') -c Release --nologo -v quiet -o (Join-Path $local 'server')
        if($LASTEXITCODE -ne 0){throw 'Build server thất bại.'}
    }
    Copy-Item -LiteralPath $serverEnv -Destination (Join-Path $local 'server/.env')
}
if(!$ServerOnly){
    if($BuildOnly -or !(Test-Path (Join-Path $local 'client/CourseGuard.exe'))){
        & dotnet publish (Join-Path $root 'CourseGuard/CourseGuard/CourseGuard.csproj') -c Release --nologo -v quiet -o (Join-Path $local 'client')
        if($LASTEXITCODE -ne 0){throw 'Build client thất bại.'}
    }
    Set-Content -LiteralPath (Join-Path $local 'client/.env') -Value ('COURSEGUARD_API_URL='+$ApiUrl.TrimEnd('/'))
    $forbidden='(?m)^(COURSEGUARD_DB_CONNECTION|SUPABASE_DB_CONNECTION|CONNECTION_STRING|SMTP_PASS|SMTP_USER|OPENROUTER_API_KEY)='
    if((Get-Content (Join-Path $local 'client/.env') -Raw) -match $forbidden){throw 'Gói client chứa cấu hình chỉ dành cho server.'}
}
if($BuildOnly){Write-Output 'Đã build vào .local/server và .local/client; cấu hình hai bên tách riêng.';return}
if(!$ClientOnly){
    if(!$uri.IsLoopback){throw 'Script khởi chạy local chỉ dùng địa chỉ loopback. Triển khai server từ xa theo hướng dẫn HTTPS.'}
    $healthy=$false
    try{$healthy=(Invoke-RestMethod ($ApiUrl.TrimEnd('/')+'/health') -TimeoutSec 2).service -eq 'CourseGuard.Server'}catch{}
    if(!$healthy){
        $oldEnvironment=$env:ASPNETCORE_ENVIRONMENT;$oldUrls=$env:ASPNETCORE_URLS
        try{
            $env:ASPNETCORE_ENVIRONMENT='Development';$env:ASPNETCORE_URLS=$ApiUrl
            $process=Start-Process -FilePath (Join-Path $local 'server/CourseGuard.Server.exe') -WorkingDirectory (Join-Path $local 'server') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $local 'server-out.log') -RedirectStandardError (Join-Path $local 'server-error.log')
            $process.Id|Set-Content (Join-Path $local 'server.pid')
        }finally{$env:ASPNETCORE_ENVIRONMENT=$oldEnvironment;$env:ASPNETCORE_URLS=$oldUrls}
        for($i=0;$i -lt 40;$i++){
            try{if((Invoke-RestMethod ($ApiUrl.TrimEnd('/')+'/health') -TimeoutSec 1).service -eq 'CourseGuard.Server'){$healthy=$true;break}}catch{}
            Start-Sleep -Milliseconds 250
        }
        if(!$healthy){throw 'Server chưa sẵn sàng. Xem .local/server-error.log.'}
    }
}
if(!$ServerOnly){Start-Process -FilePath (Join-Path $local 'client/CourseGuard.exe') -WorkingDirectory (Join-Path $local 'client')|Out-Null}
Write-Output ('CourseGuard API: '+$ApiUrl)

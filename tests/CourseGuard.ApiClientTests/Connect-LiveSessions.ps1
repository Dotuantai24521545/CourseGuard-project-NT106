param([string]$ApiBase='http://127.0.0.1:5087',[string]$SessionDirectory='.local/api-test-sessions')
$ErrorActionPreference='Stop'
$uri=[Uri]$ApiBase
if($uri.Scheme -ne 'https' -and !$uri.IsLoopback){throw 'Dùng HTTPS khi kiểm thử từ xa.'}
New-Item -ItemType Directory -Path $SessionDirectory -Force|Out-Null
foreach($name in @('admin','thnghi','student')){
    $secret=Read-Host ('Mật khẩu '+$name) -AsSecureString
    $credential=[PSCredential]::new($name,$secret)
    $body=@{username=$name;password=$credential.GetNetworkCredential().Password;deviceName='CourseGuard-API-E2E'}|ConvertTo-Json
    $challenge=Invoke-RestMethod ($ApiBase+'/api/auth/login') -Method Post -ContentType application/json -Headers @{'X-CourseGuard-Client'='desktop'} -Body $body
    $body=$null;$credential=$null
    $otp=Read-Host ('OTP '+$name)
    $body=@{challengeId=$challenge.challengeId;otp=$otp}|ConvertTo-Json
    $null=Invoke-RestMethod ($ApiBase+'/api/auth/verify') -Method Post -ContentType application/json -Headers @{'X-CourseGuard-Client'='desktop'} -Body $body -SessionVariable session
    $body=$null;$otp=$null
    $suffix=if($name -eq 'admin'){''}else{'-'+$name}
    $session.Cookies.GetCookies($uri)|ForEach-Object {@{Name=$_.Name;Value=$_.Value;Path=$_.Path}}|ConvertTo-Json|Set-Content (Join-Path $SessionDirectory ('server-e2e-cookies'+$suffix+'.json'))
    Write-Output ('Đã tạo phiên '+$name)
}

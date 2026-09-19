param([string]$Email)
$ErrorActionPreference = 'Stop'
if (-not $Email) { $Email = Read-Host 'Gmail sender address' }
if ($Email -notmatch '^[^\s@]+@[^\s@]+\.[^\s@]+$') { throw 'Enter a valid sender email address.' }
$project = Join-Path $PSScriptRoot '..\CoLearnX.Server\CoLearnX.Server.csproj'
$secret = Read-Host 'Google app password (hidden; not your Google account password)' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
try {
    $settings = @{
        'PasswordReset:DeliveryMode' = 'Smtp'
        'PasswordReset:SmtpHost' = 'smtp.gmail.com'
        'PasswordReset:SmtpPort' = '587'
        'PasswordReset:SmtpUsername' = $Email
        'PasswordReset:FromAddress' = $Email
        'PasswordReset:SmtpPassword' = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer).Replace(' ', '')
        'PasswordReset:ClientBaseUrl' = 'https://localhost:55128'
    }
    $settings | ConvertTo-Json -Compress | dotnet user-secrets set --project $project
    if ($LASTEXITCODE -ne 0) { throw 'Could not save mail configuration.' }
    Write-Host 'Gmail SMTP configuration saved in local user-secrets. Restart the local server.'
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    if ($settings) { $settings.Clear() }
    $secret.Dispose()
}

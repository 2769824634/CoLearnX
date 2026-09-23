param(
    [Parameter(Mandatory)] [string] $AppName,
    [Parameter(Mandatory)] [string] $ResourceGroup,
    [string] $Email,
    [string] $PublicUrl
)
$ErrorActionPreference = 'Stop'
if (-not $Email) { $Email = Read-Host 'Gmail sender address' }
if ($Email -notmatch '^[^\s@]+@[^\s@]+\.[^\s@]+$') { throw 'Enter a valid sender email address.' }
if (-not (Get-Command az -ErrorAction SilentlyContinue)) { throw 'Azure CLI (az) is required.' }

$secret = Read-Host 'Google app password (hidden; not your Google account password)' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
try {
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer).Replace(' ', '')
    $settings = @(
        "PasswordReset__DeliveryMode=Smtp",
        "PasswordReset__SmtpHost=smtp.gmail.com",
        "PasswordReset__SmtpPort=587",
        "PasswordReset__SmtpUsername=$Email",
        "PasswordReset__FromAddress=$Email",
        "PasswordReset__SmtpPassword=$password"
    )
    if ($PublicUrl) { $settings += "PasswordReset__ClientBaseUrl=$PublicUrl" }
    az webapp config appsettings set --name $AppName --resource-group $ResourceGroup --settings @settings | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not save App Service application settings.' }
    Write-Host 'Password reset SMTP settings saved on the App Service. Restart the web app if it does not recycle automatically.'
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    if ($password) { $password = $null }
    $secret.Dispose()
}

param(
    [Parameter(Mandatory = $true)]
    [string]$Folder
)

# === CONFIGURATION ===
$companyPrefix = 'OneWare'
$timestampUrl  = 'http://timestamp.digicert.com'

$isCi = [bool]$env:GITHUB_ACTIONS

# When launched from pwsh 7, Windows PowerShell inherits a PSModulePath pointing at PS7 modules and fails to
# autoload Microsoft.PowerShell.Security. Load the module that belongs to the running host explicitly.
try {
    Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Security\Microsoft.PowerShell.Security.psd1') -ErrorAction Stop
}
catch {
    Write-Host ("[ERROR] Could not load Microsoft.PowerShell.Security: {0}" -f $_) -ForegroundColor Red
    exit 1
}

# Check for signtool
$signTool = Get-Command signtool -ErrorAction SilentlyContinue
if (-not $signTool) {
    if ($isCi) {
        Write-Host "[ERROR] 'signtool' not found on CI. Refusing to ship unsigned binaries." -ForegroundColor Red
        exit 1
    }
    Write-Host "[WARN] 'signtool' not found. Listing files that would be signed..." -ForegroundColor Yellow
}

Write-Host "[INFO] Scanning for binaries in $Folder"

# Collect .exe and .dll files
$files = Get-ChildItem -Path $Folder -Recurse -File | Where-Object { $_.Extension -in '.exe', '.dll' }
if (-not $files) {
    Write-Host "[INFO] No binaries found."
    exit 0
}

$errors = 0

foreach ($f in $files) {
    # Check signature
    try {
        $sig = Get-AuthenticodeSignature -FilePath $f.FullName
    }
    catch {
        Write-Host ("[ERROR] Could not check signature for {0}: {1}" -f $f.FullName, $_) -ForegroundColor Red
        $errors++
        continue
    }

    # Skip already signed
    if ($sig -and $sig.SignerCertificate) {
        Write-Host ("[SKIP] {0} - signed by {1}" -f $f.FullName, $sig.SignerCertificate.Subject)
        continue
    }

    Write-Host ("[NEEDS SIGNING] {0}" -f $f.FullName)

    # Only perform signing if signtool exists
    if ($signTool) {
        Write-Host ("[SIGN] {0}" -f $f.FullName)
        & signtool sign /tr $timestampUrl /td sha256 /fd sha256 /a "$($f.FullName)"
        if ($LASTEXITCODE -ne 0) {
            Write-Host ("[ERROR] signtool failed for {0} (exit {1})" -f $f.FullName, $LASTEXITCODE) -ForegroundColor Red
            $errors++
        }
    }
}

if ($errors -gt 0) {
    Write-Host ("[ERROR] Completed with {0} signing errors." -f $errors) -ForegroundColor Red
    # Local builds without a certificate should still produce an installer; CI must not ship unsigned binaries.
    if ($isCi) { exit 1 }
} elseif (-not $signTool) {
    Write-Host "[INFO] Skipped actual signing because 'signtool' is missing. Displayed all unsigned company binaries above." -ForegroundColor Yellow
} else {
    Write-Host "[INFO] Completed successfully. All unsigned company binaries were signed or skipped."
}

exit 0

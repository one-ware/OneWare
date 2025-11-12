param(
    [Parameter(Mandatory = $true)]
    [string]$Folder
)

# Check for signtool first
$signTool = Get-Command signtool -ErrorAction SilentlyContinue
if (-not $signTool) {
    Write-Host "[WARN] 'signtool' is not available on PATH. Skipping signing. Install the Windows SDK or add signtool to PATH." -ForegroundColor Yellow
    exit 0
}

Write-Host "[INFO] Scanning for binaries in $Folder"

# Collect .exe and .dll files reliably
$files = Get-ChildItem -Path $Folder -Recurse -File | Where-Object { $_.Extension -in '.exe', '.dll' }
$errors = 0

foreach ($f in $files) {
    try {
        $sig = Get-AuthenticodeSignature -FilePath $f.FullName
    }
    catch {
        Write-Host ("[WARN] Could not check signature for {0}: {1}" -f $f.FullName, $_)
        continue
    }

    # Skip if already signed
    if ($sig -and $sig.SignerCertificate) {
        Write-Host ("[SKIP] {0} - signed by {1}" -f $f.FullName, $sig.SignerCertificate.Subject)
        continue
    }

    Write-Host ("[SIGN] {0}" -f $f.FullName)
    & signtool sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 /a "$($f.FullName)"
    if ($LASTEXITCODE -ne 0) {
        Write-Host ("[ERROR] signtool failed for {0} (exit {1})" -f $f.FullName, $LASTEXITCODE) -ForegroundColor Red
        $errors++
    }
}

if ($errors -gt 0) {
    Write-Host ("[WARN] Completed with {0} signing errors." -f $errors) -ForegroundColor Yellow
} else {
    Write-Host "[INFO] Completed successfully, all unsigned files were signed or skipped."
}

exit 0   # Always return 0 so MSBuild doesn't fail

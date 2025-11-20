param(
    [Parameter(Mandatory = $true)]
    [string]$Folder
)

# === CONFIGURATION ===
$companyPrefix = 'OneWare'
$timestampUrl  = 'http://timestamp.digicert.com'

# Check for signtool
$signTool = Get-Command signtool -ErrorAction SilentlyContinue
if (-not $signTool) {
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

# Common runtime/framework prefixes to skip
$frameworkPatterns = @(
    '^System\.', '^Microsoft\.', '^WindowsBase$', '^Presentation', '^runtime\.', '^clrcompression', '^mscordaccore'
)

foreach ($f in $files) {
    $name = $f.BaseName

    # Skip framework or runtime assemblies
    if ($frameworkPatterns | Where-Object { $name -match $_ }) {
        Write-Host ("[SKIP] {0} - framework/runtime assembly" -f $f.FullName)
        continue
    }

    # Skip if not under your namespace or not your executable
    if ($name -notmatch "^$companyPrefix" -and $f.Extension -eq '.dll') {
        Write-Host ("[SKIP] {0} - not a {1} binary" -f $f.FullName, $companyPrefix)
        continue
    }

    # Check signature
    try {
        $sig = Get-AuthenticodeSignature -FilePath $f.FullName
    }
    catch {
        Write-Host ("[WARN] Could not check signature for {0}: {1}" -f $f.FullName, $_)
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

if (-not $signTool) {
    Write-Host "[INFO] Skipped actual signing because 'signtool' is missing. Displayed all unsigned company binaries above." -ForegroundColor Yellow
} elseif ($errors -gt 0) {
    Write-Host ("[WARN] Completed with {0} signing errors." -f $errors) -ForegroundColor Yellow
} else {
    Write-Host "[INFO] Completed successfully. All unsigned company binaries were signed or skipped."
}

exit 0   # Always return 0 so MSBuild doesn't fail

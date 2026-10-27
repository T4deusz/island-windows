$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$source = $PSScriptRoot
$release = Join-Path $taskRoot 'Island-Windows-1.3'
$packageRoot = Join-Path $PSScriptRoot 'obj/msix-1.3'
$installer = Join-Path $taskRoot 'Island-Installer-1.3'
New-Item -ItemType Directory -Force "$source/Assets", $packageRoot, $installer | Out-Null
Add-Type -AssemblyName System.Drawing
foreach ($size in @(44, 50, 150, 256)) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = 'AntiAlias'
    $graphics.Clear([System.Drawing.Color]::FromArgb(25,25,29))
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(131,226,198), [single]($size * .055))
    $points = [System.Drawing.PointF[]]@([System.Drawing.PointF]::new($size*.5,$size*.22),[System.Drawing.PointF]::new($size*.76,$size*.5),[System.Drawing.PointF]::new($size*.5,$size*.78),[System.Drawing.PointF]::new($size*.24,$size*.5))
    $graphics.DrawPolygon($pen,$points)
    $graphics.FillEllipse([System.Drawing.Brushes]::White, [single]($size*.445), [single]($size*.445), [single]($size*.11), [single]($size*.11))
    $name = switch ($size) {44 {'Logo44.png'} 50 {'StoreLogo.png'} 150 {'Logo150.png'} 256 {'Icon.png'}}
    $bitmap.Save((Join-Path "$source/Assets" $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $pen.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
# The ICO embeds a PNG, which Windows supports for modern high-resolution application icons.
$png = [IO.File]::ReadAllBytes("$source/Assets/Icon.png")
$stream = [IO.File]::Create("$source/Assets/Island.ico")
$writer = [IO.BinaryWriter]::new($stream)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$png.Length); $writer.Write([uint32]22); $writer.Write($png); $writer.Dispose()
Write-Output 'Application assets generated.'
Copy-Item -Path "$release/*" -Destination $packageRoot -Recurse -Force
# Keep machine-specific diagnostics and preview renders outside the installable package.
foreach ($relative in @('diagnostics.txt', 'self-test.txt', 'api-self-test.txt', 'previews')) {
    $target = Join-Path $packageRoot $relative
    if ((Test-Path $target) -and ([IO.Path]::GetFullPath($target).StartsWith([IO.Path]::GetFullPath($packageRoot) + [IO.Path]::DirectorySeparatorChar))) { Remove-Item -LiteralPath $target -Force -Recurse }
}
Copy-Item "$source/Package.appxmanifest" "$packageRoot/AppxManifest.xml" -Force
Copy-Item "$source/Assets" $packageRoot -Recurse -Force
$kit = 'C:/Program Files (x86)/Windows Kits/10/bin/10.0.26100.0/x64'
& "$kit/makeappx.exe" pack /d $packageRoot /p "$installer/Island.msix" /o
if ($LASTEXITCODE -ne 0) { throw 'MSIX validation failed.' }
$rsa = [Security.Cryptography.RSA]::Create(2048)
$request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=Island Local Development', $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
$eku = [Security.Cryptography.OidCollection]::new()
[void]$eku.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
$request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($eku, $true))
$request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new([Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature, $true))
$request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true))
$cert = $request.CreateSelfSigned([DateTimeOffset]::Now.AddDays(-1), [DateTimeOffset]::Now.AddYears(1))
$password = [Guid]::NewGuid().ToString()
[IO.File]::WriteAllBytes("$PSScriptRoot/obj/signing.pfx", $cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $password))
[IO.File]::WriteAllBytes("$installer/Island-Local.cer", $cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
& "$kit/signtool.exe" sign /fd SHA256 /f "$PSScriptRoot/obj/signing.pfx" /p $password "$installer/Island.msix"
if ($LASTEXITCODE -ne 0) { throw 'Signing failed.' }
$cert.Dispose(); $rsa.Dispose()
Remove-Item -LiteralPath "$PSScriptRoot/obj/signing.pfx" -Force
Write-Output 'MSIX generated and signed locally. No certificates installed or trusted.'


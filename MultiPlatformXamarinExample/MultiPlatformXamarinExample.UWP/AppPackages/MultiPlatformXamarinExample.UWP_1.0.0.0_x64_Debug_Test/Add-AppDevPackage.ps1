#
# Add-AppxDevPackage.ps1 is a PowerShell script designed to install app
# packages created by Visual Studio for developers.  To run this script from
# Explorer, right-click on its icon and choose "Run with PowerShell".
#
# Visual Studio supplies this script in the folder generated with its
# "Prepare Package" command.  The same folder will also contain the app
# package (a .appx file), the signing certificate (a .cer file), and a
# "Dependencies" subfolder containing all the framework packages used by the
# app.
#
# This script simplifies installing these packages by automating the
# following functions:
#   1. Find the app package and signing certificate in the script directory
#   2. Prompt the user to acquire a developer license and to install the
#      certificate if necessary
#   3. Find dependency packages that are applicable to the operating system's
#      CPU architecture
#   4. Install the package along with all applicable dependencies
#
# All command line parameters are reserved for use internally by the script.
# Users should launch this script from Explorer.
#

# .Link
# http://go.microsoft.com/fwlink/?LinkId=243053

param(
    [switch]$Force = $false,
    [switch]$GetDeveloperLicense = $false,
    [switch]$SkipLoggingTelemetry = $false,
    [string]$CertificatePath = $null
)

$ErrorActionPreference = "Stop"

# The language resources for this script are placed in the
# "Add-AppDevPackage.resources" subfolder alongside the script.  Since the
# current working directory might not be the directory that contains the
# script, we need to create the full path of the resources directory to
# pass into Import-LocalizedData
$ScriptPath = $null
try
{
    $ScriptPath = (Get-Variable MyInvocation).Value.MyCommand.Path
    $ScriptDir = Split-Path -Parent $ScriptPath
}
catch {}

if (!$ScriptPath)
{
    PrintMessageAndExit $UiStrings.ErrorNoScriptPath $ErrorCodes.NoScriptPath
}

$LocalizedResourcePath = Join-Path $ScriptDir "Add-AppDevPackage.resources"
Import-LocalizedData -BindingVariable UiStrings -BaseDirectory $LocalizedResourcePath

$ErrorCodes = Data {
    ConvertFrom-StringData @'
    Success = 0
    NoScriptPath = 1
    NoPackageFound = 2
    ManyPackagesFound = 3
    NoCertificateFound = 4
    ManyCertificatesFound = 5
    BadCertificate = 6
    PackageUnsigned = 7
    CertificateMismatch = 8
    ForceElevate = 9
    LaunchAdminFailed = 10
    GetDeveloperLicenseFailed = 11
    InstallCertificateFailed = 12
    AddPackageFailed = 13
    ForceDeveloperLicense = 14
    CertUtilInstallFailed = 17
    CertIsCA = 18
    BannedEKU = 19
    NoBasicConstraints = 20
    NoCodeSigningEku = 21
    InstallCertificateCancelled = 22
    BannedKeyUsage = 23
    ExpiredCertificate = 24
'@
}

$IMAGE_FILE_MACHINE_i386  = 0x014c
$IMAGE_FILE_MACHINE_AMD64 = 0x8664
$IMAGE_FILE_MACHINE_ARM64 = 0xAA64
$IMAGE_FILE_MACHINE_ARM   = 0x01c0
$IMAGE_FILE_MACHINE_THUMB = 0x01c2
$IMAGE_FILE_MACHINE_ARMNT = 0x01c4

$MACHINE_ATTRIBUTES_UserEnabled    = 0x01
$MACHINE_ATTRIBUTES_KernelEnabled  = 0x02
$MACHINE_ATTRIBUTES_Wow64Container = 0x04

# First try to use IsWow64Process2 to determine the OS architecture
try
{
    $IsWow64Process2_MethodDefinition = @"
[DllImport("kernel32.dll", SetLastError = true)]
public static extern bool IsWow64Process2(IntPtr process, out ushort processMachine, out ushort nativeMachine);
"@

    $Kernel32 = Add-Type -MemberDefinition $IsWow64Process2_MethodDefinition -Name "Kernel32" -Namespace "Win32" -PassThru

    $Proc = Get-Process -id $pid
    $processMachine = New-Object uint16
    $nativeMachine = New-Object uint16

    $Res = $Kernel32::IsWow64Process2($Proc.Handle, [ref] $processMachine, [ref] $nativeMachine)
    if ($Res -eq $True)
    {
        switch ($nativeMachine)
        {
            $IMAGE_FILE_MACHINE_i386  { $ProcessorArchitecture = "x86" }
            $IMAGE_FILE_MACHINE_AMD64 { $ProcessorArchitecture = "amd64" }
            $IMAGE_FILE_MACHINE_ARM64 { $ProcessorArchitecture = "arm64" }
            $IMAGE_FILE_MACHINE_ARM   { $ProcessorArchitecture = "arm" }
            $IMAGE_FILE_MACHINE_THUMB { $ProcessorArchitecture = "arm" }
            $IMAGE_FILE_MACHINE_ARMNT { $ProcessorArchitecture = "arm" }
        }
    }
}
catch
{
    # Ignore exception and fall back to using environment variables to determine the OS architecture
}

$Amd64AppsSupportedOnArm64 = $False
if ($null -eq $ProcessorArchitecture)
{
    $ProcessorArchitecture = $Env:Processor_Architecture

    # Getting $Env:Processor_Architecture on arm64 machines will return x86.  So check if the environment
    # variable "ProgramFiles(Arm)" is also set, if it is we know the actual processor architecture is arm64.
    # The value will also be x86 on amd64 machines when running the x86 version of PowerShell.
    if ($ProcessorArchitecture -eq "x86")
    {
        if ($null -ne ${Env:ProgramFiles(Arm)})
        {
            $ProcessorArchitecture = "arm64"
        }
        elseif ($null -ne ${Env:ProgramFiles(x86)})
        {
            $ProcessorArchitecture = "amd64"
        }
    }
}
elseif ("arm64" -eq $ProcessorArchitecture)
{
    # If we successfully determined the OS to be arm64 with the above call to IsWow64Process2 and we're on Win11+
    # we need to check for a special case where amd64 apps are supported as well.
    if ([Environment]::OSVersion.Version -ge (new-object 'Version' 10,0,22000))
    {
        try
        {
            $GetMachineTypeAttributes_MethodDefinition = @"
[DllImport("api-ms-win-core-processthreads-l1-1-7.dll", EntryPoint = "GetMachineTypeAttributes", ExactSpelling = true, PreserveSig = false)]
public static extern void GetMachineTypeAttributes(ushort Machine, out ushort machineTypeAttributes);
"@

            $ProcessThreads = Add-Type -MemberDefinition $GetMachineTypeAttributes_MethodDefinition -Name "processthreads_l1_1_7" -Namespace "Win32" -PassThru
            $machineTypeAttributes = New-Object uint16

            $ProcessThreads::GetMachineTypeAttributes($IMAGE_FILE_MACHINE_AMD64, [ref] $machineTypeAttributes)

            # We're looking for the case where the UserEnabled flag is set
            if (($machineTypeAttributes -band $MACHINE_ATTRIBUTES_UserEnabled) -ne 0)
            {
                $Amd64AppsSupportedOnArm64 = $True
            }
        }
        catch
        {
            # Ignore exceptions and maintain assumption that amd64 apps aren't supported on this machine
        }
    }
}

function PrintMessageAndExit($ErrorMessage, $ReturnCode)
{
    Write-Host $ErrorMessage
    try
    {
        # Log telemetry data regarding the use of the script if possible.
        # There are three ways that this can be disabled:
        #   1. If the "TelemetryDependencies" folder isn't present.  This can be excluded at build time by setting the MSBuild property AppxLogTelemetryFromSideloadingScript to false
        #   2. If the SkipLoggingTelemetry switch is passed to this script.
        #   3. If Visual Studio telemetry is disabled via the registry.
        $TelemetryAssembliesFolder = (Join-Path $PSScriptRoot "TelemetryDependencies")
        if (!$SkipLoggingTelemetry -And (Test-Path $TelemetryAssembliesFolder))
        {
            $job = Start-Job -FilePath (Join-Path $TelemetryAssembliesFolder "LogSideloadingTelemetry.ps1") -ArgumentList $TelemetryAssembliesFolder, "VS/DesignTools/SideLoadingScript/AddAppDevPackage", $ReturnCode, $ProcessorArchitecture
            Wait-Job -Job $job -Timeout 60 | Out-Null
        }
    }
    catch
    {
        # Ignore telemetry errors
    }

    if (!$Force)
    {
        Pause
    }
    
    exit $ReturnCode
}

#
# Warns the user about installing certificates, and presents a Yes/No prompt
# to confirm the action.  The default is set to No.
#
function ConfirmCertificateInstall
{
    $Answer = $host.UI.PromptForChoice(
                    "", 
                    $UiStrings.WarningInstallCert, 
                    [System.Management.Automation.Host.ChoiceDescription[]]@($UiStrings.PromptYesString, $UiStrings.PromptNoString), 
                    1)
    
    return $Answer -eq 0
}

#
# Validates whether a file is a valid certificate using CertUtil.
# This needs to be done before calling Get-PfxCertificate on the file, otherwise
# the user will get a cryptic "Password: " prompt for invalid certs.
#
function ValidateCertificateFormat($FilePath)
{
    # certutil -verify prints a lot of text that we don't need, so it's redirected to $null here
    certutil.exe -verify $FilePath > $null
    if ($LastExitCode -lt 0)
    {
        PrintMessageAndExit ($UiStrings.ErrorBadCertificate -f $FilePath, $LastExitCode) $ErrorCodes.BadCertificate
    }
    
    # Check if certificate is expired
    $cert = Get-PfxCertificate $FilePath
    if (($cert.NotBefore -gt (Get-Date)) -or ($cert.NotAfter -lt (Get-Date)))
    {
        PrintMessageAndExit ($UiStrings.ErrorExpiredCertificate -f $FilePath) $ErrorCodes.ExpiredCertificate
    }
}

#
# Verify that the developer certificate meets the following restrictions:
#   - The certificate must contain a Basic Constraints extension, and its
#     Certificate Authority (CA) property must be false.
#   - The certificate's Key Usage extension must be either absent, or set to
#     only DigitalSignature.
#   - The certificate must contain an Extended Key Usage (EKU) extension with
#     Code Signing usage.
#   - The certificate must NOT contain any other EKU except Code Signing and
#     Lifetime Signing.
#
# These restrictions are enforced to decrease security risks that arise from
# trusting digital certificates.
#
function CheckCertificateRestrictions
{
    Set-Variable -Name BasicConstraintsExtensionOid -Value "2.5.29.19" -Option Constant
    Set-Variable -Name KeyUsageExtensionOid -Value "2.5.29.15" -Option Constant
    Set-Variable -Name EkuExtensionOid -Value "2.5.29.37" -Option Constant
    Set-Variable -Name CodeSigningEkuOid -Value "1.3.6.1.5.5.7.3.3" -Option Constant
    Set-Variable -Name LifetimeSigningEkuOid -Value "1.3.6.1.4.1.311.10.3.13" -Option Constant
    Set-Variable -Name UwpSigningEkuOid -Value "1.3.6.1.4.1.311.84.3.1" -Option Constant
    Set-Variable -Name DisposableSigningEkuOid -Value "1.3.6.1.4.1.311.84.3.2" -Option Constant

    $CertificateExtensions = (Get-PfxCertificate $CertificatePath).Extensions
    $HasBasicConstraints = $false
    $HasCodeSigningEku = $false

    foreach ($Extension in $CertificateExtensions)
    {
        # Certificate must contain the Basic Constraints extension
        if ($Extension.oid.value -eq $BasicConstraintsExtensionOid)
        {
            # CA property must be false
            if ($Extension.CertificateAuthority)
            {
                PrintMessageAndExit $UiStrings.ErrorCertIsCA $ErrorCodes.CertIsCA
            }
            $HasBasicConstraints = $true
        }

        # If key usage is present, it must be set to digital signature
        elseif ($Extension.oid.value -eq $KeyUsageExtensionOid)
        {
            if ($Extension.KeyUsages -ne "DigitalSignature")
            {
                PrintMessageAndExit ($UiStrings.ErrorBannedKeyUsage -f $Extension.KeyUsages) $ErrorCodes.BannedKeyUsage
            }
        }

        elseif ($Extension.oid.value -eq $EkuExtensionOid)
        {
            # Certificate must contain the Code Signing EKU
            $EKUs = $Extension.EnhancedKeyUsages.Value
            if ($EKUs -contains $CodeSigningEkuOid)
            {
                $HasCodeSigningEKU = $True
            }

            # EKUs other than code signing and lifetime signing are not allowed
            foreach ($EKU in $EKUs)
            {
                if ($EKU -ne $CodeSigningEkuOid -and $EKU -ne $LifetimeSigningEkuOid -and $EKU -ne $UwpSigningEkuOid -and $EKU -ne $DisposableSigningEkuOid)
                {
                    PrintMessageAndExit ($UiStrings.ErrorBannedEKU -f $EKU) $ErrorCodes.BannedEKU
                }
            }
        }
    }

    if (!$HasBasicConstraints)
    {
        PrintMessageAndExit $UiStrings.ErrorNoBasicConstraints $ErrorCodes.NoBasicConstraints
    }
    if (!$HasCodeSigningEKU)
    {
        PrintMessageAndExit $UiStrings.ErrorNoCodeSigningEku $ErrorCodes.NoCodeSigningEku
    }
}

#
# Performs operations that require administrative privileges:
#   - Prompt the user to obtain a developer license
#   - Install the developer certificate (if -Force is not specified, also prompts the user to confirm)
#
function DoElevatedOperations
{
    if ($GetDeveloperLicense)
    {
        Write-Host $UiStrings.GettingDeveloperLicense

        if ($Force)
        {
            PrintMessageAndExit $UiStrings.ErrorForceDeveloperLicense $ErrorCodes.ForceDeveloperLicense
        }
        try
        {
            Show-WindowsDeveloperLicenseRegistration
        }
        catch
        {
            $Error[0] # Dump details about the last error
            PrintMessageAndExit $UiStrings.ErrorGetDeveloperLicenseFailed $ErrorCodes.GetDeveloperLicenseFailed
        }
    }

    if ($CertificatePath)
    {
        Write-Host $UiStrings.InstallingCertificate

        # Make sure certificate format is valid and usage constraints are followed
        ValidateCertificateFormat $CertificatePath
        CheckCertificateRestrictions

        # If -Force is not specified, warn the user and get consent
        if ($Force -or (ConfirmCertificateInstall))
        {
            # Add cert to store
            certutil.exe -addstore TrustedPeople $CertificatePath
            if ($LastExitCode -lt 0)
            {
                PrintMessageAndExit ($UiStrings.ErrorCertUtilInstallFailed -f $LastExitCode) $ErrorCodes.CertUtilInstallFailed
            }
        }
        else
        {
            PrintMessageAndExit $UiStrings.ErrorInstallCertificateCancelled $ErrorCodes.InstallCertificateCancelled
        }
    }
}

#
# Checks whether the machine is missing a valid developer license.
#
function CheckIfNeedDeveloperLicense
{
    $Result = $true
    try
    {
        $Result = (Get-WindowsDeveloperLicense | Where-Object { $_.IsValid } | Measure-Object).Count -eq 0
    }
    catch {}

    return $Result
}

#
# Launches an elevated process running the current script to perform tasks
# that require administrative privileges.  This function waits until the
# elevated process terminates, and checks whether those tasks were successful.
#
function LaunchElevated
{
    # Set up command line arguments to the elevated process
    $RelaunchArgs = '-ExecutionPolicy Unrestricted -file "' + $ScriptPath + '"'

    if ($Force)
    {
        $RelaunchArgs += ' -Force'
    }
    if ($NeedDeveloperLicense)
    {
        $RelaunchArgs += ' -GetDeveloperLicense'
    }
    if ($SkipLoggingTelemetry)
    {
        $RelaunchArgs += ' -SkipLoggingTelemetry'
    }
    if ($NeedInstallCertificate)
    {
        $RelaunchArgs += ' -CertificatePath "' + $DeveloperCertificatePath.FullName + '"'
    }

    # Launch the process and wait for it to finish
    try
    {
        $PowerShellExePath = (Get-Process -Id $PID).Path
        $AdminProcess = Start-Process $PowerShellExePath -Verb RunAs -ArgumentList $RelaunchArgs -PassThru
    }
    catch
    {
        $Error[0] # Dump details about the last error
        PrintMessageAndExit $UiStrings.ErrorLaunchAdminFailed $ErrorCodes.LaunchAdminFailed
    }

    while (!($AdminProcess.HasExited))
    {
        Start-Sleep -Seconds 2
    }

    # Check if all elevated operations were successful
    if ($NeedDeveloperLicense)
    {
        if (CheckIfNeedDeveloperLicense)
        {
            PrintMessageAndExit $UiStrings.ErrorGetDeveloperLicenseFailed $ErrorCodes.GetDeveloperLicenseFailed
        }
        else
        {
            Write-Host $UiStrings.AcquireLicenseSuccessful
        }
    }
    if ($NeedInstallCertificate)
    {
        $Signature = Get-AuthenticodeSignature $DeveloperPackagePath -Verbose
        if ($Signature.Status -ne "Valid")
        {
            PrintMessageAndExit ($UiStrings.ErrorInstallCertificateFailed -f $Signature.Status) $ErrorCodes.InstallCertificateFailed
        }
        else
        {
            Write-Host $UiStrings.InstallCertificateSuccessful
        }
    }
}

#
# Finds all applicable dependency packages according to OS architecture, and
# installs the developer package with its dependencies.  The expected layout
# of dependencies is:
#
# <current dir>
#     \Dependencies
#         <Architecture neutral dependencies>.appx\.msix
#         \x86
#             <x86 dependencies>.appx\.msix
#         \x64
#             <x64 dependencies>.appx\.msix
#         \arm
#             <arm dependencies>.appx\.msix
#         \arm64
#             <arm64 dependencies>.appx\.msix
#
function InstallPackageWithDependencies
{
    $DependencyPackagesDir = (Join-Path $ScriptDir "Dependencies")
    $DependencyPackages = @()
    if (Test-Path $DependencyPackagesDir)
    {
        # Get architecture-neutral dependencies
        $DependencyPackages += Get-ChildItem (Join-Path $DependencyPackagesDir "*.appx") | Where-Object { $_.Mode -NotMatch "d" }
        $DependencyPackages += Get-ChildItem (Join-Path $DependencyPackagesDir "*.msix") | Where-Object { $_.Mode -NotMatch "d" }

        # Get architecture-specific dependencies
        if (($ProcessorArchitecture -eq "x86" -or $ProcessorArchitecture -eq "amd64" -or $ProcessorArchitecture -eq "arm64") -and (Test-Path (Join-Path $DependencyPackagesDir "x86")))
        {
            $DependencyPackages += Get-ChildItem (Join-Path $DependencyPackagesDir "x86\*.appx") | Where-Object { $_.Mode -NotMatch "d" }
            $DependencyPackages += Get-ChildItem (Join-Path $DependencyPackagesDir "x86\*.msix") | Where-Object { $_.Mode -NotMatch "d" }
        }
        if ((($ProcessorArchitecture -eq "amd64") -or ($ProcessorArchitecture -eq "arm64" -and $Amd64AppsSupportedOnArm64)) -and (Test-Path (Join-Path $DependencyPackagesDir "x64")))
        {
            $DependencyPackages += Get-ChildItem (Join-Path $DependencyPackagesDir "x64\*.appx") | Where-Object { $_.Mode -NotMatch "d" }
            $DependencyPackages += Get-ChildItem (Join-Path $DependencyPackagesDir "x64\*.msix") | Where-Object { $_.Mode -NotMatch "d" }
        }
        if (($ProcessorArchitecture -eq "arm" -or $ProcessorArchitecture -eq "arm64") -and (Test-Path (Join-Path $DependencyPackagesDir "arm")))
        {
            $DependencyPackages += Get-ChildItem (Join-Path $DependencyPackagesDir "arm\*.appx") | Where-Object { $_.Mode -NotMatch "d" }
            $DependencyPackages += Get-ChildItem (Join-Path $DependencyPackagesDir "arm\*.msix") | Where-Object { $_.Mode -NotMatch "d" }
        }
        if (($ProcessorArchitecture -eq "arm64") -and (Test-Path (Join-Path $DependencyPackagesDir "arm64")))
        {
            $DependencyPackages += Get-ChildItem (Join-Path $DependencyPackagesDir "arm64\*.appx") | Where-Object { $_.Mode -NotMatch "d" }
            $DependencyPackages += Get-ChildItem (Join-Path $DependencyPackagesDir "arm64\*.msix") | Where-Object { $_.Mode -NotMatch "d" }
        }
    }
    Write-Host $UiStrings.InstallingPackage

    $AddPackageSucceeded = $False
    try
    {
        if ($DependencyPackages.FullName.Count -gt 0)
        {
            Write-Host $UiStrings.DependenciesFound
            $DependencyPackages.FullName
            Add-AppxPackage -Path $DeveloperPackagePath.FullName -DependencyPath $DependencyPackages.FullName -ForceApplicationShutdown
        }
        else
        {
            Add-AppxPackage -Path $DeveloperPackagePath.FullName -ForceApplicationShutdown
        }
        $AddPackageSucceeded = $?
    }
    catch
    {
        $Error[0] # Dump details about the last error
    }

    if (!$AddPackageSucceeded)
    {
        if ($NeedInstallCertificate)
        {
            PrintMessageAndExit $UiStrings.ErrorAddPackageFailedWithCert $ErrorCodes.AddPackageFailed
        }
        else
        {
            PrintMessageAndExit $UiStrings.ErrorAddPackageFailed $ErrorCodes.AddPackageFailed
        }
    }
}

#
# Main script logic when the user launches the script without parameters.
#
function DoStandardOperations
{
    # Check for an .appx or .msix file in the script directory
    $PackagePath = Get-ChildItem (Join-Path $ScriptDir "*.appx") | Where-Object { $_.Mode -NotMatch "d" }
    if ($PackagePath -eq $null)
    {
        $PackagePath = Get-ChildItem (Join-Path $ScriptDir "*.msix") | Where-Object { $_.Mode -NotMatch "d" }
    }
    $PackageCount = ($PackagePath | Measure-Object).Count

    # Check for an .appxbundle or .msixbundle file in the script directory
    $BundlePath = Get-ChildItem (Join-Path $ScriptDir "*.appxbundle") | Where-Object { $_.Mode -NotMatch "d" }
    if ($BundlePath -eq $null)
    {
        $BundlePath = Get-ChildItem (Join-Path $ScriptDir "*.msixbundle") | Where-Object { $_.Mode -NotMatch "d" }
    }
    $BundleCount = ($BundlePath | Measure-Object).Count

    # Check for an .eappx or .emsix file in the script directory
    $EncryptedPackagePath = Get-ChildItem (Join-Path $ScriptDir "*.eappx") | Where-Object { $_.Mode -NotMatch "d" }
    if ($EncryptedPackagePath -eq $null)
    {
        $EncryptedPackagePath = Get-ChildItem (Join-Path $ScriptDir "*.emsix") | Where-Object { $_.Mode -NotMatch "d" }
    }
    $EncryptedPackageCount = ($EncryptedPackagePath | Measure-Object).Count

    # Check for an .eappxbundle or .emsixbundle file in the script directory
    $EncryptedBundlePath = Get-ChildItem (Join-Path $ScriptDir "*.eappxbundle") | Where-Object { $_.Mode -NotMatch "d" }
    if ($EncryptedBundlePath -eq $null)
    {
        $EncryptedBundlePath = Get-ChildItem (Join-Path $ScriptDir "*.emsixbundle") | Where-Object { $_.Mode -NotMatch "d" }
    }
    $EncryptedBundleCount = ($EncryptedBundlePath | Measure-Object).Count

    $NumberOfPackages = $PackageCount + $EncryptedPackageCount
    $NumberOfBundles = $BundleCount + $EncryptedBundleCount

    # There must be at least one package or bundle
    if ($NumberOfPackages + $NumberOfBundles -lt 1)
    {
        PrintMessageAndExit $UiStrings.ErrorNoPackageFound $ErrorCodes.NoPackageFound
    }
    # We must have exactly one bundle OR no bundle and exactly one package
    elseif ($NumberOfBundles -gt 1 -or
            ($NumberOfBundles -eq 0 -and $NumberOfpackages -gt 1))
    {
        PrintMessageAndExit $UiStrings.ErrorManyPackagesFound $ErrorCodes.ManyPackagesFound
    }

    # First attempt to install a bundle or encrypted bundle. If neither exists, fall back to packages and then encrypted packages
    if ($BundleCount -eq 1)
    {
        $DeveloperPackagePath = $BundlePath
        Write-Host ($UiStrings.BundleFound -f $DeveloperPackagePath.FullName)
    }
    elseif ($EncryptedBundleCount -eq 1)
    {
        $DeveloperPackagePath = $EncryptedBundlePath
        Write-Host ($UiStrings.EncryptedBundleFound -f $DeveloperPackagePath.FullName)
    }
    elseif ($PackageCount -eq 1)
    {
        $DeveloperPackagePath = $PackagePath
        Write-Host ($UiStrings.PackageFound -f $DeveloperPackagePath.FullName)
    }
    elseif ($EncryptedPackageCount -eq 1)
    {
        $DeveloperPackagePath = $EncryptedPackagePath
        Write-Host ($UiStrings.EncryptedPackageFound -f $DeveloperPackagePath.FullName)
    }
    
    # The package must be signed
    $PackageSignature = Get-AuthenticodeSignature $DeveloperPackagePath
    $PackageCertificate = $PackageSignature.SignerCertificate
    if (!$PackageCertificate)
    {
        PrintMessageAndExit $UiStrings.ErrorPackageUnsigned $ErrorCodes.PackageUnsigned
    }

    # Test if the package signature is trusted.  If not, the corresponding certificate
    # needs to be present in the current directory and needs to be installed.
    $NeedInstallCertificate = ($PackageSignature.Status -ne "Valid")

    if ($NeedInstallCertificate)
    {
        # List all .cer files in the script directory
        $DeveloperCertificatePath = Get-ChildItem (Join-Path $ScriptDir "*.cer") | Where-Object { $_.Mode -NotMatch "d" }
        $DeveloperCertificateCount = ($DeveloperCertificatePath | Measure-Object).Count

        # There must be exactly 1 certificate
        if ($DeveloperCertificateCount -lt 1)
        {
            PrintMessageAndExit $UiStrings.ErrorNoCertificateFound $ErrorCodes.NoCertificateFound
        }
        elseif ($DeveloperCertificateCount -gt 1)
        {
            PrintMessageAndExit $UiStrings.ErrorManyCertificatesFound $ErrorCodes.ManyCertificatesFound
        }

        Write-Host ($UiStrings.CertificateFound -f $DeveloperCertificatePath.FullName)

        # The .cer file must have the format of a valid certificate
        ValidateCertificateFormat $DeveloperCertificatePath

        # The package signature must match the certificate file
        if ($PackageCertificate -ne (Get-PfxCertificate $DeveloperCertificatePath))
        {
            PrintMessageAndExit $UiStrings.ErrorCertificateMismatch $ErrorCodes.CertificateMismatch
        }
    }

    $NeedDeveloperLicense = CheckIfNeedDeveloperLicense

    # Relaunch the script elevated with the necessary parameters if needed
    if ($NeedDeveloperLicense -or $NeedInstallCertificate)
    {
        Write-Host $UiStrings.ElevateActions
        if ($NeedDeveloperLicense)
        {
            Write-Host $UiStrings.ElevateActionDevLicense
        }
        if ($NeedInstallCertificate)
        {
            Write-Host $UiStrings.ElevateActionCertificate
        }

        $IsAlreadyElevated = ([Security.Principal.WindowsIdentity]::GetCurrent().Groups.Value -contains "S-1-5-32-544")
        if ($IsAlreadyElevated)
        {
            if ($Force -and $NeedDeveloperLicense)
            {
                PrintMessageAndExit $UiStrings.ErrorForceDeveloperLicense $ErrorCodes.ForceDeveloperLicense
            }
            if ($Force -and $NeedInstallCertificate)
            {
                Write-Warning $UiStrings.WarningInstallCert
            }
        }
        else
        {
            if ($Force)
            {
                PrintMessageAndExit $UiStrings.ErrorForceElevate $ErrorCodes.ForceElevate
            }
            else
            {
                Write-Host $UiStrings.ElevateActionsContinue
                Pause
            }
        }

        LaunchElevated
    }

    InstallPackageWithDependencies
}

#
# Main script entry point
#
if ($GetDeveloperLicense -or $CertificatePath)
{
    DoElevatedOperations
}
else
{
    DoStandardOperations
    PrintMessageAndExit $UiStrings.Success $ErrorCodes.Success
}

# SIG # Begin signature block
# MIIphgYJKoZIhvcNAQcCoIIpdzCCKXMCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCAYh/586NhEPWiO
# +3D6/pj3+lXDRXkK9+wCdlGR7safL6CCDdYwgga9MIIEpaADAgECAhMzAAAAHEif
# gd+hsLd3AAAAAAAcMA0GCSqGSIb3DQEBDAUAMIGIMQswCQYDVQQGEwJVUzETMBEG
# A1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWlj
# cm9zb2Z0IENvcnBvcmF0aW9uMTIwMAYDVQQDEylNaWNyb3NvZnQgUm9vdCBDZXJ0
# aWZpY2F0ZSBBdXRob3JpdHkgMjAxMDAeFw0yNDA4MDgyMTM2MjNaFw0zNTA2MjMy
# MjA0MDFaMF8xCzAJBgNVBAYTAlVTMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9y
# YXRpb24xMDAuBgNVBAMTJ01pY3Jvc29mdCBXaW5kb3dzIENvZGUgU2lnbmluZyBQ
# Q0EgMjAyNDCCAiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAJp9a30nwXYq
# Lq7j1TT/zCtt7vxU+CCj+7BkifS/B2gXKGU7OV9SXRJGP1yFs5p6jpsYi4cYzF56
# AV0AEmmEjV8wT2lvPU5BhN3wV30HqYPIYEj5P3WXf0kXD9fvjUf1GAtXEriJ8w7A
# LNaVEm9Rs4ePA0ZsYHaCbU5kBUJQDXv76hafOcQgdFCA3I3zYtfzX2vOwx87uDOa
# CuyKORZih9c3zTf+TLC5QYLyhVMBnDXEHDOrvaw92DSyIqpdgRWpufzqDFy1egVj
# koXZhb+9pZ9heUzNXTXhOoXzexh6YzAL4flBWm+Bc1hQyESenEvBJznV+25u3h77
# jjgMUY44+WXQ4u9qddDe/U5SeAaKRvvibmi4z7QRpLvZsla0CPiOUGz00Do5sfkC
# 0EwlsSzfM3+8A9rsyFVOgWDVPzt98OJP2EoaEOq8GE9GCoN2i7/4C2FCwff1BSCT
# JWZO1Wcr2MteJE6UxGV+ihA8nN51YPKD2dYGoewrXvRzC/1HoUeSvlZf0mf9GHEt
# vvkbJVRRo6PBf0md5t87Vb1mM/fIp1eypyaxmXkgpcBwuylsOq2kSVOJ5wBPoaEs
# sJkeMcKnEuuu++UKdDHlS0DtsYjN0QnOucvTdSsdvhzKOSjJF3XVqr9f2C945LXT
# 5rxKIHUIEDBcNYU6BKDDH6rfpKOOCSilAgMBAAGjggFGMIIBQjAOBgNVHQ8BAf8E
# BAMCAYYwEAYJKwYBBAGCNxUBBAMCAQAwHQYDVR0OBBYEFB6C3w7XjLPXAjSDDtqr
# rWW5r7jsMBkGCSsGAQQBgjcUAgQMHgoAUwB1AGIAQwBBMA8GA1UdEwEB/wQFMAMB
# Af8wHwYDVR0jBBgwFoAU1fZWy4/oolxiaNE9lJBb186aGMQwVgYDVR0fBE8wTTBL
# oEmgR4ZFaHR0cDovL2NybC5taWNyb3NvZnQuY29tL3BraS9jcmwvcHJvZHVjdHMv
# TWljUm9vQ2VyQXV0XzIwMTAtMDYtMjMuY3JsMFoGCCsGAQUFBwEBBE4wTDBKBggr
# BgEFBQcwAoY+aHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraS9jZXJ0cy9NaWNS
# b29DZXJBdXRfMjAxMC0wNi0yMy5jcnQwDQYJKoZIhvcNAQEMBQADggIBAENf+N8/
# u+mUjDtc9btoA52RBc0XVDSBMQBMqxu56hXHBwuctUWs1XBqDDMIFCHu9c6Y/UF+
# TN8EIgjnujApKYmHP4f4EM3ARSmlzrpF5ozOJx0BA5FUv1jmpdf/2ZbqpvCxlxv/
# G1R4KjrSmmqPHzs6igw3b7RTbj7BxIS8fOIkwYWQhB2fLjlg+3HSrDGPFIhpIJWV
# amMIR7a72OGonjdf45rspwqIHuynZU4avy9ruB/Rhhbwm+fMb8BMecIaTmkohx/E
# ZZ5GNWcN6oTYW3G2BM3B3YznWkl9t4shP60fMue+2ksdHGWSE8EVTdSmGUdj0jrU
# c46lGVFJISF3/MxcxnlNeP1Khyr+ZzT4Ets/I7mufpaLnLalzMR2zIuhGOAWWswe
# sbjtFzkVUFgDR2SW903I0XKlbPEA6q8epHGJ9roxh85nsEKcBNUw4Scp68KCqSpF
# BaKiyV1skd+l8U50WNePMb9Bzz0OfASal8v5sQG+DW01kN+I+RKUIbM5I50wJjiH
# ymQFNDsbobFx9I95mCEEPU7fUZ3VT/HOUVbkmX7ltIC/eQAu5GO8fu+ceETMybvb
# oxUM4dYNC+PzooUxfmC0DuKRwB21bX9+acuIBkxIm4Ed3O19w1VLoA7UNOUuJ7z6
# NQ2W/+q7cnfOPl2QVL4qlgCblUT2vmQpllV3MIIHETCCBPmgAwIBAgITMwAAAIe8
# gm6Foa5TqAAAAAAAhzANBgkqhkiG9w0BAQwFADBfMQswCQYDVQQGEwJVUzEeMBwG
# A1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMTAwLgYDVQQDEydNaWNyb3NvZnQg
# V2luZG93cyBDb2RlIFNpZ25pbmcgUENBIDIwMjQwHhcNMjUwNTA4MTgyNDU0WhcN
# MjYwNTA2MTgyNDU0WjB0MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3Rv
# bjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0
# aW9uMR4wHAYDVQQDExVNaWNyb3NvZnQgQ29ycG9yYXRpb24wggIiMA0GCSqGSIb3
# DQEBAQUAA4ICDwAwggIKAoICAQC0JvvNCq9eE0sLt0WdArsItuEZpbuTI0W3EVMS
# i6PTkY1xniZhsm8rsEzr/Ngq77HknKV8GYYNkVxhppwdoQilh/R0KELXZi22Qqi4
# jEUsOYgkDHPIbnUEcQvbaG1R2ijOme/uGE9DX3RBx9Pkbi8TYec1d2iDhzF/xofy
# oZHPUTvL2PzDae7ncAjjRChG7kCzobShkcMpUZPyYMXKtqCMAx4OBYdeBF6PGfft
# 4Z16crWAmSEGMCcXQ7EVxWj1W7R5sOo4TilWQBQp9yYyvKYUObpiQwUjNZb4wALe
# Y6msmJogp7LC5N6warLYTbhwJJblmhZQIhaD8UABuP030nUofVkGDqK6xC/REnOE
# KTnsE+KaRb8JDOBXWSscNMQSR7wbX2NF+hK/S4dg6NUHzr0p0k20yY8LZg0OPOeb
# Hg5WdXUqkFHNB4Ck2aOT4rhu7YYiYBewfVZXF4/XF/BemkMKgnQToJvFJYLMPZM3
# tosN1IW0Ow3Ny3RvwQfHPGQ1kSqprOl14JFTI9CO/CZzhZbgeRB9Z3dWcXt1RYpu
# NIAkMWl7gDTRYy4gWhkgmzE1x1cz1hA8hKZIvD0VmNiwqN1HHRDHn2ryxmLgZgLh
# kth4K/i3CR+xSiptNnYnpMkE0Rre89r45MKUHytz0z0yqSsey/BsJ9RhnTvqiNrX
# Ay0TBwIDAQABo4IBrzCCAaswDgYDVR0PAQH/BAQDAgeAMB8GA1UdJQQYMBYGCisG
# AQQBgjc9BgEGCCsGAQUFBwMDMAwGA1UdEwEB/wQCMAAwHQYDVR0OBBYEFCYGR6ii
# PVV4VrV7+HKNkZBNf6SoMEUGA1UdEQQ+MDykOjA4MR4wHAYDVQQLExVNaWNyb3Nv
# ZnQgQ29ycG9yYXRpb24xFjAUBgNVBAUTDTIzMDg2NSs1MDQ1ODEwHwYDVR0jBBgw
# FoAUHoLfDteMs9cCNIMO2qutZbmvuOwwagYDVR0fBGMwYTBfoF2gW4ZZaHR0cDov
# L3d3dy5taWNyb3NvZnQuY29tL3BraW9wcy9jcmwvTWljcm9zb2Z0JTIwV2luZG93
# cyUyMENvZGUlMjBTaWduaW5nJTIwUENBJTIwMjAyNC5jcmwwdwYIKwYBBQUHAQEE
# azBpMGcGCCsGAQUFBzAChltodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpb3Bz
# L2NlcnRzL01pY3Jvc29mdCUyMFdpbmRvd3MlMjBDb2RlJTIwU2lnbmluZyUyMFBD
# QSUyMDIwMjQuY3J0MA0GCSqGSIb3DQEBDAUAA4ICAQB+y/LNkTgCe/vYXjHxIrS/
# 73+FTugtMOG7l/fuiVFw3poLaGGtcn7LF5s8/h6soz4ST82QeZA3y/ADvl5VXeO1
# 5mROcELkFZ8kbdzqXTnyuWpDOTTm54DX2XVwxQfFIxYJqBij8pwvlNAB5r3JOBOA
# fegOad1NuCoP0/aA5hgu4ci3d0i+LaBVHm2RlHM44si1KyEg8Rs9g/SQPSijwRMt
# Da7TsWmz7F1J9P+UV36yOmwQ4jepF5h8hFUSrCJ3x7tEqA0ruiy2yCT15FcaJdnQ
# 3tZ8RRUdoCJ/pxtDI3YySMLA2XYLs4qHzoF/TPPNtaWDTvr4a+rBbUZm58pV+S14
# jhtkQAoO+275/a+ESeRSEeP9+Hde3Ez06U1MRKu+uicTPfDhUggSXzcaDImnRS6i
# FjEryJGHhRyZYoMphCjLn+PBJe58nvL/HWuP7kCMbSJOYz1ghVU/k5cgTYqq/V3E
# yzIo2K0MtwoUQcZygkTdCRUv6EtqFWbxtK6fyM6mbCoRgs2tKdMl8EN5nSMRXZEr
# sx6bG6M7BgTiSP56F//HvdLyGKfAR247Nu4/1nxgcDBZVcvCJyj6FmYKnvQEWixb
# aXFlpvCMvahUFV1b/1FKk2EF4ntMScCjyTKUXSTt8cgmVfS8FB1YkZm7IsCjBkua
# GWbGZBLKlwLys7wpFhqvOzGCGwYwghsCAgEBMHYwXzELMAkGA1UEBhMCVVMxHjAc
# BgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEwMC4GA1UEAxMnTWljcm9zb2Z0
# IFdpbmRvd3MgQ29kZSBTaWduaW5nIFBDQSAyMDI0AhMzAAAAh7yCboWhrlOoAAAA
# AACHMA0GCWCGSAFlAwQCAQUAoIGuMBkGCSqGSIb3DQEJAzEMBgorBgEEAYI3AgEE
# MBwGCisGAQQBgjcCAQsxDjAMBgorBgEEAYI3AgEVMC8GCSqGSIb3DQEJBDEiBCA/
# 8sllbhmdgHazXDKfJje5ZPISyNOJfdmSfAvZZC8tJDBCBgorBgEEAYI3AgEMMTQw
# MqAUgBIATQBpAGMAcgBvAHMAbwBmAHShGoAYaHR0cDovL3d3dy5taWNyb3NvZnQu
# Y29tMA0GCSqGSIb3DQEBAQUABIICAFzxqnluSeG0Cvkuk3K4rqQeZFrPiU0jL0N6
# uahF7wuqAj37rTXV4zhYle7l79GgChRHYa8T+cxSPNnDg5vfdnh/hxowO5jfn8qo
# KchMzXe884a+k1kymRrSfat4EEnBquZ6gq4caKWWoKU+Tqn/wvNoBtnsJ6uA8bCl
# v2inj6KgiKNoqbYgTxPRqu5Qc5u2OK9gnND66X+eab4cO6EpWZFIbMwB1v6hR8oj
# 57VyUGC3Lks+B6fGSu3M6vayu0w604Q8N6xpPKCdOQTDJdPRXTtwUIr7Z6jftzSl
# YB/rah5LcmrhNvDUEMy+ZJaUvasu0OfGXl/XH4KL16r0NsKbQO6fOj5eEBWLBbWm
# f6WWzg3LC3W5na90zl/53ep3kVchEbs1aLdFjg1CGOMMulWFW7BZsha6JCoBhsrY
# m9s3VnAUwrg07qSp8xKPwZuowbwRmQPGZ4Pjzajf2JJx6CQL2qS8QhQZcFQ1SrWP
# ZEE+VLRKmFhZScvPDzhdPwv5GbVGcYL+qVabNh9aRSJ0Z22qhSZ4sKbKDPRhDk+t
# Tto9+DR+BgxYlOihv8ZtVK1a+VNzB8MrRZXVwLrzuQJvo2mdQEbzCzBjENGfOcsp
# pbambRw7jREeYMMOhAuHAvGlTUNteBoPMvOfaH46HgVVP88x2EOrMiWR1uI/CCx7
# zqRZP33boYIXsDCCF6wGCisGAQQBgjcDAwExghecMIIXmAYJKoZIhvcNAQcCoIIX
# iTCCF4UCAQMxDzANBglghkgBZQMEAgEFADCCAVoGCyqGSIb3DQEJEAEEoIIBSQSC
# AUUwggFBAgEBBgorBgEEAYRZCgMBMDEwDQYJYIZIAWUDBAIBBQAEIPkLsrZgZtjh
# Qd6NmYQx3yb6XhdiTuIN1P5gE4RarDdOAgZoUxoTsMUYEzIwMjUwNzEwMjI1ODUx
# LjQzNlowBIACAfSggdmkgdYwgdMxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNo
# aW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29y
# cG9yYXRpb24xLTArBgNVBAsTJE1pY3Jvc29mdCBJcmVsYW5kIE9wZXJhdGlvbnMg
# TGltaXRlZDEnMCUGA1UECxMeblNoaWVsZCBUU1MgRVNOOjU1MUEtMDVFMC1EOTQ3
# MSUwIwYDVQQDExxNaWNyb3NvZnQgVGltZS1TdGFtcCBTZXJ2aWNloIIR/jCCBygw
# ggUQoAMCAQICEzMAAAIB0UVZmBDMQk8AAQAAAgEwDQYJKoZIhvcNAQELBQAwfDEL
# MAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1v
# bmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWlj
# cm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwHhcNMjQwNzI1MTgzMTIyWhcNMjUx
# MDIyMTgzMTIyWjCB0zELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24x
# EDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlv
# bjEtMCsGA1UECxMkTWljcm9zb2Z0IElyZWxhbmQgT3BlcmF0aW9ucyBMaW1pdGVk
# MScwJQYDVQQLEx5uU2hpZWxkIFRTUyBFU046NTUxQS0wNUUwLUQ5NDcxJTAjBgNV
# BAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2UwggIiMA0GCSqGSIb3DQEB
# AQUAA4ICDwAwggIKAoICAQC1at/4fMO7uyTnTLlgeF4IgkbS7FFIUVwc16T5N31m
# bImPbQQSNpTwkMm7mlZzik8CwEjfw0QAnVv4oGeVK2pTy69cXiqcRKEN2Lyc+xll
# CxNkMpYCBQzaJlM6JYi5lwWzlkLz/4aWtUszmHVj2d8yHkHgOdRA5cyt6YBP0yS9
# SGDe5piCaouWZjI4OZtriVdkG7XThIsAWxc5+X9MuGlOhPjrLuUj2xsj26rf8B6u
# Hdo+LaSce8QRrOKVd6ihc0sLB274izqjyRAui5SfcrBRCbRvtpS2y/Vf86A+aw4m
# LrI3cthbIchK+s24isniJg2Ad0EG6ZBgrwuNmZBpMoVpzGGZcnSraDNoh/EXbIjA
# z5X2xCqQeSD9D6JIM2kyvqav87CSc4QiMjSDpkw7KaK+kKHMM2g/P2GQreBUdkpb
# s1Xz5QFc3gbRoFfr18pRvEEEvKTZwL4+E6hSOSXpQLz9zSG6qPnFfyb5hUiTzV7u
# 3oj5X8TjJdF55mCvQWFio2m9OMZxo7ZauQ/leaxhLsi8w8h/gMLIglRlqqgExOgA
# kkcZF74M+oIeDpuYY+b3sys5a/Xr8KjpL1xAORen28xJJFBZfLgq0mFl+a4PPa+v
# WPDg16LHC4gMbDWa1X9N1Ij6+ksl9SIuX9v3D+0kH3YEAtBPx7Vgfw2mF06jXELC
# RwIDAQABo4IBSTCCAUUwHQYDVR0OBBYEFLByr1uWoug8+JWvKb2YWYVZvLJSMB8G
# A1UdIwQYMBaAFJ+nFV0AXmJdg/Tl0mWnG1M1GelyMF8GA1UdHwRYMFYwVKBSoFCG
# Tmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvY3JsL01pY3Jvc29mdCUy
# MFRpbWUtU3RhbXAlMjBQQ0ElMjAyMDEwKDEpLmNybDBsBggrBgEFBQcBAQRgMF4w
# XAYIKwYBBQUHMAKGUGh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvY2Vy
# dHMvTWljcm9zb2Z0JTIwVGltZS1TdGFtcCUyMFBDQSUyMDIwMTAoMSkuY3J0MAwG
# A1UdEwEB/wQCMAAwFgYDVR0lAQH/BAwwCgYIKwYBBQUHAwgwDgYDVR0PAQH/BAQD
# AgeAMA0GCSqGSIb3DQEBCwUAA4ICAQA6NADLPRxXO1MUapdfkktHEUr87+gx7nm4
# OoQLxV3WBtwzbwoFQ+C9Qg9eb+90M3YhUGRYebAKngAhzLh1m2S5SZ3R+e7ppP0y
# +jWd2wunZglwygUsS3dO2uIto76Lgau/RlQu1ZdQ8Bb8yflJyOCfTFl24Y8EP9ez
# cnv6B6337xm8GKmyD83umiKZg5WwfEtx6btXld0w2zK1Ob+4KiaEz/CBHkqUNhNU
# 0BcHFxIox4lqIPdXX4eE2RWWIyXlU4/6fDnFYXnm72Hp4XYbt4E+pP6pIVD6tAJB
# 0is3TIbVA308muiC4r4UlAl1DN18PdFZWxyIHKBthpmVPVwYkjUjJDvgNDRQF1Ol
# 94azKsRD08jxDKpUupvarsu0joMkw2mFi76Ov//SymvVRW/IM+25GdsZBE2LUI7A
# lyP05iaWQWAo14J9sNPtTe4Q69aiZ6RfrRj+bm61FxQ9V4A92GQH4PENp6/cnXLA
# M13K73XWcBU+BGTIqAwrdRIsbfsR2Vq0OTwXK4KvHi2IfKoc7fATrE/DfZDx7++a
# 5A+gFm5fepR6gUizJkR6cerZJwy6eFypbfZJRUCLmhnhned/t0CA1q7bU0Q/CBb7
# bCSs2oODsenzIfKg4puAQG7pERBu9J9nkqHg9X5LaDF/a6roahgOeWoAE4xjDPfT
# 0hKLRs8yHzCCB3EwggVZoAMCAQICEzMAAAAVxedrngKbSZkAAAAAABUwDQYJKoZI
# hvcNAQELBQAwgYgxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAw
# DgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24x
# MjAwBgNVBAMTKU1pY3Jvc29mdCBSb290IENlcnRpZmljYXRlIEF1dGhvcml0eSAy
# MDEwMB4XDTIxMDkzMDE4MjIyNVoXDTMwMDkzMDE4MzIyNVowfDELMAkGA1UEBhMC
# VVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNV
# BAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRp
# bWUtU3RhbXAgUENBIDIwMTAwggIiMA0GCSqGSIb3DQEBAQUAA4ICDwAwggIKAoIC
# AQDk4aZM57RyIQt5osvXJHm9DtWC0/3unAcH0qlsTnXIyjVX9gF/bErg4r25Phdg
# M/9cT8dm95VTcVrifkpa/rg2Z4VGIwy1jRPPdzLAEBjoYH1qUoNEt6aORmsHFPPF
# dvWGUNzBRMhxXFExN6AKOG6N7dcP2CZTfDlhAnrEqv1yaa8dq6z2Nr41JmTamDu6
# GnszrYBbfowQHJ1S/rboYiXcag/PXfT+jlPP1uyFVk3v3byNpOORj7I5LFGc6XBp
# Dco2LXCOMcg1KL3jtIckw+DJj361VI/c+gVVmG1oO5pGve2krnopN6zL64NF50Zu
# yjLVwIYwXE8s4mKyzbnijYjklqwBSru+cakXW2dg3viSkR4dPf0gz3N9QZpGdc3E
# XzTdEonW/aUgfX782Z5F37ZyL9t9X4C626p+Nuw2TPYrbqgSUei/BQOj0XOmTTd0
# lBw0gg/wEPK3Rxjtp+iZfD9M269ewvPV2HM9Q07BMzlMjgK8QmguEOqEUUbi0b1q
# GFphAXPKZ6Je1yh2AuIzGHLXpyDwwvoSCtdjbwzJNmSLW6CmgyFdXzB0kZSU2LlQ
# +QuJYfM2BjUYhEfb3BvR/bLUHMVr9lxSUV0S2yW6r1AFemzFER1y7435UsSFF5PA
# PBXbGjfHCBUYP3irRbb1Hode2o+eFnJpxq57t7c+auIurQIDAQABo4IB3TCCAdkw
# EgYJKwYBBAGCNxUBBAUCAwEAATAjBgkrBgEEAYI3FQIEFgQUKqdS/mTEmr6CkTxG
# NSnPEP8vBO4wHQYDVR0OBBYEFJ+nFV0AXmJdg/Tl0mWnG1M1GelyMFwGA1UdIARV
# MFMwUQYMKwYBBAGCN0yDfQEBMEEwPwYIKwYBBQUHAgEWM2h0dHA6Ly93d3cubWlj
# cm9zb2Z0LmNvbS9wa2lvcHMvRG9jcy9SZXBvc2l0b3J5Lmh0bTATBgNVHSUEDDAK
# BggrBgEFBQcDCDAZBgkrBgEEAYI3FAIEDB4KAFMAdQBiAEMAQTALBgNVHQ8EBAMC
# AYYwDwYDVR0TAQH/BAUwAwEB/zAfBgNVHSMEGDAWgBTV9lbLj+iiXGJo0T2UkFvX
# zpoYxDBWBgNVHR8ETzBNMEugSaBHhkVodHRwOi8vY3JsLm1pY3Jvc29mdC5jb20v
# cGtpL2NybC9wcm9kdWN0cy9NaWNSb29DZXJBdXRfMjAxMC0wNi0yMy5jcmwwWgYI
# KwYBBQUHAQEETjBMMEoGCCsGAQUFBzAChj5odHRwOi8vd3d3Lm1pY3Jvc29mdC5j
# b20vcGtpL2NlcnRzL01pY1Jvb0NlckF1dF8yMDEwLTA2LTIzLmNydDANBgkqhkiG
# 9w0BAQsFAAOCAgEAnVV9/Cqt4SwfZwExJFvhnnJL/Klv6lwUtj5OR2R4sQaTlz0x
# M7U518JxNj/aZGx80HU5bbsPMeTCj/ts0aGUGCLu6WZnOlNN3Zi6th542DYunKmC
# VgADsAW+iehp4LoJ7nvfam++Kctu2D9IdQHZGN5tggz1bSNU5HhTdSRXud2f8449
# xvNo32X2pFaq95W2KFUn0CS9QKC/GbYSEhFdPSfgQJY4rPf5KYnDvBewVIVCs/wM
# nosZiefwC2qBwoEZQhlSdYo2wh3DYXMuLGt7bj8sCXgU6ZGyqVvfSaN0DLzskYDS
# PeZKPmY7T7uG+jIa2Zb0j/aRAfbOxnT99kxybxCrdTDFNLB62FD+CljdQDzHVG2d
# Y3RILLFORy3BFARxv2T5JL5zbcqOCb2zAVdJVGTZc9d/HltEAY5aGZFrDZ+kKNxn
# GSgkujhLmm77IVRrakURR6nxt67I6IleT53S0Ex2tVdUCbFpAUR+fKFhbHP+Crvs
# QWY9af3LwUFJfn6Tvsv4O+S3Fb+0zj6lMVGEvL8CwYKiexcdFYmNcP7ntdAoGokL
# jzbaukz5m/8K6TT4JDVnK+ANuOaMmdbhIurwJ0I9JZTmdHRbatGePu1+oDEzfbzL
# 6Xu/OHBE0ZDxyKs6ijoIYn/ZcGNTTY3ugm2lBRDBcQZqELQdVTNYs6FwZvKhggNZ
# MIICQQIBATCCAQGhgdmkgdYwgdMxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNo
# aW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29y
# cG9yYXRpb24xLTArBgNVBAsTJE1pY3Jvc29mdCBJcmVsYW5kIE9wZXJhdGlvbnMg
# TGltaXRlZDEnMCUGA1UECxMeblNoaWVsZCBUU1MgRVNOOjU1MUEtMDVFMC1EOTQ3
# MSUwIwYDVQQDExxNaWNyb3NvZnQgVGltZS1TdGFtcCBTZXJ2aWNloiMKAQEwBwYF
# Kw4DAhoDFQDX7bpxH/IfXTQOI0UZaG4C/atgGqCBgzCBgKR+MHwxCzAJBgNVBAYT
# AlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYD
# VQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBU
# aW1lLVN0YW1wIFBDQSAyMDEwMA0GCSqGSIb3DQEBCwUAAgUA7BqYADAiGA8yMDI1
# MDcxMDE5NTAyNFoYDzIwMjUwNzExMTk1MDI0WjB3MD0GCisGAQQBhFkKBAExLzAt
# MAoCBQDsGpgAAgEAMAoCAQACAjcaAgH/MAcCAQACAhOpMAoCBQDsG+mAAgEAMDYG
# CisGAQQBhFkKBAIxKDAmMAwGCisGAQQBhFkKAwKgCjAIAgEAAgMHoSChCjAIAgEA
# AgMBhqAwDQYJKoZIhvcNAQELBQADggEBALDOjxYHHs+K0Tx2+OaEZhgw7Dcz1cwr
# /FqSWfOlgEj7QYTFIB0pbZ3AhOc5uNDWuZ8vePh/7ZWNEn6neu2GJwJRtWGdrBe3
# fuYg3nkGEijXNNl5X5TIc63r4fe+W2mpLLiQKHsfkhXl1lSO04WoN9xvT0kFv5VQ
# FjIAfBr2CidsT0C+BJ1J8wsu2/cPxPxUFHYQ/Aq/EIh0UoBdbIgf8dP7ZWhjKHPN
# pLOLXOggXv3880MFrcZ+79ZIvkDaOzOUn3Fj1z5+m2aW+jU4OJUtkRtuXMB+unEV
# 8Vw5beS4vbF3fQQ1EgA5eLe0Q1L3ElNLZfKn9gpQUdYgIm5/v62sYmoxggQNMIIE
# CQIBATCBkzB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4G
# A1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYw
# JAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMAITMwAAAgHRRVmY
# EMxCTwABAAACATANBglghkgBZQMEAgEFAKCCAUowGgYJKoZIhvcNAQkDMQ0GCyqG
# SIb3DQEJEAEEMC8GCSqGSIb3DQEJBDEiBCACJm5//ixd6lY6iP2mQB7SjS2DayZ0
# jIPEk01u7aUPDjCB+gYLKoZIhvcNAQkQAi8xgeowgecwgeQwgb0EIFhrsjpMlBFy
# bHQdpJNZl0mCjB2uX35muvSkh2oe1zgjMIGYMIGApH4wfDELMAkGA1UEBhMCVVMx
# EzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoT
# FU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUt
# U3RhbXAgUENBIDIwMTACEzMAAAIB0UVZmBDMQk8AAQAAAgEwIgQgO/v/PmVyn+9Z
# oFDy2DTshONl8nTILmlrmp/DydYpzr8wDQYJKoZIhvcNAQELBQAEggIAAiEM/Ylf
# uwlXPotMZBJR94PENmWsChQtcgxxiuhC5EEEQOA53wAWV5ToSNXTbVJembRggCKR
# sp+EiuOpZEDkJ9uaI2kqh3Z4RtiY5omRMNH5pzc57kDuUQsWmNX43PYS4Buq/LV+
# iPKY+X2ajY1orxArpaVSoS+gnV1O43Vfb0hrT6iLskljQqscfAC0UO3bYIs1MFLE
# b9+wgYIpgE6HWEHNEEr0G8sfRXwKlhDaBqeUtbbTIMg4UvUm7K4qbDIm56cRebmI
# 7cUcBuy37ELHwQ7HZGxZJQkMBvQCHaW2oSIQhMB4683NGG0pc0tjuHSck8Edg9Zx
# xWC8ZPOfGHL22qGBnNoRErtxLrRk7lBDGJyFGNb5e3VrJuKRQHNNBiYyBtbZxTn5
# kowReH/TDDC8f/usuk/YpIPivNzko53cJcOc6QOnlt7nP02D1v1TXKNk/SYjTwPq
# /1127qcbONSU4Y6y5QIYxqGkm4066dHzb3D22OisYz5zKMzP3BI50wSgb3gVYmvU
# xbIDAyqo/Qa/+DZjoTsEL2mMG/X7B7d8KYr7soziwfJq1Kc4LihAQ8+ej3cNPGzd
# 974Pri2otJoEySZ4T5MoDteMuYBkR3FaKlt/lP2JKMvWyxXrL4+CV5Pv31Ovvos4
# OEEZTc7o0seGtNUxGlxdeGfF/5XozhsRMck=
# SIG # End signature block

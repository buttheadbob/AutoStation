# Run by build.bat before every build. Increments the build number (fourth part) of the version in
# AssemblyVersion.cs, which the Torch and Magnetar projects compile, and copies it into the Torch manifest.
# To start a new release, edit the first three parts in AssemblyVersion.cs by hand (for example 1.1.0.0).
$versionFile = "$PSScriptRoot/AssemblyVersion.cs"
$manifestPath = "$PSScriptRoot/../TorchPlugin/manifest.xml"

$major = 1; $minor = 0; $patch = 0; $build = 0
if (Test-Path $versionFile) {
    $match = [regex]::Match([System.IO.File]::ReadAllText($versionFile), 'AssemblyVersion\("([0-9]+)\.([0-9]+)\.([0-9]+)\.([0-9]+)"\)')
    if ($match.Success) {
        $major = [int]$match.Groups[1].Value
        $minor = [int]$match.Groups[2].Value
        $patch = [int]$match.Groups[3].Value
        $build = [int]$match.Groups[4].Value
    } else {
        throw "Could not read the version from $versionFile"
    }
}
$build++

$dotNetVersion = "$major.$minor.$patch.$build"
$infoVersion = "v$dotNetVersion"

$fileContent = @"
using System.Reflection;

[assembly: AssemblyVersion("$dotNetVersion")]
[assembly: AssemblyFileVersion("$dotNetVersion")]
[assembly: AssemblyInformationalVersion("$infoVersion")]

"@

[System.IO.File]::WriteAllText($versionFile, $fileContent)

$manifest = [System.IO.File]::ReadAllText($manifestPath)
[System.IO.File]::WriteAllText($manifestPath, ($manifest -replace "<Version>.*?</Version>", "<Version>$infoVersion</Version>"))

echo "$infoVersion"

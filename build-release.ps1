# Builds EarTrumpet (Release|x86) with Visual Studio Build Tools, without the .NET Framework 4.6.2 targeting pack
# installed: the reference assemblies come from NuGet into .tools\.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$refRoot = Join-Path $root '.tools\ref462'
if (-not (Test-Path (Join-Path $refRoot 'build\.NETFramework\v4.6.2'))) {
    New-Item -ItemType Directory -Force (Join-Path $root '.tools') | Out-Null
    $nupkg = Join-Path $root '.tools\ref462.zip'
    Invoke-WebRequest 'https://www.nuget.org/api/v2/package/Microsoft.NETFramework.ReferenceAssemblies.net462/1.0.3' -OutFile $nupkg
    Expand-Archive $nupkg $refRoot -Force
}
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.Roslyn.Compiler -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild with the C# compiler not found' }
$proj = Join-Path $root 'EarTrumpet\EarTrumpet.csproj'
& $msbuild $proj -t:restore -p:RestorePackagesConfig=true -p:SolutionDir="$root\" -v:m -nologo
if ($LASTEXITCODE) { throw 'restore failed' }
& $msbuild $proj -p:Configuration=Release -p:Platform=x86 -p:SolutionDir="$root\" "-p:TargetFrameworkRootPath=$refRoot\build\" -v:m -nologo @args
if ($LASTEXITCODE) { throw 'build failed' }
Write-Host "Built: $root\Build\Release\EarTrumpet.exe"

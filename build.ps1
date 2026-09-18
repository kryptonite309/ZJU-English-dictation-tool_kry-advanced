param(
    [string]$OutputDirectory = "..\_build"
)

$ErrorActionPreference = "Stop"
$sourceDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputDirectoryPath = [System.IO.Path]::GetFullPath((Join-Path $sourceDirectory $OutputDirectory))
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "未找到 C# 编译器: $compiler"
}

$speechAssemblyFolder = "$env:WINDIR\Microsoft.NET\assembly\GAC_MSIL\System.Speech"
$speechAssembly = Get-ChildItem -LiteralPath $speechAssemblyFolder -Filter System.Speech.dll `
    -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $speechAssembly) {
    throw "未找到 Windows .NET Framework 的 System.Speech.dll"
}

New-Item -ItemType Directory -Force -Path $outputDirectoryPath | Out-Null

& $compiler `
    /nologo `
    /target:winexe `
    /platform:x64 `
    /optimize+ `
    /win32manifest:"$sourceDirectory\app.manifest" `
    /out:"$outputDirectoryPath\main.exe" `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:"$($speechAssembly.FullName)" `
    /reference:System.Windows.Forms.dll `
    /reference:System.Web.Extensions.dll `
    "$sourceDirectory\Main.cs" `
    "$sourceDirectory\ModernUI.cs" `
    "$sourceDirectory\NotebookStore.cs" `
    "$sourceDirectory\NotebookManagerForm.cs" `
    "$sourceDirectory\NotebookTests.cs" `
    "$sourceDirectory\StudyStore.cs" `
    "$sourceDirectory\Appearance.cs" `
    "$sourceDirectory\AppearanceSettingsForm.cs" `
    "$sourceDirectory\StudyCalendar.cs" `
    "$sourceDirectory\StudyForms.cs" `
    "$sourceDirectory\BackupService.cs" `
    "$sourceDirectory\StudySettingsForm.cs" `
    "$sourceDirectory\StudyTests.cs" `
    "$sourceDirectory\Pronunciation.cs"

if ($LASTEXITCODE -ne 0) {
    throw "构建失败，退出代码: $LASTEXITCODE"
}

Get-Item -LiteralPath "$outputDirectoryPath\main.exe"

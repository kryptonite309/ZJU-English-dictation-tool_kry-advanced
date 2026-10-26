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

function Find-FrameworkAssembly([string]$name) {
    $assembly = Get-ChildItem -LiteralPath "$env:WINDIR\Microsoft.NET\assembly" `
        -Filter $name -Recurse -File -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $assembly) { throw "未找到 Windows .NET Framework 组件: $name" }
    return $assembly.FullName
}

$windowsBase = Find-FrameworkAssembly "WindowsBase.dll"
$presentationCore = Find-FrameworkAssembly "PresentationCore.dll"
$presentationFramework = Find-FrameworkAssembly "PresentationFramework.dll"
$windowsFormsIntegration = Find-FrameworkAssembly "WindowsFormsIntegration.dll"
$systemXaml = Find-FrameworkAssembly "System.Xaml.dll"

New-Item -ItemType Directory -Force -Path $outputDirectoryPath | Out-Null

& $compiler `
    /nologo `
    /target:winexe `
    /platform:x64 `
    /optimize+ `
    /win32manifest:"$sourceDirectory\app.manifest" `
    /win32icon:"$sourceDirectory\app.ico" `
    /out:"$outputDirectoryPath\main.exe" `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.IO.Compression.dll `
    /reference:System.IO.Compression.FileSystem.dll `
    /reference:"$($speechAssembly.FullName)" `
    /reference:System.Windows.Forms.dll `
    /reference:System.Web.Extensions.dll `
    /reference:"$windowsBase" `
    /reference:"$presentationCore" `
    /reference:"$presentationFramework" `
    /reference:"$windowsFormsIntegration" `
    /reference:"$systemXaml" `
    "$sourceDirectory\Main.cs" `
    "$sourceDirectory\ModernUI.cs" `
    "$sourceDirectory\HomeDashboard.cs" `
    "$sourceDirectory\OnboardingGuide.cs" `
    "$sourceDirectory\VideoBackground.cs" `
    "$sourceDirectory\NotebookStore.cs" `
    "$sourceDirectory\NotebookManagerForm.cs" `
    "$sourceDirectory\FavoriteForms.cs" `
    "$sourceDirectory\FavoriteFolderTests.cs" `
    "$sourceDirectory\NotebookTests.cs" `
    "$sourceDirectory\StudyStore.cs" `
    "$sourceDirectory\Appearance.cs" `
    "$sourceDirectory\AppearanceSettingsForm.cs" `
    "$sourceDirectory\StudyCalendar.cs" `
    "$sourceDirectory\StudyForms.cs" `
    "$sourceDirectory\BackupService.cs" `
    "$sourceDirectory\StudySettingsForm.cs" `
    "$sourceDirectory\StudyTests.cs" `
    "$sourceDirectory\Pronunciation.cs" `
    "$sourceDirectory\LearningSupport.cs" `
    "$sourceDirectory\PracticeStore.cs" `
    "$sourceDirectory\StatisticsForm.cs" `
    "$sourceDirectory\UpdateService.cs" `
    "$sourceDirectory\V120Tests.cs" `
    "$sourceDirectory\SessionFeedback.cs" `
    "$sourceDirectory\V131Tests.cs" `
    "$sourceDirectory\V134Tests.cs" `
    "$sourceDirectory\V135Tests.cs" `
    "$sourceDirectory\V136Tests.cs" `
    "$sourceDirectory\V137Tests.cs" `
    "$sourceDirectory\V140Tests.cs"

if ($LASTEXITCODE -ne 0) {
    throw "构建失败，退出代码: $LASTEXITCODE"
}

Get-Item -LiteralPath "$outputDirectoryPath\main.exe"

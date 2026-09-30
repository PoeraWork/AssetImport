#requires -Version 5.1
<#
仅本地收集：指定日志、本插件配置、插件 DLL 元数据。不会上传，不改游戏设置。
例：powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Collect-AssetImportLogs.ps1 -GameRoot "D:\Games\Koikatu" -RunLabel "ME403-FBX"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$GameRoot,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'CollectedLogs'),
    [ValidatePattern('^[A-Za-z0-9_.-]{1,48}$')]
    [string]$RunLabel = 'manual'
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$warnings = New-Object 'System.Collections.Generic.List[string]'
$copied = New-Object 'System.Collections.Generic.List[object]'
$inventory = New-Object 'System.Collections.Generic.List[object]'

$root = (Resolve-Path -LiteralPath $GameRoot).ProviderPath
if (-not (Test-Path -LiteralPath (Join-Path $root 'BepInEx') -PathType Container)) {
    throw '游戏根目录中没有 BepInEx 文件夹；请传入包含 Koikatu.exe 或 CharaStudio.exe 的游戏文件夹。'
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($output) | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$bundleName = 'AssetImport-Logs-' + $RunLabel + '-' + $stamp + '-' + ([Guid]::NewGuid().ToString('N').Substring(0, 6))
$bundle = Join-Path $output $bundleName
[IO.Directory]::CreateDirectory($bundle) | Out-Null

function Relative-GamePath([string]$Path) {
    return $Path.Substring($root.Length).TrimStart([char[]]'\/')
}
function Copy-AllowlistedFile([string]$RelativePath) {
    $source = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        $copied.Add([pscustomobject]@{ Path = $RelativePath; Status = '不存在'; Bytes = 0; SourceLastWriteUtc = '' })
        return
    }
    # 保留相对目录，绝不递归复制配置或 UserData。
    $destination = Join-Path $bundle $RelativePath
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    $inputStream = $null
    $outputStream = $null
    try {
        $info = Get-Item -LiteralPath $source
        $inputStream = [IO.File]::Open($source, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
        $outputStream = [IO.File]::Open($destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        $inputStream.CopyTo($outputStream)
        $bytes = $outputStream.Length
        $copied.Add([pscustomobject]@{ Path = $RelativePath; Status = '已复制'; Bytes = $bytes; SourceLastWriteUtc = $info.LastWriteTimeUtc.ToString('o') })
    }
    catch {
        $warnings.Add('复制失败：' + $RelativePath + '；' + $_.Exception.Message)
        $copied.Add([pscustomobject]@{ Path = $RelativePath; Status = '失败'; Bytes = 0; SourceLastWriteUtc = '' })
    }
    finally {
        if ($null -ne $outputStream) { $outputStream.Dispose() }
        if ($null -ne $inputStream) { $inputStream.Dispose() }
    }
}

@(
    'BepInEx\LogOutput.log',
    'output_log.txt',
    'Koikatu_Data\output_log.txt',
    'CharaStudio_Data\output_log.txt',
    'BepInEx\config\org.njaecha.plugins.assetimport.cfg'
) | ForEach-Object { Copy-AllowlistedFile $_ }

$pluginFolder = Join-Path $root 'BepInEx\plugins'
$files = @()
if (Test-Path -LiteralPath $pluginFolder -PathType Container) {
    $files += @(Get-ChildItem -LiteralPath $pluginFolder -Recurse -Filter '*.dll' -File)
}
foreach ($relative in @('BepInEx\core\BepInEx.dll', 'BepInEx\core\0Harmony.dll', 'Koikatu.exe', 'CharaStudio.exe', 'runtimes\win-x64\native\assimp.dll')) {
    $path = Join-Path $root $relative
    if (Test-Path -LiteralPath $path -PathType Leaf) { $files += Get-Item -LiteralPath $path }
}
foreach ($file in $files) {
    $assemblyName = ''
    $assemblyVersion = ''
    $kind = '非托管或不可读程序集'
    try {
        # GetAssemblyName 只读程序集标识，不会 Assembly.Load 或运行 DLL。
        $metadata = [Reflection.AssemblyName]::GetAssemblyName($file.FullName)
        $assemblyName = $metadata.Name
        $assemblyVersion = $metadata.Version.ToString()
        $kind = '托管'
    }
    catch { }
    $fileVersion = ''
    try { $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($file.FullName).FileVersion } catch { }
    $hash = ''
    if (($assemblyName + ' ' + $file.Name) -match '(?i)AssetImport|MaterialEditor|KKAPI|BepInEx|0Harmony|Assimp|ABMX|KKPE|KKSPE|DynamicBoneEditor|LoadFileLi?mitedFix') {
        try { $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
        catch { $warnings.Add('无法计算校验值：' + (Relative-GamePath $file.FullName)) }
    }
    $inventory.Add([pscustomobject]@{
        Path = (Relative-GamePath $file.FullName)
        AssemblyName = $assemblyName
        AssemblyVersion = $assemblyVersion
        FileVersion = $fileVersion
        Kind = $kind
        SHA256 = $hash
    })
}

$managed = @($inventory | Where-Object { $_.AssemblyName -ne '' })
foreach ($group in @($managed | Group-Object AssemblyName | Where-Object { $_.Count -gt 1 })) {
    $warnings.Add('同名程序集有多份，请检查是否重复安装（脚本未移动文件）：' + $group.Name + ' => ' + (($group.Group | ForEach-Object { $_.Path }) -join ' ; '))
}
$materialEditors = @($managed | Where-Object { $_.AssemblyName -eq 'KK_MaterialEditor' })
if ($materialEditors.Count -ne 1) { $warnings.Add('预期恰好一份 KK_MaterialEditor.dll，检测到 ' + $materialEditors.Count + ' 份。重命名DLL也不会隔离版本。') }
$assetImports = @($managed | Where-Object { $_.AssemblyName -eq 'KK_AssetImport' -or $_.AssemblyName -eq 'AssetImport' })
if ($assetImports.Count -ne 1) { $warnings.Add('预期恰好一份 KK AssetImport，检测到 ' + $assetImports.Count + ' 份。') }
foreach ($item in @($managed | Where-Object { $_.AssemblyName -match '^KKS' })) {
    $warnings.Add('KK 环境中发现 KKS 程序集，请核对：' + $item.Path)
}
$activeGames = @(Get-Process -Name 'Koikatu', 'CharaStudio' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty ProcessName -Unique)
if ($activeGames.Count -gt 0) { $warnings.Add('游戏仍在运行，日志可能继续变化：' + ($activeGames -join ', ') + '。请在本次出错后、再次启动游戏前收集。脚本不会关闭游戏。') }
if (-not (Test-Path -LiteralPath (Join-Path $root 'BepInEx\LogOutput.log') -PathType Leaf)) {
    $warnings.Add('缺少 BepInEx/LogOutput.log：可查看已收集的根目录 output_log.txt 或 *_Data/output_log.txt；主日志缺失本身不代表插件失败。脚本不会修改日志配置。')
}

$inventory | Sort-Object Path | Export-Csv -LiteralPath (Join-Path $bundle 'PluginInventory.csv') -NoTypeInformation -Encoding UTF8
$copied | Export-Csv -LiteralPath (Join-Path $bundle 'CollectedFiles.csv') -NoTypeInformation -Encoding UTF8
$envLines = @(
    ('RunLabel=' + $RunLabel),
    ('CollectedLocal=' + [DateTimeOffset]::Now.ToString('o')),
    ('CollectedUtc=' + [DateTimeOffset]::UtcNow.ToString('o')),
    ('TimeZone=' + [TimeZoneInfo]::Local.Id),
    ('OS=' + [Environment]::OSVersion.VersionString),
    ('OS64Bit=' + [Environment]::Is64BitOperatingSystem),
    ('PowerShell=' + $PSVersionTable.PSVersion.ToString()),
    ('PowerShell64Bit=' + [Environment]::Is64BitProcess),
    ('RunningGameNames=' + ($activeGames -join ', ')),
    ('DetectedMaterialEditor=' + (($materialEditors | ForEach-Object { $_.AssemblyVersion }) -join ', ')),
    ('DetectedAssetImport=' + (($assetImports | ForEach-Object { $_.AssemblyVersion }) -join ', ')),
    '',
    '未记录Windows用户名或电脑名；未复制DLL、角色卡、服装卡、场景卡、模型、贴图或其他配置。',
    '原始日志和AssetImport配置可能包含文件路径或卡片名称；发送压缩包前请自行查看。',
    '未执行任何网络请求或上传。'
)
$envLines | Set-Content -LiteralPath (Join-Path $bundle 'Environment.txt') -Encoding UTF8
if ($warnings.Count -eq 0) { $warnings.Add('未发现上述安装项异常；这不代表游戏内测试通过。') }
$warnings | Set-Content -LiteralPath (Join-Path $bundle 'Warnings.txt') -Encoding UTF8

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipPath = Join-Path $output ($bundleName + '.zip')
[IO.Compression.ZipFile]::CreateFromDirectory($bundle, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $false)
Write-Host ''
Write-Host ('已生成本地压缩包：' + $zipPath)
Write-Host '请先查看 Warnings.txt 和压缩包内容，再手动回传。脚本没有上传任何文件。'

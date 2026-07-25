param([Parameter(Mandatory=$true)][string]$PortFile)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

$script:SelectedPaths = @{}
$script:SelectedEncodings = @{}
$script:LastRequestAt = [DateTime]::UtcNow
$script:ShutdownRequested = $false
$script:WebRoot = Join-Path $PSScriptRoot 'web'
$script:StaticFiles = @('index.html', 'styles.css', 'app.js')

function Get-FileEncoding {
    param([string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        return [System.Text.UTF8Encoding]::new($true)
    }
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        return [System.Text.UnicodeEncoding]::new($false, $true)
    }
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
        return [System.Text.BigEndianUnicodeEncoding]::new($true)
    }
    [System.Text.UTF8Encoding]::new($false)
}

function Read-LuaFile {
    param([string]$Path)

    $encoding = Get-FileEncoding $Path
    [pscustomobject]@{
        Name = [System.IO.Path]::GetFileName($Path)
        Content = [System.IO.File]::ReadAllText($Path, $encoding)
        Encoding = $encoding
    }
}

function Save-LuaFile {
    param([string]$Path, [string]$Content, [System.Text.Encoding]$Encoding)

    $tempPath = "$Path.writing.$([guid]::NewGuid().ToString('N').Substring(0, 8))"
    try {
        [System.IO.File]::WriteAllText($tempPath, $Content, $Encoding)
        Move-Item -Force $tempPath $Path
    } catch {
        if (Test-Path -LiteralPath $tempPath) { Remove-Item -Force $tempPath -ErrorAction SilentlyContinue }
        throw
    }
}

function Send-Json {
    param($Response, [int]$StatusCode, $Value)

    $data = [System.Text.Encoding]::UTF8.GetBytes(($Value | ConvertTo-Json -Compress -Depth 5))
    $Response.StatusCode = $StatusCode
    $Response.ContentType = 'application/json; charset=utf-8'
    $Response.ContentLength64 = $data.Length
    $Response.OutputStream.Write($data, 0, $data.Length)
    $Response.Close()
}

function Send-StaticFile {
    param($Response, [string]$FileName)

    if ($script:StaticFiles -notcontains $FileName) {
        $Response.StatusCode = 404
        $Response.Close()
        return
    }
    $data = [System.IO.File]::ReadAllBytes((Join-Path $script:WebRoot $FileName))
    $Response.ContentType = if ($FileName -eq 'index.html') { 'text/html; charset=utf-8' } elseif ($FileName -eq 'styles.css') { 'text/css; charset=utf-8' } else { 'application/javascript; charset=utf-8' }
    $Response.ContentLength64 = $data.Length
    $Response.OutputStream.Write($data, 0, $data.Length)
    $Response.Close()
}

function Get-RequestBody {
    param($Request)

    $reader = New-Object System.IO.StreamReader($Request.InputStream, $Request.ContentEncoding)
    try { $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
}

function Select-ConfigFiles {
    $keyDialog = New-Object System.Windows.Forms.OpenFileDialog
    $keyDialog.Title = '选择第一个配置文件（按键配置）'
    $keyDialog.Filter = 'Lua 文件 (*.lua)|*.lua|所有文件 (*.*)|*.*'
    $keyDialog.InitialDirectory = 'C:\'
    if ($keyDialog.ShowDialog() -ne 'OK') { return $null }

    $sensitivityDialog = New-Object System.Windows.Forms.OpenFileDialog
    $sensitivityDialog.Title = '选择第二个配置文件（灵敏度配置）'
    $sensitivityDialog.Filter = 'Lua 文件 (*.lua)|*.lua|所有文件 (*.*)|*.*'
    $sensitivityDialog.InitialDirectory = 'C:\'
    if ($sensitivityDialog.ShowDialog() -ne 'OK') { return $null }
    if ($keyDialog.FileName -eq $sensitivityDialog.FileName) { throw '请为两个配置角色选择不同的文件。' }

    $keyFile = Read-LuaFile $keyDialog.FileName
    $sensitivityFile = Read-LuaFile $sensitivityDialog.FileName
    $script:SelectedPaths = @{ KeyBindings=$keyDialog.FileName; Sensitivity=$sensitivityDialog.FileName }
    $script:SelectedEncodings = @{ KeyBindings=$keyFile.Encoding; Sensitivity=$sensitivityFile.Encoding }
    @{
        keyBindings = @{ name=$keyFile.Name; content=$keyFile.Content }
        sensitivity = @{ name=$sensitivityFile.Name; content=$sensitivityFile.Content }
    }
}

$listener = [System.Net.HttpListener]::new()
$tcp = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$tcp.Start()
$port = ([System.Net.IPEndPoint]$tcp.LocalEndpoint).Port
$tcp.Stop()
$listener.Prefixes.Add("http://127.0.0.1:$port/")
$listener.Start()
[System.IO.File]::WriteAllText($PortFile, $port.ToString(), [System.Text.Encoding]::ASCII)

try {
    $pending = $listener.BeginGetContext($null, $null)
    while (!$script:ShutdownRequested) {
        if (([DateTime]::UtcNow - $script:LastRequestAt) -gt [TimeSpan]::FromMinutes(15)) { break }
        if (!$pending.AsyncWaitHandle.WaitOne(1000)) { continue }
        $context = $listener.EndGetContext($pending)
        $pending = $listener.BeginGetContext($null, $null)
        $script:LastRequestAt = [DateTime]::UtcNow
        $request = $context.Request
        $path = $request.Url.AbsolutePath
        try {
            if ($path -eq '/api/status' -and $request.HttpMethod -eq 'GET') {
                $idleSeconds = [Math]::Max(0, [int](900 - (([DateTime]::UtcNow - $script:LastRequestAt).TotalSeconds)))
                Send-Json $context.Response 200 @{ available=$true; hasSelectedFiles=($script:SelectedPaths.Count -eq 2); idleSecondsRemaining=$idleSeconds }
            } elseif ($path -eq '/api/select-files' -and $request.HttpMethod -eq 'POST') {
                $selection = Select-ConfigFiles
                if ($null -eq $selection) { Send-Json $context.Response 200 @{ cancelled=$true } }
                else { Send-Json $context.Response 200 @{ cancelled=$false; files=$selection } }
            } elseif ($path -eq '/api/apply' -and $request.HttpMethod -eq 'POST') {
                if ($script:SelectedPaths.Count -ne 2) { throw '请先通过服务选择两个配置文件。' }
                $body = Get-RequestBody $request
                if ($null -eq $body.keyBindingsContent -or $null -eq $body.sensitivityContent) { throw '保存请求缺少配置内容。' }
                Save-LuaFile $script:SelectedPaths.KeyBindings ([string]$body.keyBindingsContent) $script:SelectedEncodings.KeyBindings
                Save-LuaFile $script:SelectedPaths.Sensitivity ([string]$body.sensitivityContent) $script:SelectedEncodings.Sensitivity
                Send-Json $context.Response 200 @{ saved=$true }
            } elseif ($path -eq '/api/shutdown' -and $request.HttpMethod -eq 'POST') {
                Send-Json $context.Response 200 @{ stopping=$true }
                $script:ShutdownRequested = $true
            } elseif ($request.HttpMethod -eq 'GET') {
                $fileName = if ($path -eq '/') { 'index.html' } elseif ($path -eq '/styles.css') { 'styles.css' } elseif ($path -eq '/app.js') { 'app.js' } else { $null }
                if ($null -eq $fileName) { $context.Response.StatusCode = 404; $context.Response.Close() } else { Send-StaticFile $context.Response $fileName }
            } else {
                $context.Response.StatusCode = 405
                $context.Response.Close()
            }
        } catch {
            if ($context.Response.OutputStream.CanWrite) { Send-Json $context.Response 400 @{ error=$_.Exception.Message } }
        }
    }
} finally {
    if ($listener.IsListening) { $listener.Stop() }
    $listener.Close()
    if (Test-Path -LiteralPath $PortFile) { Remove-Item -Force $PortFile }
}

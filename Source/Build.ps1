param([string]$InnoCompiler = '')
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($InnoCompiler)) {
    if (Test-Path "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") {
        $InnoCompiler = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    } elseif (Test-Path 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe') {
        $InnoCompiler = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
    } else {
        throw 'Inno Setup compiler (ISCC.exe) not found.'
    }
}

dotnet run --project Tests\Tests.csproj -c Release -- --test
if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed.' }
foreach ($labRole in @('Teacher','Student')) {
    dotnet publish "$labRole\$labRole.csproj" -c Release -r win-x64 --self-contained true -o "publish\$labRole" --nologo
    if ($LASTEXITCODE -ne 0) { throw "Could not publish $labRole." }
    & $InnoCompiler "/DRole=$labRole" "/DOutputRoot=$PSScriptRoot\installers" 'setup.iss'
    if ($LASTEXITCODE -ne 0) { throw "Could not package $labRole." }
}


Write-Host "`n=== VISHWAPREMI CONNECTION DIAGNOSTIC ===" -ForegroundColor Cyan

# 1. Check if Teacher is actually listening
Write-Host "`n[1] Listening ports:" -ForegroundColor Yellow
$tcp = Get-NetTCPConnection -LocalPort 8766 -State Listen -ErrorAction SilentlyContinue
$udp = Get-NetUDPEndpoint -LocalPort 8765 -ErrorAction SilentlyContinue
if ($tcp) { Write-Host "  TCP 8766 (controller): LISTENING" -ForegroundColor Green } else { Write-Host "  TCP 8766 (controller): NOT LISTENING - Is Teacher running?" -ForegroundColor Red }
if ($udp) { Write-Host "  UDP 8765 (discovery):  LISTENING" -ForegroundColor Green } else { Write-Host "  UDP 8765 (discovery):  NOT LISTENING - Is Teacher running?" -ForegroundColor Red }

# 2. Check firewall rules
Write-Host "`n[2] Firewall rules:" -ForegroundColor Yellow
$rules = Get-NetFirewallRule -DisplayName "Vishwapremi*" -ErrorAction SilentlyContinue
if ($rules) {
    foreach ($r in $rules) {
        $port = (Get-NetFirewallPortFilter -AssociatedNetFirewallRule $r).LocalPort
        $profile = $r.Profile
        Write-Host "  $($r.DisplayName): Enabled=$($r.Enabled) Port=$port Profile=$profile" -ForegroundColor Green
    }
} else {
    Write-Host "  NO firewall rules found! Click 'Allow lab connections' in Teacher and approve the admin prompt." -ForegroundColor Red
}

# 3. Check network profile (Public vs Private)
Write-Host "`n[3] Network profiles:" -ForegroundColor Yellow
Get-NetConnectionProfile | Where-Object { $_.IPv4Connectivity -ne 'NoTraffic' } | ForEach-Object {
    $color = if ($_.NetworkCategory -eq 'Public') { 'Yellow' } else { 'Green' }
    Write-Host "  $($_.InterfaceAlias): $($_.NetworkCategory) - $($_.Name)" -ForegroundColor $color
}

# 4. Show teacher's IP addresses
Write-Host "`n[4] Teacher IP addresses:" -ForegroundColor Yellow
Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike '127.*' -and $_.PrefixOrigin -ne 'WellKnown' } | ForEach-Object {
    Write-Host "  $($_.IPAddress)/$($_.PrefixLength) on $($_.InterfaceAlias)" -ForegroundColor White
}

# 5. Quick connectivity self-test
Write-Host "`n[5] Self-test (localhost connection):" -ForegroundColor Yellow
try {
    $client = New-Object System.Net.Sockets.TcpClient
    $client.Connect('127.0.0.1', 8766)
    $client.Close()
    Write-Host "  Localhost TCP 8766: REACHABLE" -ForegroundColor Green
} catch {
    Write-Host "  Localhost TCP 8766: FAILED - Teacher controller may not be running" -ForegroundColor Red
}

Write-Host "`n=== END DIAGNOSTIC ===" -ForegroundColor Cyan
Write-Host "Run this on the STUDENT PC too:`n  Test-NetConnection 192.168.0.124 -Port 8766`n" -ForegroundColor White

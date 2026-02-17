# Quick UDP sniffer to see what the Tuner Genius is broadcasting
$udp = New-Object System.Net.Sockets.UdpClient
$udp.Client.SetSocketOption([System.Net.Sockets.SocketOptionLevel]::Socket, [System.Net.Sockets.SocketOptionName]::ReuseAddress, $true)

Write-Host "Listening for UDP broadcasts on all ports (Ctrl+C to stop)..."
Write-Host "This will show what your Tuner Genius is broadcasting"
Write-Host ""

# Listen on common broadcast ports
foreach ($port in 9007, 9008, 9009, 9010) {
    try {
        $listener = New-Object System.Net.Sockets.UdpClient
        $listener.Client.SetSocketOption([System.Net.Sockets.SocketOptionLevel]::Socket, [System.Net.Sockets.SocketOptionName]::ReuseAddress, $true)
        $listener.Client.Bind([System.Net.IPEndPoint]::new([System.Net.IPAddress]::Any, $port))
        Write-Host "Listening on port $port..."
        
        $listener.Client.ReceiveTimeout = 2000
        try {
            $endpoint = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
            $bytes = $listener.Receive([ref]$endpoint)
            $message = [System.Text.Encoding]::ASCII.GetString($bytes)
            Write-Host "Port ${port}: $message (from $($endpoint.Address):$($endpoint.Port))" -ForegroundColor Green
        } catch {}
        $listener.Close()
    } catch {}
}

Write-Host ""
Write-Host "If nothing appeared above, your Tuner Genius might:"
Write-Host "1. Not be broadcasting on these ports"
Write-Host "2. Not be on the same network"
Write-Host "3. Use a different discovery method"

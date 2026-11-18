Start-Process -FilePath ".\Server\bin\Debug\net10.0-windows\DrivingLab.Server.exe" -WorkingDirectory ".\Server\bin\Debug\net10.0-windows"
Start-Sleep -Seconds 5
Start-Process -FilePath ".\BeltsBrakes\bin\Debug\net10.0-windows\BeltsBrakes.exe" -WorkingDirectory ".\BeltsBrakes\bin\Debug\net10.0-windows"
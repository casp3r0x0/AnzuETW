# Define variables
$serviceName   = "AnzuETW"
$displayName   = "AnzuETW"
$description   = "This service is for AnzuETW."
$exePath       = "C:\anzu\AnzuService.exe" # installation dir 
$arguments     = "C:\anzu\plguins\" # path of the plugin you can include share \\ip\share\plguins 

# Combine the executable path with arguments
$binaryPathName = "$exePath $arguments"

# Create the new service
New-Service -Name $serviceName -BinaryPathName $binaryPathName -DisplayName $displayName -StartupType Automatic

# Set the service description (using sc.exe for compatibility)
sc.exe description $serviceName "$description"

# Start the service
Start-Service -Name $serviceName
Start-Sleep -Seconds 1.5
Stop-service -name $serviceName
Start-Service -Name $serviceName

Write-Output "Service '$serviceName' installed and started with argument '$arguments'."

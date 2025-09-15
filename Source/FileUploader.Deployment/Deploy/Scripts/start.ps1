function Main {
    $scriptDir = Split-Path -Parent $PSCommandPath
    $myInstscript = (Get-Item $PSCommandPath ).Basename
    Write-Host "Start Command Invoked from $myInstscript"

    Write-Host "Applying Redis..."
    kubectl apply -k "$scriptDir\..\Artifacts\Redis"

    Write-Host "Waiting for Redis pod to be up"
    do{
        $pod = kubectl get pods -n file-uploader -l app=fileuploader-redis -o json | ConvertFrom-Json
        if ($pod.items.Count -eq 0) { continue }
        $status = $pod.items[0].status.phase
    } while ($status -ne "Running")

    
    Start-Process powershell -ArgumentList "kubectl port-forward deployment/fileuploader-redis 6379:6379 -n file-uploader" -WindowStyle Hidden 

    $logFile = "..\..\Logs\FileUploader_API.log"
    #Start-Process -FilePath "..\..\APIServer\FileUploader.API.exe" -WindowStyle Hidden -RedirectStandardOutput $logFile  
    Start-Process -FilePath (Join-Path $PSScriptRoot "..\..\APIServer\FileUploader.API.exe") -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -RedirectStandardOutput $logFile


    $logFile = "..\..\Logs\FileUploader_Worker.log"
    #Start-Process -FilePath "..\..\Worker\FileUploader.Worker.exe" -WindowStyle Hidden -RedirectStandardOutput $logFile
    Start-Process -FilePath (Join-Path $PSScriptRoot "..\..\Worker\FileUploader.Worker.exe") -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -RedirectStandardOutput $logFile

    $clientDLLPath = Join-Path $PSScriptRoot "..\..\FileUploader.Client.dll"
    Start-Process powershell -ArgumentList "dotnet $clientDLLPath"

    Write-Host "All done!"
}

Main
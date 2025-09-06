function Main {
    $scriptDir = Split-Path -Parent $PSCommandPath
    $myInstscript = (Get-Item $PSCommandPath ).Basename
    Write-Host "Stop Command Invoked from $myInstscript"

    $processName = "FileUploader.Client"
    $proc = Get-Process -Name $processName -ErrorAction SilentlyContinue
    if ($proc) {
        Stop-Process -Name $processName -Force
    }

    $processName = "FileUploader.API"
    $proc = Get-Process -Name $processName -ErrorAction SilentlyContinue
    if ($proc) {
        Stop-Process -Name $processName -Force
    }

    $processName = "FileUploader.Worker"
    $proc = Get-Process -Name $processName -ErrorAction SilentlyContinue
    if ($proc) {
        Stop-Process -Name $processName -Force
    }


    $kubectlProcesses = Get-Process -Name "kubectl" -ErrorAction SilentlyContinue
    $kubectlProcesses | ForEach-Object {
        Write-Host "Stopping kubectl process with PID:" $_.Id
        Stop-Process -Id $_.Id -Force
    }

    Write-Host "Deleting Redis.."
    kubectl delete -k "$scriptDir\..\Artifacts\Redis"

    Write-Host "All done!"
}

Main

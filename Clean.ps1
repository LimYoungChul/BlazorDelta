# Clean-BuildFolders.ps1
# Recursively finds and deletes obj and bin folders from current directory

try {
    $currentPath = Get-Location
    Write-Host "Searching for obj and bin folders..." -ForegroundColor Yellow
    Write-Host "Starting from: $currentPath" -ForegroundColor Gray
    Write-Host ""

    # Find all obj and bin folders recursively from current directory
    $foldersToDelete = Get-ChildItem -Path . -Recurse -Directory | Where-Object { 
        $_.Name -eq "obj" -or $_.Name -eq "bin" 
    }

    if ($foldersToDelete.Count -eq 0) {
        Write-Host "No obj or bin folders found." -ForegroundColor Green
    } else {
        # Display all folders that will be deleted
        Write-Host "Found $($foldersToDelete.Count) folder(s) to delete:" -ForegroundColor Red
        Write-Host ""

        foreach ($folder in $foldersToDelete) {
            Write-Host "  $($folder.FullName)" -ForegroundColor Cyan
        }

        Write-Host ""
        Write-Host "Total folders to delete: $($foldersToDelete.Count)" -ForegroundColor Yellow

        # Calculate total size
        $totalSize = 0
        foreach ($folder in $foldersToDelete) {
            try {
                $size = (Get-ChildItem -Path $folder.FullName -Recurse -File | Measure-Object -Property Length -Sum).Sum
                $totalSize += $size
            } catch {
                # Ignore errors calculating size
            }
        }

        if ($totalSize -gt 0) {
            $sizeInMB = [math]::Round($totalSize / 1MB, 2)
            Write-Host "Approximate total size: $sizeInMB MB" -ForegroundColor Yellow
        }

        Write-Host ""
        Write-Host "Press Enter to delete these folders, or Ctrl+C to cancel..." -ForegroundColor Red -NoNewline
        Read-Host

        Write-Host ""
        Write-Host "Deleting folders..." -ForegroundColor Yellow

        $deletedCount = 0
        $errorCount = 0

        foreach ($folder in $foldersToDelete) {
            try {
                Remove-Item -Path $folder.FullName -Recurse -Force
                Write-Host "Deleted: $($folder.FullName)" -ForegroundColor Green
                $deletedCount++
            } catch {
                Write-Host "Failed to delete: $($folder.FullName)" -ForegroundColor Red
                Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
                $errorCount++
            }
        }

        Write-Host ""
        Write-Host "Summary:" -ForegroundColor Yellow
        Write-Host "Successfully deleted: $deletedCount folders" -ForegroundColor Green
        if ($errorCount -gt 0) {
            Write-Host "Failed to delete: $errorCount folders" -ForegroundColor Red
        }
        Write-Host "Done!" -ForegroundColor Green
    }
} catch {
    Write-Host ""
    Write-Host "An error occurred:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

# Keep window open
Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") | Out-Null
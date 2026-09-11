# =====================================================================
# MovieManager System Resource Usage Monitor
# ابزار مستقل مانیتورینگ و ثبت مصرف منابع سیستم (CPU / GPU / RAM / VRAM / Threads)
# =====================================================================

$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = (Get-Location).Path
}

$dateStr = Get-Date -Format "yyyyMMdd_HHmmss"
$csvFile = Join-Path $scriptDir "resource_log_$dateStr.csv"
$cpuCores = [Environment]::ProcessorCount

# Write CSV Headers
"Timestamp,App_PID,CPU_Percent,GPU_Percent,VRAM_MB,RAM_WorkingSet_MB,RAM_Private_MB,Threads,Handles,Mpv_PID,Mpv_CPU_Percent,Mpv_GPU_Percent,Mpv_RAM_MB,Total_RAM_MB" | Out-File -FilePath $csvFile -Encoding utf8

Clear-Host
Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host "    MovieManager Desktop - Resource & GPU Monitor (ثبت منابع و گرافیک)    " -ForegroundColor White
Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host " [i] CPU Cores: $cpuCores" -ForegroundColor Gray
Write-Host " [i] Output CSV: $csvFile" -ForegroundColor Yellow
Write-Host " [i] مانیتورینگ شامل: پردازنده (CPU)، کارت گرافیک (GPU)، رم (RAM) و حافظه گرافیک (VRAM)" -ForegroundColor White
Write-Host " [i] برای توقف ضبط و مشاهده گزارش خلاصه، کلید [Ctrl + C] را فشار دهید." -ForegroundColor Green
Write-Host "--------------------------------------------------------------------------" -ForegroundColor DarkGray

$samples = 0
$cpuList = [System.Collections.Generic.List[double]]::new()
$gpuList = [System.Collections.Generic.List[double]]::new()
$ramList = [System.Collections.Generic.List[double]]::new()

$peakCpu = 0.0
$peakGpu = 0.0
$peakRam = 0.0
$peakVram = 0.0

$lastAppCpuTime = 0.0
$lastMpvCpuTime = 0.0
$lastTime = [DateTime]::UtcNow

function Get-ProcessGpuStats($targetPid) {
    $gpuPct = 0.0
    $vramMB = 0.0
    try {
        $engSamples = (Get-Counter "\GPU Engine(pid_${targetPid}_*)\Utilization Percentage" -ErrorAction Stop).CounterSamples
        if ($engSamples) {
            $gpuPct = [math]::Round(($engSamples | Measure-Object -Property CookedValue -Sum).Sum, 1)
        }
    } catch { }

    try {
        $memSamples = (Get-Counter "\GPU Process Memory(pid_${targetPid}_*)\Total Committed" -ErrorAction Stop).CounterSamples
        if ($memSamples) {
            $vramMB = [math]::Round(($memSamples | Measure-Object -Property CookedValue -Sum).Sum / 1MB, 1)
        }
    } catch { }

    return [PSCustomObject]@{ GpuPercent = $gpuPct; VramMB = $vramMB }
}

try {
    while ($true) {
        $appProc = Get-Process -Name "MovieManagerDesktop" -ErrorAction SilentlyContinue | Select-Object -First 1
        $mpvProc = Get-Process -Name "mpv" -ErrorAction SilentlyContinue | Select-Object -First 1

        if (-not $appProc) {
            Write-Host -NoNewline "`r[$([DateTime]::Now.ToString('HH:mm:ss'))] ⏳ منتظر باز شدن نرم‌افزار MovieManagerDesktop...            " -ForegroundColor Yellow
            Start-Sleep -Seconds 1
            $lastTime = [DateTime]::UtcNow
            continue
        }

        # Calculate deltas
        $now = [DateTime]::UtcNow
        $elapsedSec = ($now - $lastTime).TotalSeconds
        if ($elapsedSec -le 0.001) { $elapsedSec = 1.0 }

        # App CPU & RAM
        try {
            $curAppCpuTotal = $appProc.TotalProcessorTime.TotalSeconds
            $appRamMB = [math]::Round($appProc.WorkingSet64 / 1MB, 1)
            $appPrivateMB = [math]::Round($appProc.PrivateMemorySize64 / 1MB, 1)
            $threads = $appProc.Threads.Count
            $handles = $appProc.HandleCount
            $appPid = $appProc.Id

            if ($lastAppCpuTime -gt 0 -and $curAppCpuTotal -ge $lastAppCpuTime) {
                $appCpuUsage = [math]::Round((($curAppCpuTotal - $lastAppCpuTime) / ($elapsedSec * $cpuCores)) * 100, 1)
            } else {
                $appCpuUsage = 0.0
            }
            $lastAppCpuTime = $curAppCpuTotal
        } catch {
            $appCpuUsage = 0.0
            $appRamMB = 0.0
            $appPrivateMB = 0.0
            $threads = 0
            $handles = 0
            $appPid = 0
        }

        # App GPU & VRAM
        $appGpu = Get-ProcessGpuStats -targetPid $appPid
        $appGpuUsage = $appGpu.GpuPercent
        $appVramMB = $appGpu.VramMB

        # MPV Child Process (if video playback is active)
        $mpvPid = "-"
        $mpvCpuUsage = 0.0
        $mpvGpuUsage = 0.0
        $mpvRamMB = 0.0

        if ($mpvProc) {
            try {
                $mpvPid = $mpvProc.Id
                $curMpvCpuTotal = $mpvProc.TotalProcessorTime.TotalSeconds
                $mpvRamMB = [math]::Round($mpvProc.WorkingSet64 / 1MB, 1)

                if ($lastMpvCpuTime -gt 0 -and $curMpvCpuTotal -ge $lastMpvCpuTime) {
                    $mpvCpuUsage = [math]::Round((($curMpvCpuTotal - $lastMpvCpuTime) / ($elapsedSec * $cpuCores)) * 100, 1)
                }
                $lastMpvCpuTime = $curMpvCpuTotal

                $mpvGpu = Get-ProcessGpuStats -targetPid $mpvPid
                $mpvGpuUsage = $mpvGpu.GpuPercent
            } catch {
                $mpvCpuUsage = 0.0
                $mpvGpuUsage = 0.0
                $mpvRamMB = 0.0
            }
        } else {
            $lastMpvCpuTime = 0.0
        }

        $totalRamMB = [math]::Round($appRamMB + $mpvRamMB, 1)

        # Track Stats
        $samples++
        $cpuList.Add($appCpuUsage)
        $gpuList.Add($appGpuUsage)
        $ramList.Add($appRamMB)

        if ($appCpuUsage -gt $peakCpu) { $peakCpu = $appCpuUsage }
        if ($appGpuUsage -gt $peakGpu) { $peakGpu = $appGpuUsage }
        if ($appRamMB -gt $peakRam) { $peakRam = $appRamMB }
        if ($appVramMB -gt $peakVram) { $peakVram = $appVramMB }

        $timeStr = [DateTime]::Now.ToString("yyyy-MM-dd HH:mm:ss")

        # Save to CSV
        "$timeStr,$appPid,$appCpuUsage,$appGpuUsage,$appVramMB,$appRamMB,$appPrivateMB,$threads,$handles,$mpvPid,$mpvCpuUsage,$mpvGpuUsage,$mpvRamMB,$totalRamMB" | Out-File -FilePath $csvFile -Append -Encoding utf8

        # Color coded output
        $cpuColor = if ($appCpuUsage -gt 50) { "Red" } elseif ($appCpuUsage -gt 20) { "Yellow" } else { "Green" }
        $gpuColor = if ($appGpuUsage -gt 50) { "Red" } elseif ($appGpuUsage -gt 20) { "Yellow" } else { "Green" }
        $ramColor = if ($appRamMB -gt 800) { "Red" } elseif ($appRamMB -gt 500) { "Yellow" } else { "Cyan" }

        $displayTime = [DateTime]::Now.ToString("HH:mm:ss")
        Write-Host "[$displayTime] " -NoNewline -ForegroundColor DarkGray
        Write-Host "CPU: " -NoNewline -ForegroundColor Gray
        Write-Host ("{0,5:F1}%" -f $appCpuUsage) -NoNewline -ForegroundColor $cpuColor
        Write-Host (" (Peak: {0:F1}%) | " -f $peakCpu) -NoNewline -ForegroundColor DarkGray

        Write-Host "GPU: " -NoNewline -ForegroundColor Gray
        Write-Host ("{0,5:F1}%" -f $appGpuUsage) -NoNewline -ForegroundColor $gpuColor
        Write-Host (" (Peak: {0:F1}%) | " -f $peakGpu) -NoNewline -ForegroundColor DarkGray

        Write-Host "RAM: " -NoNewline -ForegroundColor Gray
        Write-Host ("{0,6:F1} MB" -f $appRamMB) -NoNewline -ForegroundColor $ramColor
        Write-Host (" (VRAM: {0:F1} MB) | " -f $appVramMB) -NoNewline -ForegroundColor DarkGray

        Write-Host "Thr: " -NoNewline -ForegroundColor Gray
        Write-Host ("{0,3}" -f $threads) -NoNewline -ForegroundColor White

        if ($mpvProc) {
            Write-Host (" | MPV: CPU {0:F1}% / GPU {1:F1}% / RAM {2:F1}MB" -f $mpvCpuUsage, $mpvGpuUsage, $mpvRamMB) -ForegroundColor Magenta
        } else {
            Write-Host ""
        }

        $lastTime = $now
        Start-Sleep -Seconds 1
    }
}
finally {
    Write-Host "`n"
    Write-Host "==========================================================================" -ForegroundColor Green
    Write-Host "                      گزارش نهایی مصرف منابع سیستم                         " -ForegroundColor White
    Write-Host "==========================================================================" -ForegroundColor Green

    if ($samples -gt 0) {
        $avgCpu = [math]::Round(($cpuList | Measure-Object -Average).Average, 1)
        $avgGpu = [math]::Round(($gpuList | Measure-Object -Average).Average, 1)
        $avgRam = [math]::Round(($ramList | Measure-Object -Average).Average, 1)

        Write-Host " • تعداد ثانیه‌های ثبت‌شده (نمونه‌ها): $samples ثانیه" -ForegroundColor White
        Write-Host " • میانگین مصرف پردازنده (Avg CPU):   $avgCpu %" -ForegroundColor Yellow
        Write-Host " • بیشترین مصرف پردازنده (Peak CPU): $peakCpu %" -ForegroundColor Red
        Write-Host " • میانگین مصرف گرافیک (Avg GPU):     $avgGpu %" -ForegroundColor Cyan
        Write-Host " • بیشترین مصرف گرافیک (Peak GPU):    $peakGpu %" -ForegroundColor Magenta
        Write-Host " • میانگین حافظه رم (Avg RAM):       $avgRam MB" -ForegroundColor Cyan
        Write-Host " • بیشترین حافظه رم (Peak RAM):      $peakRam MB" -ForegroundColor Magenta
        Write-Host " • بیشترین حافظه ویدیویی (Peak VRAM): $peakVram MB" -ForegroundColor Blue
        Write-Host "--------------------------------------------------------------------------" -ForegroundColor DarkGray
        Write-Host " • فایل گزارش کامل CSV ذخیره شد در:" -ForegroundColor Green
        Write-Host "   $csvFile" -ForegroundColor Yellow
    } else {
        Write-Host " داده‌ای برای پردازش ثبت نشد." -ForegroundColor Yellow
    }
    Write-Host "==========================================================================" -ForegroundColor Green
}

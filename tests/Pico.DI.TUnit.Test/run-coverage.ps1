# TUnit 测试覆盖率运行脚本
# Test Coverage Script for TUnit

param(
    [switch]$GenerateHtmlReport,
    [string]$OutputFormat = "cobertura"
)

$ErrorActionPreference = "Stop"
$TestProjectPath = $PSScriptRoot

Write-Host "Running TUnit tests with code coverage..." -ForegroundColor Cyan

Push-Location $TestProjectPath

try {
    # 确保输出目录存在
    $coverageOutputDir = Join-Path $TestProjectPath "TestResults"
    if (-not (Test-Path $coverageOutputDir)) {
        New-Item -ItemType Directory -Path $coverageOutputDir -Force | Out-Null
    }

    # 运行测试并收集覆盖率
    dotnet run --configuration Release `
        --coverage `
        --coverage-output $coverageOutputDir `
        --coverage-output-format $OutputFormat

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Tests failed!" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    # 查找生成的覆盖率文件
    $coverageFile = Get-ChildItem -Path $coverageOutputDir -Filter "*.cobertura.xml" | 
        Sort-Object LastWriteTime -Descending | 
        Select-Object -First 1

    if ($coverageFile) {
        Write-Host "`nCoverage report generated: $($coverageFile.FullName)" -ForegroundColor Green
        
        # 解析覆盖率摘要
        [xml]$cov = Get-Content $coverageFile.FullName
        $lineRate = [math]::Round([double]$cov.coverage.'line-rate' * 100, 2)
        $branchRate = [math]::Round([double]$cov.coverage.'branch-rate' * 100, 2)
        
        Write-Host "`n===== Coverage Summary =====" -ForegroundColor Yellow
        Write-Host "Line Coverage:   $lineRate%" -ForegroundColor $(if ($lineRate -ge 80) { "Green" } elseif ($lineRate -ge 60) { "Yellow" } else { "Red" })
        Write-Host "Branch Coverage: $branchRate%" -ForegroundColor $(if ($branchRate -ge 80) { "Green" } elseif ($branchRate -ge 60) { "Yellow" } else { "Red" })
        Write-Host "=============================" -ForegroundColor Yellow

        # 生成 HTML 报告（如果安装了 reportgenerator）
        if ($GenerateHtmlReport) {
            $reportGenerator = Get-Command reportgenerator -ErrorAction SilentlyContinue
            if (-not $reportGenerator) {
                Write-Host "`nInstalling ReportGenerator tool..." -ForegroundColor Cyan
                dotnet tool install -g dotnet-reportgenerator-globaltool
            }
            
            $htmlReportDir = Join-Path $coverageOutputDir "HtmlReport"
            Write-Host "`nGenerating HTML report..." -ForegroundColor Cyan
            reportgenerator `
                "-reports:$($coverageFile.FullName)" `
                "-targetdir:$htmlReportDir" `
                "-reporttypes:Html;Badges"
            
            Write-Host "HTML report generated: $htmlReportDir\index.html" -ForegroundColor Green
            
            # 可选：在浏览器中打开报告
            # Start-Process "$htmlReportDir\index.html"
        }
    }
}
finally {
    Pop-Location
}

# TheGround UDP ファイアウォール削除スクリプト
# 管理者権限で実行してください

$ErrorActionPreference = "Stop"

Write-Host "=== TheGround Firewall Cleanup ===" -ForegroundColor Cyan
Write-Host ""

$ruleNames = @(
    "TheGround UDP 9000 (CoP In)",
    "TheGround UDP 9001 (Haptics In)"
)

foreach ($ruleName in $ruleNames) {
    $existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
    if ($existing) {
        Remove-NetFirewallRule -DisplayName $ruleName
        Write-Host "✓ 削除: $ruleName" -ForegroundColor Green
    } else {
        Write-Host "- スキップ (存在しない): $ruleName" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "=== クリーンアップ完了 ===" -ForegroundColor Cyan

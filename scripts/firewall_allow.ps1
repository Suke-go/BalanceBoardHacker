# TheGround UDP ファイアウォール許可スクリプト
# 管理者権限で実行してください

$ErrorActionPreference = "Stop"

Write-Host "=== TheGround Firewall Setup ===" -ForegroundColor Cyan
Write-Host ""

# 既存ルールを削除（存在する場合）
$ruleNames = @(
    "TheGround UDP 9000 (CoP In)",
    "TheGround UDP 9001 (Haptics In)"
)

foreach ($ruleName in $ruleNames) {
    $existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
    if ($existing) {
        Remove-NetFirewallRule -DisplayName $ruleName
        Write-Host "既存ルール削除: $ruleName" -ForegroundColor Yellow
    }
}

# UDP 9000 許可 (CoP データ受信用)
New-NetFirewallRule `
    -DisplayName "TheGround UDP 9000 (CoP In)" `
    -Direction Inbound `
    -Protocol UDP `
    -LocalPort 9000 `
    -Action Allow `
    -Profile Any `
    -Description "TheGround: Balance Board CoP data reception"

Write-Host "✓ UDP 9000 (Inbound) を許可しました" -ForegroundColor Green

# UDP 9001 許可 (ハプティクスコマンド受信用)
New-NetFirewallRule `
    -DisplayName "TheGround UDP 9001 (Haptics In)" `
    -Direction Inbound `
    -Protocol UDP `
    -LocalPort 9001 `
    -Action Allow `
    -Profile Any `
    -Description "TheGround: Haptics command reception"

Write-Host "✓ UDP 9001 (Inbound) を許可しました" -ForegroundColor Green

Write-Host ""
Write-Host "=== 設定完了 ===" -ForegroundColor Cyan
Write-Host "TheGround.PoC と Unity の通信が可能になりました。"
Write-Host ""
Write-Host "削除する場合は firewall_remove.ps1 を実行してください。"

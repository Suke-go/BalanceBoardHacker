# PC-Quest 双方向通信 実装要件

## 概要

スキージャンプVR体験のための双方向UDP通信仕様。PCがハプティクス制御、QuestがVR描画とゲームロジックを担当。

---

## 通信アーキテクチャ

```
┌─────────────────┐                    ┌─────────────────┐
│   Windows PC    │                    │   Meta Quest    │
│  (PoC App)      │                    │   (Unity VR)    │
├─────────────────┤                    ├─────────────────┤
│ Balance Board   │──UDP 9000 (60Hz)──▶│ TheGroundManager│
│ CoPStreamer     │   CoP + Status     │                 │
│                 │                    │                 │
│ HapticController│◀──UDP 9001 (20Hz)──│ SkiJumpController│
│ CommandReceiver │   Commands         │                 │
└─────────────────┘                    └─────────────────┘
```

---

## 通信プロトコル

### PC → Quest (Port 9000, 60Hz)

**パケット形式**: 32バイト固定 (既存CoPPacket)

| フィールド | 型 | 説明 |
|------------|-----|------|
| Header | uint32 | "TGND" (0x444E4754) |
| Version | uint8 | 1 |
| Flags | uint8 | bit0:Valid, bit1:Calibrated, bit2:Converged, bit3:Vibrating |
| Reserved | uint16 | 0 |
| CopX | float | mm |
| CopY | float | mm |
| Weight | float | kg |
| Snr | float | dB |
| Timestamp | int64 | Unix ms |

### Quest → PC (Port 9001, ~20Hz)

**パケット形式**: UTF-8テキストコマンド

| コマンド | 引数 | 説明 |
|----------|------|------|
| `VIB_START,<type>,<amp>` | type: sine/noise/snow, amp: 0-1 | 振動開始 |
| `VIB_STOP` | なし | 振動停止 |
| `VIB_VELOCITY,<v>` | v: 0-1 | 速度更新 (20Hz) |
| `VIB_PULSE,<dur>,<amp>` | dur: 秒, amp: 0-1 | 短パルス |
| `CAL_START` | なし | キャリブ開始 |
| `RESET` | なし | 全状態リセット |

---

## PC側 実装要件

### 新規: HapticController.cs

```csharp
public class HapticController
{
    // 状態
    bool IsVibrating { get; }
    SignalType CurrentType { get; }
    float CurrentVelocity { get; }
    
    // コマンド処理
    void ProcessCommand(string command);
    
    // 振動制御
    void StartVibration(SignalType type, float amplitude);
    void StopVibration();
    void SetVelocity(float velocity);  // SnowTextureのVelocityに反映
    void Pulse(float duration, float amplitude);
}
```

### 更新: MainForm.cs

```csharp
// Timer (50ms間隔) でコマンドポーリング
void OnCommandPoll(object? sender, EventArgs e)
{
    while (_commandReceiver.TryReceive(out string cmd))
    {
        _hapticController.ProcessCommand(cmd);
    }
}
```

### 更新: CommandReceiver.cs

- `Poll()` → `TryReceive(out string)` に変更
- 非ブロッキング受信

---

## Unity側 実装要件

### 更新: TheGroundManager.cs

```csharp
// コマンド送信
public void SendCommand(string command);
public void SendVibrationStart(SignalType type, float amplitude);
public void SendVibrationStop();
public void SendVelocityUpdate(float velocity);  // 20Hz制限
public void SendPulse(float duration, float amplitude);

// 速度更新スロットリング
private float _lastVelocitySendTime;
private const float VelocitySendInterval = 0.05f;  // 20Hz
```

### 新規: SkiJumpController.cs

```csharp
public class SkiJumpController : MonoBehaviour
{
    // ステート
    public enum GameState { Waiting, Countdown, Running, InAir, Landed, Result }
    public GameState CurrentState { get; private set; }
    
    // 物理パラメータ
    [SerializeField] float _maxSpeed = 25f;  // m/s (90km/h)
    [SerializeField] float _gravity = 9.8f;
    [SerializeField] float _slopeAngle = 36f;  // degrees
    
    // 姿勢閾値
    [SerializeField] float _deadZoneMm = 15f;
    [SerializeField] float _maxLeanMm = 50f;
    
    // 現在値
    public float CurrentSpeed { get; private set; }
    public float NormalizedSpeed => CurrentSpeed / _maxSpeed;
    public float JumpDistance { get; private set; }
    
    // イベント
    public event Action OnRunStarted;
    public event Action OnTakeoff;
    public event Action<float> OnLanded;  // 飛距離
    
    // コア関数
    void StartRun();
    void UpdatePhysics();
    float CalculateDragCoefficient(float copY);
    void EnterFlightPhase();
    void Land(float distance);
}
```

---

## フェイルセーフ要件

### FS-01: Quest切断時の自動停止

```csharp
// PC側: 3秒間コマンドなし → 振動停止
private DateTime _lastCommandTime;
void CheckHeartbeat()
{
    if ((DateTime.UtcNow - _lastCommandTime).TotalSeconds > 3.0)
    {
        StopVibration();
        _isQuestConnected = false;
    }
}
```

### FS-02: PC切断時の状態表示

```csharp
// Unity側: 2秒間パケットなし → 警告表示
if ((Time.time - _lastPacketTime) > 2.0f)
{
    ShowConnectionWarning();
}
```

### FS-03: 振動停止の冗長性

- **早めのVIB_STOP送信**: テイクオフ100ms前に送信
- **PC側タイムアウト**: VIB_START後10秒で自動停止 (安全装置)

---

## 実装チェックリスト

### PC側

- [ ] `HapticController.cs` 新規作成
  - [ ] `ProcessCommand()` コマンドパーサー
  - [ ] `StartVibration()` / `StopVibration()`
  - [ ] `SetVelocity()` → SineWaveGenerator.Velocity連携
  - [ ] `Pulse()` 短パルス
- [ ] `CommandReceiver.cs` 更新
  - [ ] `TryReceive()` 非ブロッキング
  - [ ] ハートビート記録
- [ ] `MainForm.cs` 更新
  - [ ] コマンドポーリングタイマー (50ms)
  - [ ] フェイルセーフチェック
  - [ ] 接続状態表示

### Unity側

- [ ] `TheGroundManager.cs` 更新
  - [ ] `SendCommand()` UDP送信
  - [ ] 速度更新スロットリング (20Hz)
  - [ ] フェイルセーフ警告
- [ ] `SkiJumpController.cs` 新規作成
  - [ ] ステートマシン
  - [ ] 物理計算
  - [ ] テイクオフ検出
  - [ ] VIB_STOP送信タイミング
- [ ] シーン作成
  - [ ] ジャンプ台モデル (簡易)
  - [ ] スカイボックス
  - [ ] UI (速度/距離表示)

---

## タイミング要件

| イベント | 許容レイテンシ | 備考 |
|----------|---------------|------|
| CoP更新 | <50ms | 姿勢感度に影響 |
| 速度→振動反映 | <100ms | 体感で許容可能 |
| テイクオフ→振動停止 | **<30ms** | 違和感回避のため厳しく |
| 着地パルス | <50ms | 許容可能 |

**対策**: VIB_STOPは位置予測で早めに送信

---

## 次のアクション

1. **PC側 HapticController 実装**
2. **PC側 CommandReceiver 更新**
3. **Unity側 TheGroundManager 送信機能追加**
4. **Unity側 SkiJumpController 新規作成**
5. **統合テスト**

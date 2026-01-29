# Unity実装エージェント向け指示書

## 🎯 ミッション

**TheGround VR スキージャンプ体験** を Meta Quest 向けに実装する。Balance Board で姿勢制御、Bass Shaker で滑走振動フィードバックを提供。

---

## 📋 前提条件

### 提供済みリソース

| ファイル | 場所 | 説明 |
|----------|------|------|
| `TheGround.Core.dll` | ビルドして `Assets/Plugins/` に配置 | CoP計算・補正コアライブラリ |
| `TheGroundManager.cs` | `src/TheGround.Unity/` | UDP受信・ハプティクス制御 **コピーして使用** |
| `SkiJumpController.cs` | `src/TheGround.Unity/` | ゲームロジック雛形 **参考実装** |

### PC側 (別プロセス)

`TheGround.PoC` アプリが以下を担当:
- Wii Balance Board接続 (Bluetooth HID)
- UDP 9000 でCoPデータ送信
- UDP 9001 でハプティクスコマンド受信

**⚠️ Balance Board は Unity から直接接続しない** (HID制約のため)

---

## 🏗️ Unityプロジェクトセットアップ

### Step 1: プロジェクト作成

```
Unity Hub → New Project → 3D (URP) → "TheGroundVR"
```

### Step 2: Meta XR SDK

```
Window → Package Manager → Add package by name
→ "com.meta.xr.sdk.all"
```

### Step 3: ビルド設定

```
File → Build Settings → Android
Player Settings:
  - Minimum API Level: 29
  - Scripting Backend: IL2CPP
  - XR Plug-in Management → Oculus ✓
```

### Step 4: DLL配置

```
Assets/
├── Plugins/
│   └── TheGround.Core.dll    ← ビルドしてコピー
└── Scripts/
    └── TheGroundManager.cs   ← src/TheGround.Unity/ からコピー
```

---

## 📁 実装するファイル一覧

### 優先度: 🔴 高 / 🟡 中 / 🟢 低

| 優先度 | ファイル | 役割 |
|--------|----------|------|
| 🔴 | `Scripts/Core/GameManager.cs` | シーン遷移、グローバル状態 |
| 🔴 | `Scripts/UI/TitleUIController.cs` | 接続状態、キャリブ、スタート |
| 🔴 | `Scripts/Game/SkiJumpController.cs` | ゲームステートマシン |
| 🟡 | `Scripts/UI/GameUIController.cs` | 速度・距離表示 |
| 🟡 | `Scripts/Game/PlayerController.cs` | CoP → 移動変換 |
| 🟡 | `Scripts/UI/ResultUIController.cs` | 結果表示 |
| 🟢 | `Scripts/Game/PhysicsSimulator.cs` | 滑走・飛行物理 |

---

## 🎮 ゲームフロー

```
TitleScene                    SkiJumpScene
┌─────────┐                   ┌─────────────────────────────────┐
│ Title   │──[START]─────────▶│ Countdown → Running → InAir    │
│ Calibrate│                  │     → Landing → Result         │
│ Test Vib │                  │         ↓         ↓            │
└─────────┘                   │    [Retry]   [Back to Title]   │
     ▲                        └─────────────────────────────────┘
     └────────────────────────────────────┘
```

---

## 📡 通信API (TheGroundManager)

### プロパティ (読み取り)

```csharp
TheGroundManager.Instance.IsUserOnBoard   // ボードに乗っているか
TheGroundManager.Instance.IsCalibrated    // キャリブ完了か
TheGroundManager.Instance.CoPPositionMm   // Vector2, CoP位置 (mm)
TheGroundManager.Instance.Weight          // float, 体重 (kg)
TheGroundManager.Instance.LocomotionInput // Vector2, 正規化済み移動入力
```

### メソッド (ハプティクス制御)

```csharp
// キャリブレーション
TheGroundManager.Instance.RequestCalibration();

// 振動制御
TheGroundManager.Instance.StartVibration(frequency, amplitude);
TheGroundManager.Instance.StopVibration();
TheGroundManager.Instance.StartSnowVibration(amplitude);  // スキー用
TheGroundManager.Instance.UpdateVelocity(normalizedSpeed); // 速度連動
TheGroundManager.Instance.PulseVibration(duration, amplitude); // 着地衝撃
```

### UnityEvents (Inspector設定可)

```csharp
OnCoPUpdated           // Vector2
OnCalibrationComplete  // void
OnLocomotionInput      // Vector2
OnUserSteppedOn        // void
OnUserSteppedOff       // void
```

---

## 🎿 フェーズ別実装ガイド

### Phase 1: Countdown (3秒)

```csharp
// 3, 2, 1, GO! のカウントダウン
// 振動なし
// 姿勢ガイド表示
```

### Phase 2: Running (滑走)

```csharp
void OnEnterRunning() {
    TheGroundManager.Instance.StartSnowVibration(0.5f);
}

void UpdateRunning() {
    float speed = CalculateSpeed();
    TheGroundManager.Instance.UpdateVelocity(speed / maxSpeed);
    
    // 前傾 = 加速、後傾 = 減速
    Vector2 cop = TheGroundManager.Instance.CoPPositionMm;
    if (cop.y > 20f) acceleration = AccelForward;
    else if (cop.y < -20f) acceleration = AccelBackward;
}
```

### Phase 3: InAir (飛行)

```csharp
void OnEnterInAir() {
    TheGroundManager.Instance.StopVibration(); // 静寂 = 浮遊感
}
```

### Phase 4: Landing (着地)

```csharp
void OnLanding() {
    TheGroundManager.Instance.PulseVibration(0.2f, 1.0f);
    // 画面揺れエフェクト
}
```

---

## 🕹️ Quest コントローラー入力

```csharp
// Aボタン = 決定
if (OVRInput.GetDown(OVRInput.Button.One)) OnConfirm();

// Bボタン = キャンセル
if (OVRInput.GetDown(OVRInput.Button.Two)) OnCancel();

// トリガー = キャリブ開始
if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger)) RequestCalibration();
```

---

## ⚠️ 注意事項

### ファイアウォール
- UDP 9000, 9001 を許可

### IL2CPP対応
`Assets/link.xml`:
```xml
<linker>
  <assembly fullname="TheGround.Core" preserve="all"/>
</linker>
```

### テスト手順
1. PC: `TheGround.PoC` 起動、Connect、Play
2. Quest: Link または Build & Run
3. 同一LAN上で UDP 通信確認

---

## 📏 工数目安

| タスク | 時間 |
|--------|------|
| プロジェクト設定 | 30分 |
| TitleScene | 2時間 |
| SkiJumpController | 3時間 |
| 振動連携 | 2時間 |
| 3Dモデル・エフェクト | 2時間 |
| **合計** | **約10時間** |

---

## 📖 参照ドキュメント

| ファイル | 内容 |
|----------|------|
| `docs/unity_integration.md` | DLL/Unity連携設計 |
| `docs/unity_scene_design.md` | シーン・UI設計詳細 |
| `docs/ski_jump_requirements.md` | VR体験要件 |
| `docs/communication_spec.md` | 通信プロトコル |

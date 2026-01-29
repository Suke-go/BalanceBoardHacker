# TheGround

**振動ハプティクス下でのリアルタイムCoP（重心動揺）計測システム**

Wii Balance Board を用いた姿勢計測において、振動子（Bass Shaker等）駆動時でも高精度なCoP計測を可能にする適応干渉除去システムです。

---

## ✨ 特徴

- **振動補正アルゴリズム (AMHIC)**: NLMS適応フィルタによる多周波数干渉除去
- **デュアル利用可能**: 純粋なC#ライブラリ / Unityラッパー
- **リアルタイム処理**: 60Hz、低遅延（<16ms）
- **臨床グレード精度**: ISPGR準拠、SNR改善 10dB+

---

## 📦 プロジェクト構成

```
TheGround/
├── src/
│   ├── TheGround.Core/      # 📚 コアライブラリ (netstandard2.1)
│   │   ├── CoPProcessor.cs  #    - CoP計算・キャリブレーション・補正
│   │   └── UdpTransport.cs  #    - UDP通信
│   │
│   ├── TheGround.Unity/     # 🎮 Unityラッパー
│   │   ├── TheGroundManager.cs   # - MonoBehaviour (UDP受信)
│   │   └── SkiJumpController.cs  # - サンプル：VRスキージャンプ
│   │
│   └── TheGround.PoC/       # 🖥️ PC側アプリ (Windows Forms)
│       ├── BalanceBoard/    #    - Wii接続、Bluetooth HID
│       ├── Audio/           #    - Bass Shaker出力
│       └── Network/         #    - UDP送信
│
└── docs/                    # 📖 ドキュメント
```

---

## 🚀 使い方

### 方法1: C#ライブラリとして使用 (.NET)

```csharp
using TheGround.Core;

// インスタンス作成
var processor = new CoPProcessor(sampleRate: 60f);

// センサ値からCoP計算 (補正込み)
var result = processor.Process(
    topLeft, topRight, bottomLeft, bottomRight,
    vibrationActive: true,
    audioPhase: currentPhase
);

Console.WriteLine($"CoP: ({result.X:F1}, {result.Y:F1}) mm");
Console.WriteLine($"Weight: {result.Weight:F1} kg");
```

**NuGet / DLL参照**:
```xml
<ProjectReference Include="..\TheGround.Core\TheGround.Core.csproj" />
```

---

### 方法2: Unityから使用

#### Step 1: DLL配置
```
Assets/Plugins/TheGround.Core.dll
```

#### Step 2: TheGroundManager配置
```csharp
// シーンに配置、UDPポート9000で受信開始
public class YourScript : MonoBehaviour
{
    void Update()
    {
        if (TheGroundManager.Instance.IsUserOnBoard)
        {
            Vector2 cop = TheGroundManager.Instance.CoPPositionMm;
            Debug.Log($"CoP: {cop}");
        }
    }
}
```

#### Step 3: PC側アプリ起動
`TheGround.PoC` を起動し、UDP送信をONにする。

---

## 📡 通信プロトコル

```
[PC: PoC App] ──UDP 9000──▶ [Unity/Client]  (CoP Data)
[PC: PoC App] ◀──UDP 9001── [Unity/Client]  (Haptic Commands)
```

### CoPパケット (32 bytes)
| Field | Type | Description |
|-------|------|-------------|
| Header | 4 bytes | "TGND" |
| Flags | 1 byte | Valid, Calibrated, Converged |
| CoP X/Y | float×2 | Position in mm |
| Weight | float | kg |
| SNR | float | dB improvement |
| Timestamp | uint64 | ms |

### Haptic Commands (UTF-8 text)
```
VIB_START,30.0,0.5    # 周波数Hz, 振幅0-1
VIB_STOP
CAL_START
VIB_PULSE,0.2,1.0     # 持続秒, 振幅
```

---

## 🔧 ビルド

### コアライブラリ
```bash
cd src/TheGround.Core
dotnet build
```

### PoC App (Windows)
```bash
cd src/TheGround.PoC
dotnet run
```

---

## 📊 アルゴリズム概要

**AMHIC (Adaptive Multi-Harmonic Interference Cancellation)**

- 振動子の基本波 + 高調波 (2f, 3f) を適応フィルタで除去
- NLMS更新則で入力パワー非依存の安定収束
- 収束時間: ~3秒、SNR改善: 10-15dB

詳細: [`docs/algorithm.md`](docs/algorithm.md)

---

## 📱 サンプルアプリケーション

### VRスキージャンプ (Meta Quest)
- Balance Board で姿勢制御
- Bass Shaker で滑走振動フィードバック
- 詳細: [`docs/ski_jump_requirements.md`](docs/ski_jump_requirements.md)

---

## 📚 ドキュメント

| ファイル | 内容 |
|----------|------|
| [`docs/README.md`](docs/README.md) | 技術詳細・研究背景 |
| [`docs/algorithm.md`](docs/algorithm.md) | AMHIC アルゴリズム詳細 |
| [`docs/unity_integration.md`](docs/unity_integration.md) | Unity連携設計 |
| [`docs/communication_spec.md`](docs/communication_spec.md) | 通信プロトコル仕様 |

---

## 📋 要件

### ハードウェア
- Wii Balance Board
- Bluetooth対応PC (Windows 10/11)
- (オプション) Bass Shaker + アンプ
- (オプション) Meta Quest 2/3

### ソフトウェア
- .NET 8.0 (PoC App)
- .NET Standard 2.1 (Core Library)
- Unity 2022.3+ (Unity統合時)

---

## 📄 ライセンス

MIT License

---

## 🔗 参考文献

1. Widrow, B., & Stearns, S. D. (1985). *Adaptive Signal Processing*. Prentice-Hall.
2. Clark, R. A., et al. (2010). Validity and reliability of the Nintendo Wii Balance Board. *Gait & Posture*.
3. ISPGR (2017). *Recommendations for Posturography*.

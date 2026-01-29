# TheGround.Core - DLL/Unity 連携設計

## 概要

TheGround.Coreは、信号処理コアをDLLとして分離し、Unity等の外部アプリケーションから利用可能にするライブラリです。

## アーキテクチャ

```
┌─────────────────────────────────────────────────────┐
│                    Unity / Game                      │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │
│  │ C# Wrapper  │  │ Audio Mgr   │  │ Visualizer  │  │
│  └──────┬──────┘  └──────┬──────┘  └─────────────┘  │
└─────────┼────────────────┼──────────────────────────┘
          │                │
          ▼                ▼
┌─────────────────────────────────────────────────────┐
│              TheGround.Core.dll                      │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │
│  │CoPProcessor │  │ Compensator │  │ Calibrator  │  │
│  └─────────────┘  └─────────────┘  └─────────────┘  │
└─────────────────────────────────────────────────────┘
          ▲
          │ (別プロセス or 同一プロセス)
┌─────────────────────────────────────────────────────┐
│           Balance Board Reader (PoC App)             │
│         → UDP/OSC でデータ送信 or 直接呼び出し        │
└─────────────────────────────────────────────────────┘
```

## プロジェクト構成

```
TheGround/
├── src/
│   ├── TheGround.Core/           # 信号処理コアDLL
│   │   ├── CoPProcessor.cs       # メインAPI
│   │   ├── Calibrator.cs         # キャリブレーション
│   │   ├── AdaptiveCompensator.cs
│   │   └── TheGround.Core.csproj # netstandard2.1
│   │
│   ├── TheGround.Unity/          # Unity用ラッパー
│   │   ├── TheGroundManager.cs   # MonoBehaviour
│   │   ├── CoPVisualizer.cs      # 可視化
│   │   └── UdpReceiver.cs        # UDP受信
│   │
│   └── TheGround.PoC/            # 既存PoC（データ送信元）
│
└── docs/
```

## API設計

### TheGround.Core.CoPProcessor

```csharp
public class CoPProcessor
{
    // 初期化
    public CoPProcessor(float sampleRate = 60f);
    
    // センサ値からCoP計算 + 補正
    public CoPResult Process(float tl, float tr, float bl, float br, 
                             bool vibrationActive = false, 
                             float audioPhase = -1f);
    
    // キャリブレーション
    public void StartCalibration();
    public bool IsCalibrating { get; }
    public bool IsCalibrated { get; }
    
    // 振動補正
    public bool CompensationEnabled { get; set; }
    public float VibrationFrequency { get; set; }
    public bool IsConverged { get; }
    public float SnrImprovement { get; }
    
    // リセット
    public void Reset();
}

public struct CoPResult
{
    public float X;           // CoP X [mm]
    public float Y;           // CoP Y [mm]
    public float Weight;      // 重量 [kg]
    public float RawX;        // 補正前 X
    public float RawY;        // 補正前 Y
    public bool IsValid;      // 有効データか
}
```

### Unity用ラッパー

```csharp
public class TheGroundManager : MonoBehaviour
{
    public static TheGroundManager Instance { get; }
    
    // 接続モード
    public enum ConnectionMode { UDP, DirectDLL }
    public ConnectionMode Mode;
    public int UdpPort = 9000;
    
    // イベント
    public event Action<CoPResult> OnCoPUpdated;
    public event Action OnCalibrationComplete;
    public event Action<float> OnConverged;  // SNR
    
    // 現在値
    public CoPResult CurrentCoP { get; }
    public Vector2 CoPPosition { get; }  // Unity座標系
    
    // 制御
    public void StartCalibration();
    public void ResetCompensation();
}
```

## 通信プロトコル（UDP）

### フォーマット

```
[Header: 4 bytes "TGND"]
[Version: 1 byte]
[Flags: 1 byte]
  bit 0: IsValid
  bit 1: IsCalibrated
  bit 2: IsConverged
  bit 3: VibrationActive
[Reserved: 2 bytes]
[CoP X: float32]
[CoP Y: float32]
[Weight: float32]
[SNR: float32]
[Timestamp: uint64]
```

**サイズ**: 32 bytes/packet  
**送信レート**: 60 Hz

### OSC代替

```
/theground/cop [X:float, Y:float, Weight:float]
/theground/status [Calibrated:int, Converged:int, SNR:float]
```

## ビルド設定

### TheGround.Core.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

**netstandard2.1を選択した理由**:
- Unity 2021+ で完全サポート
- Span<T>, Memory<T> が使用可能
- IL2CPP互換

## Unity導入手順

1. `TheGround.Core.dll` を `Assets/Plugins/` に配置
2. `TheGroundManager.prefab` をシーンに追加
3. UDPモード: PoC Appを起動、UDP送信ON
4. DirectDLLモード: Balance Board接続コード追加

## 制約事項

- **DirectDLLモード**: WiimoteLibはUnityで直接動作しない
  - 理由: HIDアクセスにネイティブAPI必要
  - 解決策: 別プロセス(PoC App)からUDP送信

- **IL2CPP**: リフレクション制限あり
  - 対策: link.xmlで保護

## 実装優先度

| フェーズ | 内容 | 工数 |
|----------|------|------|
| Phase 1 | Core DLL分離 | 2h |
| Phase 2 | UDP送信（PoC側） | 1h |
| Phase 3 | Unity受信・表示 | 2h |
| Phase 4 | OSC対応（オプション） | 1h |

## 次のステップ

1. [ ] TheGround.Core プロジェクト作成
2. [ ] CoPProcessor API実装
3. [ ] PoC AppにUDP送信追加
4. [ ] Unity用サンプルシーン作成

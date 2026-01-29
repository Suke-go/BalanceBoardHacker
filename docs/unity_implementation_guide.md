# Unity スキージャンプ 初心者向け実装ガイド

## 概要

Meta Quest用のスキージャンプVR体験を作成します。Balance Boardで姿勢を読み取り、PCから振動フィードバックを送信します。

---

## 必要なもの

### ハードウェア
- Meta Quest 2/3/Pro
- Windows PC (アプリ実行用)
- Wii Balance Board + Bluetooth
- Bass Shaker + アンプ

### ソフトウェア
- Unity 2022.3 LTS
- Meta XR SDK (Oculus Integration)
- Visual Studio 2022

---

## プロジェクト作成手順

### Step 1: 新規プロジェクト作成

```
Unity Hub → New Project → 3D (URP) → "TheGroundVR"
```

### Step 2: Meta XR SDKインストール

```
Window → Package Manager → Add package by name
→ "com.meta.xr.sdk.all"
```

### Step 3: ビルド設定

```
File → Build Settings → Android
Player Settings:
  - Minimum API Level: 29
  - Target API Level: 32
  - XR Plug-in Management → Oculus チェック
```

---

## フォルダ構成 (作成するもの)

```
Assets/
│
├── Scenes/                    ★シーンファイル
│   ├── TitleScene.unity
│   └── SkiJumpScene.unity
│
├── Scripts/                   ★C#スクリプト
│   ├── Network/
│   │   └── TheGroundManager.cs    (提供済み)
│   ├── Game/
│   │   ├── SkiJumpController.cs
│   │   └── PlayerMovement.cs
│   └── UI/
│       ├── TitleUI.cs
│       └── GameUI.cs
│
├── Prefabs/                   ★再利用オブジェクト
│   ├── Player.prefab
│   ├── JumpHill.prefab
│   └── UI/
│       ├── TitleCanvas.prefab
│       └── GameCanvas.prefab
│
├── Materials/                 ★マテリアル
│   ├── Snow.mat
│   ├── Sky.mat
│   └── Track.mat
│
├── Models/                    ★3Dモデル
│   └── (後述のアセット)
│
└── Audio/                     ★効果音
    ├── wind.wav
    ├── countdown.wav
    └── landing.wav
```

---

## 必要なアセット一覧

### 3Dモデル (無料アセット推奨)

| 用途 | 推奨アセット | 備考 |
|------|-------------|------|
| ジャンプ台 | ProBuilder で自作 | Unity標準ツール |
| 雪山 | Terrain + Snow Texture | 標準機能で十分 |
| スキーヤー | なし (一人称視点) | 手だけあればOK |
| 空 | Skybox (Unity Asset Store) | "Winter Sky"で検索 |

### テクスチャ

| 用途 | ファイル名 | 解像度 |
|------|------------|--------|
| 雪面 | snow_diffuse.png | 1024x1024 |
| トラック | track_normal.png | 512x512 |

### 効果音

| 用途 | ファイル名 | 形式 |
|------|------------|------|
| 風切り音 | wind_loop.wav | ループ |
| カウントダウン | beep.wav | 短音 |
| 着地 | landing_impact.wav | 単発 |

---

## コントローラー入力 (Quest)

### 必要な入力

| 入力 | 用途 | スクリプト |
|------|------|-----------|
| **A/Xボタン** | 決定 (START, リトライ) | `OVRInput.GetDown(OVRInput.Button.One)` |
| **B/Yボタン** | キャンセル (タイトル戻り) | `OVRInput.GetDown(OVRInput.Button.Two)` |
| **トリガー** | キャリブ開始 | `OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger)` |

### 実装例

```csharp
void Update()
{
    // Aボタンで決定
    if (OVRInput.GetDown(OVRInput.Button.One))
    {
        OnConfirm();
    }
    
    // Bボタンでキャンセル
    if (OVRInput.GetDown(OVRInput.Button.Two))
    {
        OnCancel();
    }
}
```

### UI操作のポイント

- **レイキャスト不要**: ボタン直押しでシンプルに
- **ハンドトラッキング不要**: コントローラー必須
- **VRキャンバス**: World Space Canvas (プレイヤー前方に固定)

---

## シーン別の作り方

### TitleScene の作り方

1. **空のシーン作成**: `File → New Scene`
2. **OVRCameraRig配置**: `Assets/Oculus/VR/Prefabs/OVRCameraRig`をドラッグ
3. **Canvas作成**: 
   - `GameObject → UI → Canvas`
   - Render Mode: `World Space`
   - 位置: (0, 1.5, 2) ※プレイヤー前方2m
4. **UI配置**:
   - タイトルテキスト
   - 接続ステータス (TMP_Text)
   - ボタン3つ (Calibrate, Test Vib, START)
5. **TheGroundManager配置**: 空のGameObjectにアタッチ

### SkiJumpScene の作り方

1. **Terrain作成**: `GameObject → 3D → Terrain`
   - サイズ: 200m x 200m
   - 傾斜を付ける (Brushで削る)
2. **ジャンプ台作成**: ProBuilderで
   - 長さ: 100m
   - 傾斜: 36度のスロープ
   - テイクオフ: 10度で7m
3. **プレイヤー配置**:
   - OVRCameraRig
   - Rigidbody (重力なし、スクリプト制御)
4. **UI作成**:
   - 速度表示 (左上)
   - 距離表示 (右上)
   - カウントダウン (中央)

---

## スクリプト実装順序

### 初心者向け: 1つずつ確認しながら進める

```
Step 1: TheGroundManager.cs
   ↓ UDP受信できるか確認
   
Step 2: TitleUI.cs
   ↓ ボタン押下 → ログ出力で確認
   
Step 3: SkiJumpController.cs (ステートマシン)
   ↓ 各フェーズ遷移をログで確認
   
Step 4: PlayerMovement.cs
   ↓ CoP → 移動を確認
   
Step 5: 振動連携
   ↓ VIB_START/STOP送信確認
   
Step 6: 仕上げ (エフェクト、音)
```

---

## デバッグ方法

### PC側との接続確認

```csharp
void Update()
{
    // デバッグ: 毎フレームCoP表示
    if (TheGroundManager.Instance.IsUserOnBoard)
    {
        Debug.Log($"CoP: {TheGroundManager.Instance.CoPPositionMm}");
    }
}
```

### Quest Linkでテスト

1. Quest Link接続
2. Unity で Play
3. Quest上でVR表示確認
4. PC側アプリも同時起動

### 実機ビルド

```
File → Build Settings → Build And Run
→ Quest に直接インストール
```

---

## よくあるエラーと対処

| エラー | 原因 | 対処 |
|--------|------|------|
| UDP受信できない | ファイアウォール | Port 9000/9001を許可 |
| OVRInput反応なし | XR設定不足 | Oculus XR Plugin有効化 |
| Canvas見えない | World Space設定ミス | Scale 0.01、距離2m |
| ビルドエラー | API Level | 29以上に設定 |

---

## 工数見積もり

| タスク | 時間 | 難易度 |
|--------|------|--------|
| プロジェクト設定 | 30分 | ★☆☆ |
| TitleScene作成 | 2時間 | ★★☆ |
| TheGroundManager統合 | 1時間 | ★★☆ |
| SkiJumpController | 3時間 | ★★★ |
| 振動連携 | 2時間 | ★★☆ |
| 仕上げ | 2時間 | ★☆☆ |
| **合計** | **約10時間** | |

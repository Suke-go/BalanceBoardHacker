# TheGround - 振動ハプティクス下でのCoP計測システム

## プロジェクト概要

**TheGround**は、Wii Balance Boardを用いた重心動揺（Center of Pressure: CoP）計測において、同時に振動子（Bass Shaker等）を駆動しても正確なCoP計測を可能にする信号分離システムです。

### 研究背景

- 姿勢制御研究やリハビリテーションで、振動ハプティクスを用いたフィードバックが注目されている
- 問題: 振動刺激がフォースプレートのセンサ信号にノイズとして混入
- 解決策: 適応干渉除去アルゴリズムで振動成分をリアルタイムに分離

## システム構成

```
┌─────────────────┐     ┌──────────────────┐
│  Wii Balance    │────▶│   CoP Calculator │
│     Board       │     │  (Raw + Filtered)│
└─────────────────┘     └────────┬─────────┘
                                 │
                                 ▼
┌─────────────────┐     ┌──────────────────┐
│  Audio Output   │────▶│    Adaptive      │
│  (Bass Shaker)  │◀────│   Compensator    │
└─────────────────┘     │    (NLMS/AMHIC)  │
        ▲               └──────────────────┘
        │
  Phase Reference (位相同期)
```

## 技術仕様

### ハードウェア要件

| 項目 | 仕様 |
|------|------|
| フォースプレート | Wii Balance Board (4点ロードセル) |
| サンプリングレート | 60 Hz |
| 振動出力 | 任意のオーディオデバイス → Bass Shaker |
| 接続 | Bluetooth HID |

### ソフトウェア構成

- **プラットフォーム**: .NET 8.0 / Windows Forms
- **依存ライブラリ**:
  - WiimoteLib (Wii Balance Board接続)
  - 32feet.NET (Bluetooth HID設定)
  - NAudio (オーディオ出力)

## 信号処理アルゴリズム

### 1. CoP計算

```
CoP_x = (L/2) × [(TR + BR) - (TL + BL)] / ΣWeight
CoP_y = (W/2) × [(TL + TR) - (BL + BR)] / ΣWeight
```

- L = 433mm (前後距離)
- W = 238mm (左右距離)

### 2. キャリブレーション

- **方式**: 3秒間（180サンプル）の平均値
- **根拠**: ISPGR臨床姿勢計測ガイドライン準拠

### 3. 適応振動補正（AMHIC）

**Adaptive Multi-Harmonic Interference Cancellation**

```
参照信号: r[n] = [sin(ωn), cos(ωn), sin(2ωn), cos(2ωn), sin(3ωn), cos(3ωn)]
推定干渉: d̂[n] = wᵀr[n]
誤差信号: e[n] = CoP[n] - d̂[n]
重み更新: w[n+1] = w[n] + μ·e[n]·r[n] / (‖r[n]‖² + ε)
```

- **手法**: Normalized LMS (NLMS)
- **ハーモニクス**: 3次まで (f, 2f, 3f)
- **パラメータ**: μ=0.1, ε=1e-6
- **収束検出**: MSE分散 < 0.01
- **品質指標**: SNR改善量 (dB)

**理論的根拠**:
- NLMS: 入力パワーに依存しない安定した収束 (Widrow & Stearns, 1985)
- 多周波数補正: 実際の振動子は非線形歪みを持つため高調波が発生

## 評価指標

| 指標 | 説明 | 目標値 |
|------|------|--------|
| SNR改善 | 振動除去による信号品質向上 | > 10 dB |
| 収束時間 | 補正係数が安定するまでの時間 | < 3秒 |
| CoP精度 | 臨床計測精度 | ±0.1 mm (ISPGR) |

## ファイル構成

```
TheGround.PoC/
├── Audio/
│   ├── AudioOutputManager.cs    # オーディオデバイス管理
│   └── SineWaveGenerator.cs     # 信号生成（正弦波/帯域制限ノイズ）
├── BalanceBoard/
│   ├── WiiBalanceBoardReader.cs # Balance Board接続・読み取り
│   ├── CoPCalculator.cs         # CoP計算・キャリブレーション
│   └── BluetoothSetup.cs        # Bluetooth HID設定
├── SignalProcessing/
│   ├── SignalSeparator.cs       # ローパスフィルタ
│   ├── VibrationCompensator.cs  # 基本補正（ノッチ+LMS）
│   └── AdaptiveVibrationCompensator.cs  # NLMS多周波数補正
├── MainForm.cs                  # メインUI
└── MainForm.Designer.cs         # UIレイアウト
```

## 使用方法

1. **接続**: Wii Balance BoardをBluetoothペアリング
2. **BT Setup**: HIDサービスを有効化
3. **Connect**: Balance Boardに接続
4. **Calibrate**: ボードに乗って3秒間静止（キャリブレーション）
5. **Play**: 振動出力開始
6. **Vib. Compensation**: 適応補正を有効化
7. 数秒で収束（✓マーク + SNR表示）

## 参考文献

1. Widrow, B., & Stearns, S. D. (1985). *Adaptive Signal Processing*. Prentice-Hall.
2. ISPGR (2017). *Recommendations for Posturography*.
3. Clark, R. A., et al. (2010). Validity and reliability of the Nintendo Wii Balance Board for assessment of standing balance. *Gait & Posture*.

## ライセンス

MIT License

# GLBモデルの取り込み

Unity 6000.5.6f1向けに、Unity公式のglTFast 6.20.0を追加。

1. Unityでプロジェクトを開き、パッケージの読み込みが終わるまで待つ。
2. ProjectウィンドウのAssets内にモデル用フォルダーを作り、`.glb`ファイルをドラッグする。
3. 取り込んだモデルを展開すると、メッシュ・マテリアル・テクスチャ・アニメーションを確認できる。
4. モデルをシーンにドラッグして表示する。

既存のプレイヤーや敵の見た目を置き換える場合は、モデルをそのキャラクターの子に配置し、元の移動・HP・攻撃処理や当たり判定を残す。モデルの大きさ・向き・原点を確認してから調整する。アニメーションを行動に連動させるには別途設定が必要。

GLBが外部画像などを参照する場合は、参照先ファイルも同じ相対位置でコピーする。

公式手順: [glTFastのEditor Import](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.20/manual/ImportEditor.html)。

## 虎ロボット

- 素材: `Assets/Models/RobotTiger/robot_tiger_animated.glb`。
- 元ファイル: `C:/Users/kaiwa/Downloads/gigafile-1102-e33b873633ee120de4418e62af926b54/robot_tiger_animated_package (3)/robot_tiger_animated.glb`。
- SHA-256: `1D73DCEF5D96222ED3CEE2C63394F73197E6DB05EA6D75D13044F4CF4BDFE645`。
- 内包アニメーション: Idle、Walk、Run、Run_Forward、Bite、Roar、ClawSwipe。
- GLB 2.0形式。画像を内包し、外部ファイルへの参照や必須の圧縮拡張はない。

モデル素材として追加。プレイヤー・敵・味方への割り当てと、行動に合わせたアニメーションの設定は用途の指定後に行う。

2026-10-28、Unity 6000.5.6f1で取り込みと表示を確認。メッシュ2点、マテリアル3点、テクスチャ3点、上記7クリップを読み込み、全クリップに再生時間があることを確認した。結果: `.utmp/glb-import-validation.txt`、表示画像: `.utmp/robot-tiger-import.png`。

元GLBは使用拡張に`KHR_materials_volume`を宣言しているため、未対応拡張の警告が表示される。必須拡張ではなく、各マテリアルに同拡張の設定もない。GLB自体は元データを保持している。

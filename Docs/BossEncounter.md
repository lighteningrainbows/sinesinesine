# 強敵ロボットと専用エリア

仕様元: `ぷろえんあき.xlsx`「強敵ロボット」。

- 昆虫型のクモ型ロボット。HP400、移動3m/s、当たり判定3×3×3m。
- 弱攻撃: 攻撃力20、予備動作0.5秒、攻撃後2秒待機、範囲1.5×1.5m。
- 強攻撃: 攻撃力40、予備動作2秒、攻撃後4秒待機、範囲2×2m。
- 通常の抽選は弱70%・強30%。強を選択した直後の攻撃は必ず弱。中断された強攻撃の次も弱とする。
- 予備動作中は地面に矩形の警告を表示。弱は橙、強は赤。命中時に再判定するため回避可能。
- 生存しているプレイヤーと出撃中の仲間からランダムに標的を選択。標的を失ったら再選択する。
- 被弾時は通常のα版敵と同じく、0.3m吹き飛び→0.2秒空中停止→落下→着地後1秒硬直。追撃時は反応を最初からやり直す。
- 撃破時はプレイヤーと所持している全仲間にそれぞれ経験値500。控え・戦闘不能・属性に関係なく付与する。

## 場所と開始条件

`Assets/Scenes/Game/ExplorationArea.unity` の `BossArena` が専用エリア。
中心は `(67.6, 0, 67.6)`、床は30×30m。開始地点側の広場から北へ進み、橙の目印と `BOSS ARENA` 看板のある橋を渡る。

エリアへの初回進入で1体だけ出現する。離脱した場合は待機位置へ戻り、再進入時は同じ個体・残りHPで再開する。撃破後はそのプレイ中には再出現しない。通常敵の30秒リスポーンとは独立している。

`BossEncounter` の `Spawn On Enter` をオフにし、イベントから `BeginEncounter()` を呼べば、会話などのストーリーイベント終了後に出現させることもできる。開始・撃破イベントもInspectorから接続可能。

## アセットと調整

- Prefab: `Assets/Prefabs/Enemy/SpiderBoss.prefab`
- 基本値と弱攻撃: `Assets/ScriptableObjects/Enemies/SpiderBoss.asset`
- 強攻撃と確率: `Assets/ScriptableObjects/Battle/SpiderBossAttacks.asset`
- モデルは基本形状で作った仮モデル。Colliderは装飾から分離している。

`Tools > Enemies > Validate Spider Boss Assets` で設定を検証できる。
`Tools > Enemies > Create Spider Boss and Arena` は初期設定を再適用し、探索シーンの `BossArena` を作り直す。手動調整したモデル・専用エリアは上書きされるため、通常の再生時に実行する必要はない。

バッチ検証は `EnemyAlphaValidation.RunBossBatch`。通常敵の検証も実施したうえで、実際の探索シーンに入り、出現条件、二つの攻撃、被弾、標的変更、経験値、再進入を検証する。結果は `.utmp/boss-validation.txt`。

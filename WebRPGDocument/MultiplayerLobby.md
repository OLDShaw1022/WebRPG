# 房間大廳

- 設計狀態：已核定第一批範圍為建立／加入／離開、公開列表、最多 4 人與準備狀態。下列離線房主接任及私人房間細節屬本批可逆的原型行為。
- 實作狀態：已實作房間大廳與多人共同冒險；帳號、邀請及持久保存尚未提供。
- 適用範圍：Unity 多人入口、WebRPGServer 房間服務與 WebRPGShared 大廳協定。
- 用途：記錄第一批流程、組態、協定及驗證方式。

## 規則

- 1：主選單「多人遊玩」開啟大廳，選擇伺服器、輸入玩家名稱並按連線；名稱為顯示用途，伺服器每次連線另配發暫時 PlayerId，不視為持久帳號或身分驗證。
- 2：連線後可建立房間，建立者自動加入並擔任隊長。房名為 1～40 個字元；每人同時只能加入一間房。
- 3：公開列表顯示房名與目前／上限人數，可直接點選加入；私人房間不列出，知道完整房間 ID 的玩家仍可加入。私人房間不提供密碼保護。
- 4：房間容量由伺服器 JSON 設定，協定允許 1～4 人，預設為 4。滿房及重複加入由伺服器拒絕，不信任用戶端提供的玩家識別碼。
- 5：房內玩家可切換準備／取消準備，伺服器向所有大廳連線推播公開列表，並向各連線提供自己房間的完整快照。全員準備後由隊長按開始進入共同冒險。
- 6：離開、返回主選單或伺服器偵測到連線中斷時，移除成員；空房刪除。隊長離開時由加入最早的剩餘成員接任；進行中的冒險仍依下列規則全隊失敗。重新加入時準備狀態為未準備。
- 7：客戶端斷線或要求逾時會清空本機房間狀態，顯示重新連線入口；不自動恢復房間。伺服器重啟後，記憶體中的房間全部清空。
- 8：房間操作由伺服器序列化處理，避免同時加入超過容量；格式錯誤、不支援操作、過大訊息均回覆 Error。
- 9：固定 UI 保存在 LobbyCanvas.prefab，公開列表由 RoomTemplate 動態生成；Main 組裝 Model、View、Presenter 並管理釋放。玩家提供的文字不解析 TMP rich text。

## 設定與連線

- [LobbyClient.json](../WebRPG/Assets/WebRPG/Data/LobbyClient.json)：以 DefaultServerId 指定預設伺服器，Servers 陣列設定 Id、Name、Url、EditorOnly。預設為 AWS EC2，透過 `wss://ws.oldshaw-webrpg.com/ws?channel=lobby` 連線；本機測試只在 Editor 顯示。RequestTimeoutSeconds 預設為 10 秒。畫面使用 TMP_Dropdown，不提供網址輸入。非 Editor 選項必須使用 wss。
- [appsettings.json](../WebRPGServer/appsettings.json)：Lobby.RoomCapacity、MaximumRooms、MaximumConnections、SendTimeoutSeconds，預設分別為 4、100、256、3。均為本批原型設定。
- 原有 `/ws` 聊天協定保留；`/ws?channel=lobby` 使用 LobbyPacket。部署時須轉送 query string。公開網站使用時應配置對應的 wss 位址。
- 要求類型為 Hello、ListRooms、CreateRoom、JoinRoom、LeaveRoom、SetReady；回應為 Welcome、State 或 Error。RequestId 配對要求與回應，無 RequestId 的 State 為推播。
- 房間與成員只存在單一伺服器程序記憶體，尚未支援多個伺服器程序間共用狀態。

## 驗收與操作

1. 在儲存庫根目錄執行 `dotnet run --project WebRPGServer --no-launch-profile`，預設 PORT 為 8080。
2. Unity 開啟 Main 場景並播放，選擇「多人遊玩」，選擇 AWS EC2 或 Editor 專用本機測試，輸入不同玩家名稱連線。
3. 建立公開房間，其他客戶端可在列表加入；最多 4 人，第 5 人加入會被拒絕。
4. 切換準備狀態，其他房內成員同步顯示；離開或關閉連線後，成員及列表更新。
5. 返回主選單後可再次進入大廳，亦可開始既有單人冒險。
6. 執行 `dotnet run --project WebRPGServer.Tests/WebRPGServer.Tests.csproj` 驗證真實 WebSocket 的容量、列表、準備、房主接任、斷線與無效輸入。此為無外部測試套件的可執行整合測試，會啟動並關閉獨立本機伺服器。
7. Unity EditMode 執行 LobbyPrefabTests、LobbyPresenterTests、LobbyServerSelectionTests、MainMenuPresenterTests、MainMenuPrefabTests 與 AdventureStartupTests。

## 相關文件

- [主選單](MainMenu.md)：多人入口與返回主選單。
- [主玩法](CoreGameplay.md)：完整多人遊戲設計與尚未核定規則。
- [單人可玩原型](AdventurePrototype.md)：目前獨立運作的單人流程。

## 多人冒險同步

- 1：全員準備後由隊長開始，伺服器產生共用種子；開局後房間從公開列表移除，禁止新隊員加入。每名玩家保有獨立生命、技能、冷卻、金幣與獎勵抽選。
- 2：地圖節點由全員投票，結算前可改票；全員投票才結算，不設投票逾時。票數最多者獲選；平票時優先採隊長所投節點，若隊長未投平票候選則採候選索引最小者。
- 3：寶箱、營火、事件與戰後技能獎勵由每名玩家自行決定。完成者等待所有隊員到達同一地圖列後，才可再次投票；沒有技能獎勵者自動完成該次領獎。
- 4：全隊共用敵人生命、敵方動作、戰鬥時限與投射物排程。敵人每次發動動作時，從存活玩家中等機率隨機選一名目標；使用獨立共用亂數串流，避免個人領獎改變戰鬥或地圖結果。
- 5：伺服器為操作分配遞增 Sequence，並以 Turn 隔離不同地圖節點。技能、鎖定、個人節點選擇、領獎與投票經伺服器廣播後才套用；隊長約每 0.1 秒送出固定 0.05 秒的模擬步數。各客戶端依同一順序推進全隊模型，不再接受客戶端上報傷害或畫面狀態。
- 6：倒地隊員停止行動且不成為敵方目標，仍可旁觀；戰勝後按 JSON 比例復活並領取個人獎勵。全隊倒下或戰鬥逾時則整輪失敗。
- 7：冒險中任一隊員離開或斷線，全隊立即失敗。大廳仍可接任隊長，但不能延續該輪冒險。伺服器無回應達 NetworkTimeoutSeconds 時，本機終止該輪並嘗試通知其餘成員；不製作斷線續玩。
- 8：多人固定使用共同的一倍速；查看地圖或開啟中央選單不暫停。多人結算後需回主選單重新組隊開始；單人重新挑戰流程維持原行為。
- 9：冒險狀態只存在於客戶端與單一伺服器程序記憶體。伺服器驗證隊長權限、操作範圍、回合與時間推進上限，但尚未在伺服器完整模擬戰鬥；此架構不提供完整防作弊保證。客戶端與伺服器必須使用同批協定版本部署。

### 原型 JSON 設定

位置：[AdventurePrototype.json](../WebRPG/Assets/WebRPG/Data/AdventurePrototype.json) 的 Multiplayer 區段。

| 欄位 | 原型值 | 行為 |
| --- | --- | --- |
| EnemyHealthPerAdditionalPlayer | 1 | 敵人生命乘以 1 + (隊員數 - 1) × 此值，向上取整；預設等於隊員數倍。 |
| EnemyDamagePerAdditionalPlayer | 0 | 敵人傷害使用相同倍率形式，預設不增加；敵方治療隨生命倍率增加。 |
| RevivalHealthFraction | 0.2 | 戰勝時倒地隊員恢復最大生命的此比例，向上取整且至少 1 點。 |
| NetworkTimeoutSeconds | 10 | 連續未收到已排序封包的本機終止秒數；不等同投票逾時。 |

上述強化與復活為 2026-11-02 確認的可調整原型值，不代表最終平衡。

## 待確認

- 持久玩家 ID、帳號／驗證、邀請與防濫用規則。
- 正式戰鬥平衡、投票／領獎逾時機制、多人同房直接重開與伺服器完整模擬。
- 多程序部署與房間資料保存。



## 本次驗證與備份紀錄（2026-11-02）

- 根目錄 solution 建置成功，0 個警告、0 個錯誤。Unity 的大廳、主選單、單人啟動測試及真實 WebSocket 整合測試均已執行。
- Unity Play Mode 已確認四個實際客戶端收到相同四人成員快照、準備狀態更新、返回主選單後伺服器移除成員，且可啟動單人冒險。最長房名及四名最長玩家名稱另以 Prefab 渲染測試確認無裁切。
- DLL/PDB 已更新且與 WebRPGShared 建置輸出 SHA-256 相同。原有字型修改保留，不納入本批程式處理；尚未建置或驗證 WebGL 成品。
- 所有備份以本機記錄的 origin/main `40d82a5d0acd3a4cc8f0ec90f6ecafc060d07d76` 為比較基準。以下備份建立時尚未推送；推送後依 AGENTS.md 檢查保護內容是否已完整包含於遠端，再清理可刪除的 reference。備份用獨立 Git 暫存區建立，未變動原暫存區。
- 需要還原時，僅依使用者指定的 reference 與路徑執行 `git restore --source=<reference> --worktree -- "<指定路徑>"`；以下僅記錄方式，本次未執行還原。

### refs/backups/20261102-140721-lobby-shared-artifacts

涵蓋路徑：

- `WebRPG/Assets/Plugins/WebRPGShared.dll`
- `WebRPG/Assets/Plugins/WebRPGShared.pdb`

### refs/backups/20261102-142017-lobby-client-settings

涵蓋路徑：

- `WebRPG/Assets/WebRPG/Code/Bootstrap/Main.cs`
- `WebRPG/Assets/WebRPG/Code/Lobby/LobbyModel.cs`
- `WebRPG/Assets/WebRPG/Code/Lobby/LobbyView.cs`

### refs/backups/20261102-142113-lobby-scene-settings

涵蓋路徑：

- `WebRPG/Assets/WebRPG/Scenes/Main.unity`

### refs/backups/20261102-142709-lobby-member-layout

涵蓋路徑：

- `WebRPG/Assets/WebRPG/Code/Lobby/LobbyPresenter.cs`

### refs/backups/20261102-142908-lobby-layout-test

涵蓋路徑：

- `WebRPG/Assets/WebRPG/Tests/Editor/LobbyPrefabTests.cs`

### refs/backups/20261102-143001-lobby-four-player-panel

涵蓋路徑：

- `WebRPG/Assets/WebRPG/Prefabs/LobbyCanvas.prefab`
- `WebRPG/Assets/WebRPG/Prefabs/LobbyCanvas.prefab.meta`

### refs/backups/20261102-143408-lobby-final-review

涵蓋路徑：

- `WebRPG/Assets/WebRPG/Code/Lobby/LobbyModel.cs`
- `WebRPG/Assets/WebRPG/Code/Lobby/LobbyPresenter.cs`
- `WebRPG/Assets/WebRPG/Code/Lobby/LobbyRoomRowView.cs`
- `WebRPG/Assets/WebRPG/Code/Lobby/LobbyView.cs`
- `WebRPG/Assets/WebRPG/Code/Lobby/LobbyClientConfiguration.cs`
- `WebRPG/Assets/WebRPG/Data/LobbyClient.json`
- `WebRPG/Assets/WebRPG/Code/Bootstrap/Main.cs`
- `WebRPG/Assets/WebRPG/Code/MainMenu/MainMenuView.cs`
- `WebRPG/Assets/WebRPG/Code/MainMenu/MainMenuPresenter.cs`
- `WebRPG/Assets/WebRPG/Tests/Editor/MainMenuPresenterTests.cs`
- `WebRPGShared/LobbyPacket.cs`
- `WebRPGServer/LobbyOptions.cs`
- `WebRPGServer/LobbyRegistry.cs`
- `WebRPGServer/LobbySocketHelper.cs`
- `WebRPGServer/LobbyHub.cs`
- `WebRPGServer/appsettings.json`
- `WebRPGServer/Program.cs`
- `WebRPGServer.Tests/WebRPGServer.Tests.csproj`
- `WebRPGServer.Tests/Program.cs`
- `WebRPG/Assets/WebRPG/Tests/Editor/LobbyPrefabTests.cs`
- `WebRPGDocument/MainMenu.md`
- `WebRPGDocument/Document Directory.md`
- `WebRPGDocument/CoreGameplay.md`
- `WebRPGDocument/AdventurePrototype.md`
- `WebRPGDocument/MultiplayerLobby.md`
- `WebRPG/Assets/WebRPG/Tests/Editor/LobbyPresenterTests.cs`
- `WebRPG/Assets/WebRPG/Tests/Editor/WebRPG.Tests.Editor.asmdef`
- `.gitignore`
- `WebRPG/Assets/Screenshots/lobby-review.png`
- `WebRPG/Assets/Screenshots/lobby-review.png.meta`
- `WebRPG/Assets/Screenshots.meta`
### refs/backups/20261102-143510-lobby-final-build

涵蓋路徑：

- `WebRPG/Assets/Plugins/WebRPGShared.dll`
- `WebRPG/Assets/Plugins/WebRPGShared.pdb`
- `WebRPGDocument/MultiplayerLobby.md`


## EC2 與 WebGL 部署

- 由既有 `.github/workflows/deploy-server-to-ec2.yml` 部署伺服器，先執行本機 WebSocket 整合測試，再 publish、同步到現有 EC2 並重啟 webrpg。限制 main 且要求部署 revision 為最新 main。
- EC2 部署後，workflow 透過正式 WSS 端點驗證建立／列出／加入／準備／離開／清理；只操作自己建立的短期驗證房間。
- 由既有 `.github/workflows/deploy-web-to-firebase.yml` 建置 WebGL 並部署至 Firebase。CI 建置方法驗證正式伺服器目錄，公開版本只顯示可用的安全端點。
- 觸發命令：`gh workflow run deploy-server-to-ec2.yml --ref main -f confirm_deployment=deploy`，以及 `gh workflow run deploy-web-to-firebase.yml --ref main`。
- 正式遊戲網址：https://oldshaw-web-rpg.web.app/ 。實際部署結果以對應 GitHub Actions run 為準。

### refs/backups/20261102-145037-ec2-server-selector

新增伺服器選單與遠端驗證前的本機備份；比較基準為 origin/main `40d82a5d0acd3a4cc8f0ec90f6ecafc060d07d76`，涵蓋路徑：

- `WebRPG/Assets/WebRPG/Code/Lobby/LobbyClientConfiguration.cs`
- `WebRPG/Assets/WebRPG/Data/LobbyClient.json`
- `WebRPG/Assets/WebRPG/Code/Lobby/LobbyModel.cs`
- `WebRPG/Assets/WebRPG/Code/Lobby/LobbyView.cs`
- `WebRPG/Assets/WebRPG/Code/Lobby/LobbyPresenter.cs`
- `WebRPG/Assets/WebRPG/Code/Bootstrap/Main.cs`
- `WebRPG/Assets/WebRPG/Tests/Editor/LobbyPresenterTests.cs`
- `WebRPG/Assets/WebRPG/Tests/Editor/LobbyPrefabTests.cs`
- `WebRPG/Assets/WebRPG/Tests/Editor/LobbyServerSelectionTests.cs`
- `WebRPG/Assets/WebRPG/Prefabs/LobbyCanvas.prefab`
- `WebRPG/Assets/WebRPG/Prefabs/LobbyCanvas.prefab.meta`
- `WebRPGServer.Tests/Program.cs`
- `WebRPGDocument/MultiplayerLobby.md`
- `WebRPG/Assets/Plugins/WebRPGShared.dll`
- `WebRPG/Assets/Plugins/WebRPGShared.pdb`
### refs/backups/20261102-150430-ec2-deployment-review

部署前的驗證圖片與文件備份，涵蓋路徑：

- `WebRPG/Assets/Screenshots.meta`
- `WebRPG/Assets/Screenshots/ec2-server-selector.png`
- `WebRPG/Assets/Screenshots/ec2-server-selector.png.meta`
- `WebRPG/Assets/Screenshots/ec2-server-selector-final.png`
- `WebRPG/Assets/Screenshots/ec2-server-selector-final.png.meta`
- `WebRPGDocument/MultiplayerLobby.md`

驗證圖片移至根目錄已忽略的 artifacts，不納入遊戲資產或提交。

## 多人冒險完成與還原紀錄（2026-11-02）

- 本批實作全員節點投票、共用戰鬥、個人節點與獎勵、領獎等待、戰勝復活、組隊強化、隨機存活目標，以及任一隊員離線時全隊失敗。
- `dotnet build WebRPG.sln` 通過（0 警告／0 錯誤）；伺服器規則與真實 WebSocket 整合測試通過。Unity EditMode 101 項測試通過，涵蓋兩人／四人獨立模型重播、四人三層通關、隨機存活目標、倒地復活、共用敵人及領獎屏障。
- Unity Play Mode 以四個獨立 NativeWebSocket 客戶端確認開局、投票、共同戰鬥、手動技能與鎖定、個別領獎等待，以及關閉一個連線後全隊失敗；四端收到相同的敵人生命狀態。已確認幀數持續推進。隨機目標的最後調整另由 EditMode 測試驗證。
- 驗證截圖位於儲存庫根目錄 `artifacts/multiplayer-validation/`；未新增為遊戲資產。Play Mode 測試客戶端、暫時委派及本機測試伺服器已清理。
- 共用 DLL/PDB 由建置更新，SHA-256 已確認與 `WebRPGShared/bin/Debug/netstandard2.1/` 對應產物一致；既有字型變更保留。尚未建置或驗證 WebGL 成品，未提交、推送或部署；正式站需成對更新伺服器與客戶端。

### 本批程式備份

Reference：`refs/backups/20261102-1793619821262-complete-multiplayer`

比較與還原基準：本機記錄的 `origin/main`，`d94eea222bf92f31d3afff15235b876f7c68fdf4`。

涵蓋以下原有未推送變更（包含本批接續的未追蹤多人腳本），未包含字型：

- `WebRPG/Assets/Plugins/WebRPGShared.dll`
- `WebRPG/Assets/Plugins/WebRPGShared.pdb`
- `WebRPG/Assets/WebRPG/Code/Adventure/AdventureBattleModel.cs`
- `WebRPG/Assets/WebRPG/Code/Adventure/AdventureModel.cs`
- `WebRPG/Assets/WebRPG/Code/Adventure/AdventurePresenter.cs`
- `WebRPG/Assets/WebRPG/Code/Adventure/AdventureView.cs`
- `WebRPG/Assets/WebRPG/Code/Adventure/MultiplayerAdventureSession.cs`
- `WebRPG/Assets/WebRPG/Code/Adventure/MultiplayerAdventureSession.cs.meta`
- `WebRPG/Assets/WebRPG/Code/Bootstrap/Main.cs`
- `WebRPG/Assets/WebRPG/Code/Lobby/LobbyModel.cs`
- `WebRPG/Assets/WebRPG/Code/Lobby/LobbyPresenter.cs`
- `WebRPG/Assets/WebRPG/Tests/Editor/LobbyPresenterTests.cs`
- `WebRPGDocument/MultiplayerLobby.md`
- `WebRPGServer/LobbyHub.cs`
- `WebRPGServer/LobbyRegistry.cs`
- `WebRPGShared/LobbyPacket.cs`
- `WebRPGShared/AdventurePacket.cs`

`AdventureConfiguration.cs`、`AdventurePrototype.json`、伺服器測試 `Program.cs` 及本批其餘既有文件在修改前相對上述基準沒有未推送變更，記錄該基準而未另建備份；本批新增檔案原先不存在。備份使用獨立 Git index，沒有改動原暫存區。Reference 的父提交另保留涵蓋路徑的原暫存狀態。

僅在使用者要求還原時，針對指定路徑執行：

```powershell
git restore --source=refs/backups/20261102-1793619821262-complete-multiplayer --worktree -- "<指定路徑>"
```

若使用者也要求還原該路徑原暫存狀態，可另使用此 reference 的父提交作為 `--staged` 來源；不處理無關路徑。本批未執行還原，亦未刪除備份。

### 驗證截圖搬移前備份

Reference：`refs/backups/20261102-1793621335787-multiplayer-captures`。基準同上；涵蓋工具自動產生的下列路徑：

- `WebRPG/Assets/artifacts.meta`
- `WebRPG/Assets/artifacts/multiplayer-playmode-20261102.png`
- `WebRPG/Assets/artifacts/multiplayer-playmode-20261102.png.meta`
- `WebRPG/Assets/artifacts/multiplayer-battle-20261102.png`
- `WebRPG/Assets/artifacts/multiplayer-battle-20261102.png.meta`

圖片及 metadata 已移至根目錄的驗證產物目錄，原 Assets/artifacts 空資料夾已移除。只需要還原指定截圖時，可使用上方相同的 `git restore` 方式，將來源換成此 reference 並指定完整圖片路徑。

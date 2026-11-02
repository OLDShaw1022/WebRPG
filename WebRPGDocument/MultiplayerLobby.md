# 房間大廳

- 設計狀態：已核定第一批範圍為建立／加入／離開、公開列表、最多 4 人與準備狀態。下列離線房主接任及私人房間細節屬本批可逆的原型行為。
- 實作狀態：已實作房間大廳；尚未提供共同冒險、開始挑戰、帳號與邀請。
- 適用範圍：Unity 多人入口、WebRPGServer 房間服務與 WebRPGShared 大廳協定。
- 用途：記錄第一批流程、組態、協定及驗證方式。

## 規則

- 1：主選單「多人遊玩」開啟大廳，選擇伺服器、輸入玩家名稱並按連線；名稱為顯示用途，伺服器每次連線另配發暫時 PlayerId，不視為持久帳號或身分驗證。
- 2：連線後可建立房間，建立者自動加入並擔任隊長。房名為 1～40 個字元；每人同時只能加入一間房。
- 3：公開列表顯示房名與目前／上限人數，可直接點選加入；私人房間不列出，知道完整房間 ID 的玩家仍可加入。私人房間不提供密碼保護。
- 4：房間容量由伺服器 JSON 設定，協定允許 1～4 人，預設為 4。滿房及重複加入由伺服器拒絕，不信任用戶端提供的玩家識別碼。
- 5：房內玩家可切換準備／取消準備，伺服器向所有大廳連線推播公開列表，並向各連線提供自己房間的完整快照。全員準備不會開始挑戰。
- 6：離開、返回主選單或伺服器偵測到連線中斷時，移除成員；空房刪除。隊長離開時由加入最早的剩餘成員接任。重新加入時準備狀態為未準備。
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

## 待確認

- 持久玩家 ID、帳號／驗證、邀請與防濫用規則。
- 開始挑戰權限、全員準備條件及挑戰中加入／退出的處理。
- 多人戰鬥同步、投票完成與逾時、各別領獎等待、隊伍強化公式及復活血量。
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

# 房間大廳

- 設計狀態：已核定第一批範圍為建立／加入／離開、公開列表、最多 4 人與準備狀態。下列離線房主接任及私人房間細節屬本批可逆的原型行為。
- 實作狀態：已實作房間大廳與多人共同冒險；帳號程式已接入，帳號測試方法見[帳號登入](Accounts.md#測試與驗收)；邀請及房間持久保存尚未提供。
- 適用範圍：Unity 多人入口、WebRPGServer 房間服務與 WebRPGShared 大廳協定。
- 用途：記錄第一批流程、組態、協定及驗證方式。

## 規則

- 1：先完成[帳號登入](Accounts.md)，從主選單選擇「多人遊玩」後先進入大廳瀏覽頁，沿用登入伺服器與使用者名稱自動連線；PlayerId 使用伺服器驗證後的固定帳號 ID，名稱允許重複，不能從大廳改名或切換伺服器。
- 2：大廳瀏覽頁提供公開房間列表、建立房間、依房間 ID 加入及返回主選單。建立者自動加入並擔任隊長。建立或加入成功後切換至房間頁；失敗時留在大廳瀏覽頁並顯示原因。房名為 1～40 個字元；每人同時只能加入一間房。
- 3：公開列表顯示房名與目前／上限人數，可直接點選加入；私人房間不列出，知道完整房間 ID 的玩家仍可加入。私人房間不提供密碼保護。
- 4：房間容量由伺服器 JSON 設定，協定允許 1～4 人，預設為 4。滿房及重複加入由伺服器拒絕，不信任用戶端提供的玩家識別碼。
- 5：房間頁顯示目前房間的名稱、ID、成員、隊長與準備狀態，並提供準備／取消準備及離房。伺服器向所有大廳連線推播公開列表，並向各連線提供自己房間的完整快照。全員準備後由隊長按開始進入共同冒險。
- 6：離開、返回主選單或伺服器偵測到連線中斷時，移除成員；空房刪除。離房成功後回到大廳瀏覽頁並更新列表。隊長離開時由加入最早的剩餘成員接任；進行中的冒險仍依下列規則全隊失敗。重新加入時準備狀態為未準備。
- 7：客戶端斷線或要求逾時會清空本機房間狀態並回到大廳瀏覽頁。連線失敗或斷線時，可返回主選單再選擇「多人遊玩」重試。重新連線後須重新加入房間。伺服器重啟後，記憶體中的房間全部清空。
- 8：房間操作由伺服器序列化處理，避免同時加入超過容量；格式錯誤、不支援操作、過大訊息均回覆 Error。
- 9：固定 UI 保存在 LobbyCanvas.prefab，公開列表由 RoomTemplate 動態生成；Main 組裝 Model、View、Presenter 並管理釋放。玩家提供的文字不解析 TMP rich text。
- 10：房間頁的紀錄區（Log）可垂直捲動，顯示成員加入、離開等房內事件及同房聊天，底部提供文字輸入與送出。進入冒險後，可在冒險紀錄區繼續同一房間的聊天；訊息生命週期依[房間聊天](RoomChat.md)。

## 設定與連線

- [LobbyClient.json](../WebRPG/Assets/WebRPG/Data/LobbyClient.json)：以 DefaultServerId 指定預設伺服器，Servers 陣列設定 Id、Name、Url、EditorOnly。預設為 AWS EC2，透過 `wss://ws.oldshaw-webrpg.com/ws?channel=lobby` 連線；遊戲頁面使用 `https://oldshaw-webrpg.com`，帳號 API 使用 ws 子網域並允許指定遊戲來源的 Cookie 要求，詳見[帳號登入](Accounts.md)的部署要求；本機測試只在 Editor 顯示。RequestTimeoutSeconds 預設為 10 秒。登入畫面使用 TMP_Dropdown 選擇伺服器。非 Editor 選項必須使用 wss。
- [appsettings.json](../WebRPGServer/appsettings.json)：Lobby.RoomCapacity、MaximumRooms、MaximumConnections、SendTimeoutSeconds，預設分別為 4、100、256、3。均為本批原型設定。
- `/ws?channel=lobby` 使用 LobbyPacket，包含房間聊天。舊獨立聊天分流已停用。部署時須轉送 query string。公開網站使用時應配置對應的 wss 位址。
- 要求類型為 Hello、ListRooms、CreateRoom、JoinRoom、LeaveRoom、SetReady；回應為 Welcome、State 或 Error。RequestId 配對要求與回應，無 RequestId 的 State 為推播。
- 房間與成員只存在單一伺服器程序記憶體，尚未支援多個伺服器程序間共用狀態。

## 驗收與操作

1. 在儲存庫根目錄執行 `dotnet run --project WebRPGServer --no-launch-profile`，預設 PORT 為 8080。
2. Unity 開啟 Main 場景並播放，選擇已部署帳號 API 的伺服器並完成註冊／登入，再選擇「多人遊玩」，確認大廳自動連上同一伺服器；多人驗證使用不同帳號，可有相同使用者名稱。原匿名正式伺服器需配套升級後才能使用新版客戶端。
3. 進入多人模式先顯示大廳瀏覽頁。建立公開房間後，其他客戶端可從列表加入。建立或加入成功才顯示房間頁與紀錄區；第 5 人加入預設容量 4 人的房間時被拒絕，仍停留在大廳瀏覽頁。
4. 切換準備狀態，其他房內成員同步顯示。同房玩家可在紀錄區收發聊天並辨識加入或離開事件。離房後回到更新過的大廳列表；重新加入時不顯示上次連線的聊天。
5. 返回主選單後可再次進入大廳，亦可開始既有單人冒險。
6. 執行 `dotnet test WebRPGServer.Tests/WebRPGServer.Tests.csproj`。測試採用 NUnit，分類為 Unit、Component 與 Integration；本機整合測試會自動啟動及關閉獨立伺服器。遠端測試標記為 Explicit，需以 `--filter FullyQualifiedName~RemoteLobbyTests` 明確選取，並透過環境變數 `WEBRPG_TEST_SERVER_URL` 或 NUnit 的 `TestRunParameters` 提供 `ServerUrl`；預設不連線遠端。
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
- 5：伺服器為操作分配遞增 Sequence，並以 Turn 隔離不同地圖節點。技能、鎖定、個人節點選擇、領獎與投票經伺服器廣播後才套用；隊長約每 0.1 秒送出時間推進封包，每步對應 0.05 秒。客戶端依目前倍率執行 0、1 或 2 倍的固定步數，暫停時仍送出封包以維持連線。各客戶端依同一順序推進全隊模型，不再接受客戶端上報傷害或畫面狀態。
- 6：倒地隊員停止行動且不成為敵方目標，仍可旁觀；戰勝後按 JSON 比例復活並領取個人獎勵。全隊倒下或戰鬥逾時則整輪失敗。
- 7：冒險中任一隊員離開或斷線，全隊立即失敗。大廳仍可接任隊長，但不能延續該輪冒險。伺服器無回應達 NetworkTimeoutSeconds 時，本機終止該輪並嘗試通知其餘成員；不製作斷線續玩。
- 8：多人開局預設 1 倍速；只有隊長能切換暫停、1 倍與 2 倍，所有隊員收到伺服器廣播後同步套用。查看地圖或開啟中央選單不會觸發暫停。多人結算後需回主選單重新組隊開始；單人重新挑戰流程維持原行為。
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

- 邀請與進階防濫用規則；固定帳號 ID、登入與帳號 API 限流見[帳號登入](Accounts.md)。
- 正式戰鬥平衡、投票／領獎逾時機制、多人同房直接重開與伺服器完整模擬。
- 多程序部署與房間資料保存。

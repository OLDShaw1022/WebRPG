# 單人可玩原型

- 設計狀態：已核定；第一版採單人完整循環，多人、資料庫與局外成長留待後續。
- 實作狀態：已實作本文列出的單人範圍；不代表[主玩法](CoreGameplay.md)中的多人與長期系統已完成。
- 適用範圍：現行 Unity 單人挑戰原型。
- 用途：記錄可玩範圍、程式分工、設定入口、實作限制與驗證基準。

## 第一版範圍

- 1：提供三層地圖、自動普攻與技能、戰後選技與升級、遺物、營火、事件、成功／失敗結算及重新挑戰。
- 2：單人生命歸零、戰鬥逾時或主動放棄時，本輪失敗。開始新一輪會清除當輪金幣、技能成長、遺物與所選路徑，依設定重新初始化。
- 3：金幣與進度僅存在本次執行的記憶體，結算供本輪檢視；未接入帳號、資料庫、局外成長、儲值或挑戰續玩。
- 4：未接入多人房間、邀請、節點投票、隊伍強化、隊友復活或分別領獎等待流程。既有聊天／WebSocket 程式不代表已提供多人挑戰。

## 架構與入口

| 檔案 | 職責 |
| --- | --- |
| [Main.cs](../WebRPG/Assets/WebRPG/Code/Bootstrap/Main.cs) | 啟動時顯示主選單；選擇單人遊玩後建立冒險 Model、呼叫 StartRun，再建立 View 與 Presenter，逐幀推進並管理釋放。 |
| [AdventureConfiguration.cs](../WebRPG/Assets/WebRPG/Code/Adventure/AdventureConfiguration.cs) | JSON 對應資料結構與設定驗證。 |
| [AdventureModel.cs](../WebRPG/Assets/WebRPG/Code/Adventure/AdventureModel.cs) | 純 C# 挑戰狀態、地圖生成、戰鬥、獎勵、節點處理與已選路徑。 |
| [AdventurePresenter.cs](../WebRPG/Assets/WebRPG/Code/Adventure/AdventurePresenter.cs) | 將 View 操作轉為 Model 指令，產生本機 HUD、角色檢視、讀條與地圖顯示資料。 |
| [AdventureView.cs](../WebRPG/Assets/WebRPG/Code/Adventure/AdventureView.cs) | 綁定 Prefab 中的固定元件、顯示畫面資料並送出互動事件；依地圖資料生成節點與連線範本實例。 |
| [AdventureCanvas.prefab](../WebRPG/Assets/WebRPG/Prefabs/AdventureCanvas.prefab) | 保存 HUD、戰鬥區、選項區、技能列、地圖容器、詳細資訊視窗及地圖範本，固定版面可在 Prefab Mode 編輯。 |

- 狀態流程：Ready → Map → Battle／NodeChoice → SkillReward（有候選技能時）→ 下一節點；通過第三層後進入 Complete，失敗或放棄進入 Failed。NodeChoice 處理完、或戰勝卻無技能可抽時，直接推進節點。
- 遊戲入口與選項行為見[主選單](MainMenu.md)。單人遊玩會直接進入 Map，不需再按一次「開始挑戰」；Ready 仍是 AdventureModel 建立後、StartRun 前的初始狀態。
- View 不自行計算戰鬥結果；上方本機資料與下方角色檢視資料分開，由 Presenter 提供。
- UI 配置的單一規格來源為[挑戰介面](AdventureUI.md)。
- 固定 UI 的位置、樣式與引用保存在 Prefab；地圖節點及連線依資料動態生成。地圖、詳細資訊等非預設顯示的面板可在 Prefab Mode 暫時啟用以編輯，執行時由 View 依遊戲狀態切換。
- [AdventureUiHelper.cs](../WebRPG/Assets/WebRPG/Code/Adventure/AdventureUiHelper.cs) 負責事件綁定、讀條及互動狀態的共用處理；[AdventureDynamicLayoutHelper.cs](../WebRPG/Assets/WebRPG/Code/Adventure/AdventureDynamicLayoutHelper.cs) 只排列依資料變動的角色、技能與地圖內容，相關尺寸及間距可在 AdventureView 的 Layout Settings 中調整。

## JSON 設定入口

設定檔：[AdventurePrototype.json](../WebRPG/Assets/WebRPG/Data/AdventurePrototype.json)。目前在開始建立單人 Model 時載入與驗證；不提供進行中挑戰的熱更新。現有數值是原型設定，不視為正式平衡規格。

| 設定 | 現行用途與限制 |
| --- | --- |
| StartingHealth、StartingSkills | 本輪初始生命與技能。 |
| BasicAttack.Damage／Cooldown | 自動普攻傷害與獨立倒數；不占技能槽。 |
| SkillSlots、RewardChoices | 六個技能槽與最多三個戰後選項；驗證器要求槽數為 6，選項為 1～3。 |
| OwnedSkillWeight、NewSkillWeight | 已有／未有技能的獨立抽選權重。 |
| SimulationStep、BattleTimeLimit | 固定模擬步長與戰鬥失敗時限；這是原型行為，正式逾時規則仍待核定。 |
| Skills、Relics、Effects | 技能、遺物與可組裝的效果；技能支援傷害／治療，遺物支援生命上限／傷害加成。 |
| Enemies、EnemyGroups | 敵人基本數值與隊伍；EnemyGroups[].EnemyIds 依順序設定 1～4 名，可重複同一敵人。 |
| Floors[].Rows | 每層的列設定；陣列長度即該層列數，各層可不同。目前驗證器要求共三層，各層最後一列只含 Boss。 |
| Rows[].NodePool／NodeCount | 每列候選節點及實際生成數量；可設定該列允許的節點種類。目前每列最多 3 個選項，受 MaximumMapChoices 與池大小限制。 |
| NodePool[].EnemyGroupPool | 戰鬥節點允許抽選的隊伍 ID 池；每次抽中一整組，完整採用其成員與排列。 |
| 節點 Gold／Healing／HealthCost | 戰鬥金幣、營火／事件恢復及事件生命代價。 |

## 原型實作行為

以下記錄現況，未另行核定的細節不直接擴大為正式多人規則。

- 1：新一輪開始時生成各層地圖。每列從該列 NodePool 不重複抽取 NodeCount 個候選項，保留池中的排列；若全部採用則直接沿用排列。處理目前列的一個節點後前進下一列，可前往下一列的任一節點，不回頭；目前尚無限制通路的獨立邊資料。
- 2：敵人隊伍 ID 從節點 EnemyGroupPool 等機率抽取；尚無每組獨立權重。同組重複敵人各有自己的生命與普攻倒數。
- 3：固定模擬步依序處理本機普攻、依技能槽順序的技能、存活敵人攻擊，最後檢查逾時。傷害以第一個存活敵人為目標；消滅所有敵人即結束該場，後續敵人不再攻擊。正式目標選擇、傷害公式及同時觸發規則仍見主玩法的待確認項目。
- 4：傷害加成遺物同時影響普攻與傷害技能。增加生命上限時也增加同量目前生命；治療不超過上限。技能沒有耗魔、防禦或其他狀態系統。
- 5：寶箱等機率抽取本輪未持有的遺物；全部持有後不再給新遺物。營火可休息或離開；事件可付出生命取得金幣或選擇恢復，代價致死時失敗且不給金幣。
- 6：戰後技能依設定權重不重複抽選；滿槽時僅抽已裝備且未滿級技能，無候選時跳過選技。
- 7：敵人目前只有普攻；玩家區只有本機一人。角色以文字／幾何標記佔位，未製作正式插圖或動畫。
- 8：UI 使用美術規範既有八色；目前大／中／小字級為 28／22／18。這是原型實作值，不自動定為所有頁面的永久字級。

## 相關文件

- [主選單](MainMenu.md)：遊戲啟動、單人入口與離開遊戲行為。
- [主玩法](CoreGameplay.md)：整體設計與尚未確認的長期規則。
- [挑戰介面](AdventureUI.md)：挑戰畫面的配置與驗收。
- [美術製作規範](ArtProductionRules.md)：風格、色票、字體層級與資產要求。
- [Unity 規則](UnityRules.md)：修改 Unity 程式、Prefab 或其他資產前閱讀。

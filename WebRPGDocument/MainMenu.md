# 主選單

- 設計狀態：已核定；多人遊玩入口開啟房間大廳；設定仍未開放。
- 實作狀態：已實作本文列出的主選單與單人啟動流程；多人遊玩已接入[房間大廳](MultiplayerLobby.md)，共同冒險已實作，流程見同份文件；設定功能未實作。
- 適用範圍：Unity 主場景的主選單、單人冒險入口與離開遊戲操作。
- 用途：記錄選項行為、Prefab 編輯方式、程式分工與驗證範圍。

## 規則

- 1：遊戲啟動時先顯示主選單，依序提供「單人遊玩」、「多人遊玩」、「設定（尚未開放）」與「離開遊戲」。
- 2：單人遊玩直接開始新一局並進入冒險地圖；成功後隱藏主選單，避免重複建立冒險。啟動失敗時保留選單、顯示錯誤提示並允許重試。
- 3：多人遊玩開啟[房間大廳](MultiplayerLobby.md)，由玩家輸入名稱並連線；返回主選單時關閉連線並釋放大廳。設定按鈕仍停用。
- 4：離開遊戲在桌面版呼叫 Application.Quit，在 Unity Editor 停止播放；WebGL 顯示「請關閉瀏覽器分頁以離開遊戲。」。
- 5：Main 不再建立或釋放聊天功能；既有 Chat 程式與 Prefab 保留；多人共同冒險由[房間大廳](MultiplayerLobby.md)啟動。
- 6：從挑戰選單確認返回主選單後，結束本輪、釋放冒險與挑戰選單，重新建立主選單 Presenter；可再次點擊單人遊玩開始新局。取消確認則保留本輪。挑戰內設定提示與中央選單行為見[挑戰介面](AdventureUI.md)，不改動本頁尚未開放的設定入口。

## Prefab 與程式分工

| 檔案 | 職責 |
| --- | --- |
| [MainMenuCanvas.prefab](../WebRPG/Assets/WebRPG/Prefabs/MainMenuCanvas.prefab) | 保存 Canvas、縮放、射線偵測、背景、標題、四個按鈕、提示文字及 View 引用；可在 Prefab Mode 調整。 |
| [MainMenuView.cs](../WebRPG/Assets/WebRPG/Code/MainMenu/MainMenuView.cs) | 綁定與解除按鈕事件、轉送操作、顯示提示及隱藏選單；不動態建立固定版面。 |
| [MainMenuModel.cs](../WebRPG/Assets/WebRPG/Code/MainMenu/MainMenuModel.cs) | 記錄是否已成功進入冒險。 |
| [MainMenuPresenter.cs](../WebRPG/Assets/WebRPG/Code/MainMenu/MainMenuPresenter.cs) | 處理單人與離開操作；成功啟動後更新 Model 並隱藏 View，釋放時解除事件訂閱。 |
| [Main.cs](../WebRPG/Assets/WebRPG/Code/Bootstrap/Main.cs) | 生成主選單 Prefab、組裝 MVP，並負責冒險啟動、逐幀推進、離開遊戲與生命週期管理。 |

- Prefab 的 Menu 節點包含 Title、SinglePlayer、Multiplayer、Settings、Quit 與 Message；按鈕文字位於各自的 Label 子節點。
- 固定版面與動態內容的製作要求以 [Unity 規則](UnityRules.md)為準；helper method 的拆分要求見 [Coding 規則](CodingRules.md)。
- 配色與字體呈現遵守[美術製作規範](ArtProductionRules.md)；主選單與挑戰畫面共用[單人可玩原型](AdventurePrototype.md)記錄的三層字級。

## 驗收情境

- 對應規則 1：啟動後顯示四個選項，尚未建立冒險或聊天畫面。
- 對應規則 2：點擊單人遊玩後直接進入 Map，主選單隱藏，冒險畫面只有一個；啟動失敗時仍可重試。
- 對應規則 3：多人入口可開啟大廳並返回主選單，再次開啟會建立新的大廳；設定按鈕不可操作。
- 對應規則 4：依執行平台確認關閉程式、停止播放或顯示分頁關閉提示。
- 對應規則 5：遊戲入口不建立聊天 Model、View 或 Presenter。
- Prefab 編輯：未進入 Play Mode 時即可看見並調整固定 UI，View 的按鈕與提示文字引用完整。

## 相關文件

- [單人可玩原型](AdventurePrototype.md)：冒險流程、設定與目前可玩範圍。
- [Unity 規則](UnityRules.md)：UI Prefab 與動態生成的製作要求。
- [Coding 規則](CodingRules.md)：MVP 分工、元件引用、生命週期與通用 helper method 拆分要求。
- [美術製作規範](ArtProductionRules.md)：UI 配色、字體與互動狀態。

## 待確認

- 設定功能的具體內容及開放時程。

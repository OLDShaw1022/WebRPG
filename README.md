# WebRPG

[English](README.en.md)

WebRPG 是我的個人作品集專案，以一款 2D RPG 原型展示我如何開發 Unity 遊戲前端、串接帳號 API 與即時多人後端，以及如何在開發過程中與 AI 協作。

## 功能展示

- **單人冒險**：探索多層地圖、選擇路線、參與戰鬥，並在途中取得技能與道具。
- **多人冒險**：玩家可建立或加入房間，最多 4 人準備後一同出發。伺服器處理房間與冒險狀態，並透過 WebSocket 同步給玩家。房間內也能即時聊天。
- **帳號系統**：支援 Email 註冊、登入與記住登入，帳號資料儲存在 PostgreSQL。

## 我如何與 AI 協作

我先定義功能範圍、行為規則與驗收條件，再讓 AI 依照專案文件協助拆解任務、實作與檢查變更。我負責設計取捨與驗收，依功能用 Unity 操作或伺服器測試確認行為。

以多人房間為例，[房間大廳規格](WebRPGDocument/MultiplayerLobby.md)定義了加入房間、切換準備狀態與斷線等行為，[WebSocket 整合測試](WebRPGServer.Tests/Lobby/LobbyWebSocketTests.cs)則涵蓋房間流程。[AGENTS.md](AGENTS.md)記錄了 AI 在此專案的文件閱讀、變更備份與驗證流程。

## 技術與專案結構

| 專案 | 技術與用途 |
| --- | --- |
| [WebRPG](WebRPG/) | Unity 6.3、C#、URP、NativeWebSocket。負責遊戲畫面、玩家操作與連線，支援 WebGL。 |
| [WebRPGServer](WebRPGServer/) | ASP.NET Core（.NET 9）、PostgreSQL。透過 HTTP API 提供帳號功能，透過 WebSocket 提供多人連線。 |
| [WebRPGShared](WebRPGShared/) | .NET Standard 2.1。共用前後端的通訊資料結構。 |
| [WebRPGServer.Tests](WebRPGServer.Tests/) | NUnit。包含單元、元件與 WebSocket 整合測試。 |

更多功能規格與驗收方式可見[專案文件目錄](WebRPGDocument/Document%20Directory.md)。

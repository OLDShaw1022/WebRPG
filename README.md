# WebRPG

[English](README.en.md) · [線上試玩](https://oldshaw-webrpg.com/)

WebRPG 是我的個人作品集專案，以一款 2D RPG 原型展示我如何開發 Unity 遊戲前端、串接帳號 API 與即時多人後端，以及如何在開發過程中與 AI 協作。

## 功能展示

- **單人冒險**：探索多層地圖、選擇路線、參與戰鬥，並在途中取得技能與道具。
- **多人冒險**：玩家可建立或加入房間，最多 4 人準備後一同出發。伺服器處理房間與冒險狀態，並透過 WebSocket 同步給玩家。房間內也能即時聊天。
- **帳號系統**：支援 Email 註冊、登入與記住登入，帳號資料儲存在 PostgreSQL。

## 我如何與 AI 協作

我先定義功能範圍、行為規則與驗收條件，再讓 AI 依照專案文件協助拆解任務、實作與檢查變更。我負責設計取捨，並依功能透過 Unity 操作或伺服器測試驗收。

以多人房間為例，[房間大廳規格](WebRPGDocument/MultiplayerLobby.md)定義了加入房間、切換準備狀態與斷線等行為，[WebSocket 整合測試](WebRPGServer.Tests/Lobby/LobbyWebSocketTests.cs)則涵蓋房間流程。[AGENTS.md](AGENTS.md)記錄了 AI 在此專案的文件閱讀、變更備份與驗證流程。

## 技術與專案結構

| 項目 | 技術與用途 |
| --- | --- |
| [遊戲前端](WebRPG/) | Unity 6.3、C#、URP、NativeWebSocket。以 Model–View–Presenter（MVP）落實 SOLID 的單一職責原則。以戰鬥為例，Model 處理戰鬥規則，View 呈現畫面，Presenter 協調玩家操作。WebGL 版部署在 Firebase Hosting。 |
| [遊戲後端](WebRPGServer/) | ASP.NET Core（.NET 9）、PostgreSQL。以 Minimal API 實作註冊與登入等 HTTP API，並透過 WebSocket 同步多人遊戲狀態，伺服器運行於 AWS EC2。 |
| [共用通訊](WebRPGShared/) | .NET Standard 2.1。共用前後端的通訊資料結構。 |
| [前端測試](WebRPG/Assets/WebRPG/Tests/Editor/) | Unity Test Framework（Edit Mode）。涵蓋冒險與戰鬥邏輯、UI 和多人連線流程。 |
| [後端測試](WebRPGServer.Tests/) | NUnit。包含單元、元件與 WebSocket 整合測試。 |
| [CI/CD（GitHub Actions）](.github/workflows/) | 手動觸發 WebGL 建置與 Firebase Hosting 部署，或執行後端測試並將伺服器部署至 AWS EC2。 |
| 網域 DNS | Cloudflare 管理遊戲網站和伺服器的 DNS。 |

更多功能規格與驗收方式可見[專案文件目錄](WebRPGDocument/Document%20Directory.md)。

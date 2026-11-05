# 帳號登入

- 設計狀態：已核定 Email／密碼登入、註冊設定可重複的使用者名稱、開啟遊戲先登入，以及沿用 PostgreSQL。
- 實作狀態：已加入程式並通過本機 PostgreSQL 整合測試；正式環境驗證及部署尚未進行。
- 適用範圍：Unity 啟動入口、帳號 HTTP API、房間大廳身分與 PostgreSQL。
- 用途：記錄第一版帳號流程、設定及驗證限制。

## 規則

- 1：啟動先顯示帳號表單；登入成功後才顯示主選單，單人與多人入口都需要登入。
- 2：註冊提供 Email、密碼與使用者名稱。Email 去除前後空白後不區分大小寫，必須唯一；名稱去除前後空白後為 1～24 字元，不含控制字元，可與其他帳號同名。每個帳號使用固定 UUID。
- 3：密碼為 8～128 字元，不裁切、不去除空白；以隨機鹽值與 PBKDF2-HMAC-SHA256（600,000 次）保存雜湊，不保存明文。參數參考 [OWASP](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)。
- 4：註冊成功後需登入。第一版不含 Email 驗證、忘記密碼、改名、重設密碼或自動登入。
- 5：登入憑證有效 12 小時；資料庫只保存憑證 SHA-256 雜湊。客戶端只在記憶體保存憑證，不寫入 PlayerPrefs 或檔案。到期返回登入頁並結束目前遊戲；主選單可登出，撤銷憑證成功後返回登入頁；網路失敗時保留狀態並可重試。
- 6：登入時選擇伺服器；大廳沿用同一伺服器與帳號名稱，不能更改。正式版使用 HTTPS/WSS，HTTP 僅供 loopback 本機測試。
- 7：帳號 API 每個來源 IP 每分鐘最多 10 次，無等待佇列；要求上限 4096 bytes，回應禁止快取。未設定資料庫時回傳 503，不使用記憶體帳號替代。登入失敗不區分未知帳號與錯誤密碼。
- 8：大廳 Hello 的 `AccessToken` 欄位傳入憑證，不放進 URL；伺服器查詢固定帳號 ID 與名稱，不採信客戶端名稱。每次操作均重新確認憑證有效，撤銷或到期後拒絕操作並關閉連線；同帳號同時僅允許一條大廳連線。

## API 與設定

| 方法與路徑 | 輸入 | 成功結果 |
| --- | --- | --- |
| `POST /api/accounts/register` | `email`、`password`、`username` | 200，註冊提示 |
| `POST /api/accounts/login` | `email`、`password` | 200，`accessToken`、`accountId`、`username`、`expiresAt` |
| `POST /api/accounts/logout` | `Authorization: Bearer <token>` | 204，撤銷憑證 |

- 沿用 `ConnectionStrings:GameDatabase`，可由 `ConnectionStrings__GameDatabase` 提供；不要把密碼、憑證或連線字串寫入 Git、文件或聊天。
- 啟動時建立 `game_accounts`、`game_account_sessions` 與索引，不改既有聊天表；資料庫角色需要建表權限。過期工作階段於後續成功登入時清理。
- `Accounts:AllowedOrigins` 設定允許的 WebGL 來源，預設為既有 Firebase 的 `web.app` 與 `firebaseapp.com` 網域。客戶端由 LobbyClient.json 的 WebSocket 位址推導帳號 API 位址。
- 預設僅信任 ASP.NET Core 的 loopback 反向代理；其他拓撲需明確設定可信代理。反向代理需轉送 `/api/accounts/`、`X-Forwarded-For` 與 `X-Forwarded-Proto`，並保留既有 WebSocket 路徑。

## UI 與驗證

- `AccountCanvas.prefab` 保存固定帳號表單，可於 Editor 調整；密碼欄位使用遮罩。`Main.unity` 綁定此 Prefab，`MainMenuCanvas.prefab` 新增登出按鈕。
- `AccountModel` 管理 HTTP 與憑證；`AccountView` 顯示表單並轉送操作；`AccountPresenter` 控制忙碌與錯誤狀態；`Main` 組裝並管理登入與主選單。
- 已執行伺服器建置、欄位／雜湊、真實本機 WebSocket 認證／撤銷、資料庫未設定的 HTTP API、Unity Prefab 與 HTTP 登入／登出測試，以及既有回歸測試。Unity HTTP 測試使用本機模擬回應，不能代替 PostgreSQL 驗證。
- 已建立免安裝 PostgreSQL 17.11 本機測試環境，驗證實際註冊／登入／登出、Email 大小寫唯一約束、同名帳號不同 ID、密碼與憑證僅存雜湊、憑證到期／撤銷，以及 PostgreSQL 與正式 Program 重啟後保留帳號 ID。實測找出的帶參數多 SQL 命令問題已修正為分開執行清理與新增。
- 舊大廳測試使用隔離的本機 Kestrel 主機，可依情境注入認證；正式 Program 始終啟用帳號驗證。
- 既有遠端部署測試仍使用匿名連線，無法通過新版認證。CI 認證更新因自動核准審查拒絕而未寫入，需另外授權處理測試帳號與遠端登入。尚未執行遠端測試、提交、推送或部署。
- 新舊大廳認證不相容，用戶端與伺服器需配套發布；正式資料庫、反向代理與 WebGL 部署驗證完成前，尚未完成上線驗收。
- 建置略過共用專案，沒有覆寫 `Assets/Plugins/WebRPGShared.dll` 或 PDB；既有 DLL 與字型修改保留。

## 本機資料庫測試

- 此工作站使用 `%LOCALAPPDATA%/WebRPGTest/PostgreSQL-17.11/pgsql/bin` 的免安裝執行檔，來源為 PostgreSQL 官網連結的 [EDB 壓縮套件](https://www.enterprisedb.com/download-postgresql-binaries)。不安裝 Windows 服務、不更改系統 PATH。
- `AccountPostgresTests` 透過 `LocalAccountDatabase` 為每次測試建立 `%LOCALAPPDATA%/WebRPGTest/Runs/<隨機 ID>/data`，自動選埠，只監聽 `127.0.0.1`。測試專用 cluster 使用 trust 驗證及虛構帳號資料，不得拿來保存正式使用者資料；不讀取或使用正式連線設定。
- 測試結束自動停止 PostgreSQL，資料檔保留供排查；沒有常駐資料庫或系統服務。未提供執行檔路徑時僅略過這兩項本機資料庫測試；遠端測試仍需明確指定。
- 本工作目錄已有共用專案建置產物，以下指令略過共用專案建置，以免覆寫既有 DLL。其他乾淨工作目錄需先按 AGENTS.md 備份共用產物並正常建置。

```powershell
$env:WEBRPG_TEST_POSTGRES_BIN = Join-Path $env:LOCALAPPDATA 'WebRPGTest/PostgreSQL-17.11/pgsql/bin'
dotnet build WebRPGServer/WebRPGServer.csproj --no-restore -p:BuildProjectReferences=false
dotnet test WebRPGServer.Tests/WebRPGServer.Tests.csproj --no-restore -p:BuildProjectReferences=false
```

- 只跑資料庫流程時，在測試指令加上 `--filter FullyQualifiedName~AccountPostgresTests`。

## 相關文件

- [主選單](MainMenu.md)：登入後的遊戲入口。
- [房間大廳](MultiplayerLobby.md)：房間操作。
- [Coding 規則](CodingRules.md)、[Unity 規則](UnityRules.md)：程式與 Prefab 慣例。

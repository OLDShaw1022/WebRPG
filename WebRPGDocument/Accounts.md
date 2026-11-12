# 帳號登入

- 設計狀態：已核定 Email／密碼登入、註冊設定可重複的使用者名稱、開啟遊戲先登入，沿用 PostgreSQL，以及支援 WebGL 和桌面平台的記住登入狀態。
- 實作狀態：已實作帳密登入，以及 WebGL 和桌面平台的長效 Cookie 恢復登入。
- 適用範圍：Unity 啟動入口、帳號 HTTP API、房間大廳身分與 PostgreSQL。
- 用途：記錄第一版帳號流程、設定及驗證限制。

## 規則

- 1：啟動先嘗試使用預設伺服器的 Cookie 恢復登入，等待期間停用帳號表單。恢復成功顯示主選單；沒有 Cookie 或憑證失效時顯示登入表單；網路失敗時提供重新整理重試或帳密登入提示。單人與多人入口都需要登入。
- 2：註冊提供 Email、密碼與使用者名稱。Email 去除前後空白後不區分大小寫，必須唯一；名稱去除前後空白後為 1～24 字元，不含控制字元，可與其他帳號同名。每個帳號使用固定 UUID。
- 3：密碼為 8～128 字元，不裁切、不去除空白；以隨機鹽值與 PBKDF2-HMAC-SHA256（600,000 次）保存雜湊，不保存明文。參數參考 [OWASP](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)。
- 4：註冊成功後需登入。第一版不含 Email 驗證、忘記密碼、改名、重設密碼。
- 5：Access Token 有效 12 小時，Unity 只在記憶體保存，不寫入 PlayerPrefs 或檔案。登入表單提供預設不勾選的「記住我（30 天）」；勾選後由伺服器另發隨機長效憑證，僅以 HttpOnly、Secure、SameSite=Strict、host-only、Path=/api/accounts 的 Cookie 保存。資料庫只存兩種憑證的 SHA-256 雜湊，不保存明文憑證或密碼。
- 6：登入時選擇伺服器；大廳沿用同一伺服器與帳號名稱，不能更改。正式版使用 HTTPS/WSS，HTTP 僅供 loopback 本機測試。
- 7：帳號 API 每個來源 IP 每分鐘最多 10 次，無等待佇列；要求上限 4096 bytes，回應禁止快取。未設定資料庫時回傳 503，不使用記憶體帳號替代。登入失敗不區分未知帳號與錯誤密碼。
- 8：大廳 Hello 的 `AccessToken` 欄位傳入憑證，不放進 URL；伺服器查詢固定帳號 ID 與名稱，不採信客戶端名稱。每次操作均重新確認憑證有效，撤銷或到期後拒絕操作並關閉連線；同帳號同時僅允許一條大廳連線。

- 9：長效憑證從帳密登入起算固定 30 天，恢復登入時在交易內輪替，不延長期限；同一長效憑證只能換發一次。輪替後前一個 Access Token 失效，新 Access Token 到期不得晚於長效憑證。重新登入會撤銷同一 Cookie 的舊記住登入狀態，未勾選時也會清除 Cookie。
- 10：主選單登出會撤銷目前長效憑證及關聯 Access Token，再清除 Cookie。僅有 Bearer 的登出同樣撤銷所屬長效工作階段；網路失敗保留本機狀態以便重試。開啟或重新整理遊戲時會檢查恢復登入憑證。使用「記住我」的登入工作階段會在 Access Token 到期前 5 分鐘嘗試背景換發。網路暫時失敗時每 15 秒重試，直到到期。未勾選「記住我」的登入沒有背景換發。Access Token 到期時若仍未成功換發，客戶端會取消尚未完成的換發、關閉大廳連線、結束目前冒險、清除本機登入狀態並顯示登入表單。清除網站資料、封鎖 Cookie 或私人模式結束後可能需要重新登入。
- 11：帳號 API 拒絕未允許的 Origin 與 Sec-Fetch-Site: cross-site 要求；恢復登入與攜帶 Cookie 的登出另須提供 X-WebRPG-Request: 1，防止跨站表單偽造。WebGL 的 Cookie 由瀏覽器管理，遊戲程式不讀取或自行設定 Cookie header。

## API 與設定

| 方法與路徑 | 輸入 | 成功結果 |
| --- | --- | --- |
| `POST /api/accounts/register` | `email`、`password`、`username` | 200，註冊提示 |
| `POST /api/accounts/login` | `email`、`password`、`rememberMe`（預設 false） | 200，`accessToken`、`accountId`、`username`、`expiresAt`；勾選時另設定 Cookie |
| `POST /api/accounts/refresh` | Cookie、`X-WebRPG-Request: 1`。遊玩中換發另傳原 Access Token 作為 Bearer，須與 Cookie 屬於同一工作階段 | 200，同登入結果並輪替 Cookie；憑證無效、到期或不相符為 401 |
| `POST /api/accounts/logout` | `Authorization: Bearer <token>` 或 Cookie；帶 Cookie 時須有 `X-WebRPG-Request: 1` | 204，撤銷登入及長效憑證並清除 Cookie |

- 沿用 `ConnectionStrings:GameDatabase`，可由 `ConnectionStrings__GameDatabase` 提供；不要把密碼、憑證或連線字串寫入 Git、文件或聊天。
- 啟動時建立 `game_accounts`、`game_account_sessions`、`game_account_remembered_sessions` 與索引，為既有工作階段表補上可空的長效工作階段關聯欄位，不改既有聊天表；資料庫角色需要建表權限。過期工作階段於後續成功登入時清理。
- `Accounts:AllowedOrigins` 設定允許的 WebGL 來源，預設僅 `https://oldshaw-webrpg.com`。客戶端由 LobbyClient.json 的 WebSocket 位址推導帳號 API 位址，正式值為 `wss://ws.oldshaw-webrpg.com/ws?channel=lobby`，因此 API 為 `https://ws.oldshaw-webrpg.com/api/accounts/`；與遊戲頁面同站但不同來源。
- 預設僅信任 ASP.NET Core 的 loopback 反向代理；其他拓撲需明確設定可信代理。反向代理需轉送 `/api/accounts/`、`X-Forwarded-For` 與 `X-Forwarded-Proto`，並保留既有 WebSocket 路徑。

## 正式網域與反向代理

- 正式遊戲入口為 https://oldshaw-webrpg.com/ ，由 Firebase Hosting 的自訂網域提供靜態 WebGL；帳號 API 位於 https://ws.oldshaw-webrpg.com/api/accounts/ ，WebSocket 位於 wss://ws.oldshaw-webrpg.com/ws 。兩個 HTTPS 子網域同站但不同來源，不需要把 API 移到遊戲主網域。
- 在負責 ws.oldshaw-webrpg.com TLS 的 gateway 設定以下路由；範例假設 ASP.NET Core 與 Nginx 在同一主機且監聽 8080。Firebase 仍提供遊戲頁面，無須新增同源 API rewrite。若代理跨主機，須另外配置可信代理與實際上游。

```nginx
location /api/accounts/ {
    proxy_pass http://127.0.0.1:8080;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}

location = /ws {
    proxy_pass http://127.0.0.1:8080;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_read_timeout 3600s;
}
```

- API 代理保留 Set-Cookie、Origin 與 CORS 回應標頭，不把 /api/accounts/ 回應快取或改寫成 index.html。正式請使用 oldshaw-webrpg.com 的遊戲頁面；Firebase 預設 web.app／firebaseapp.com 網址屬於另一個站點，不在預設來源清單內，也不承諾其跨站 Cookie 登入。
- 上線驗收須實際確認：WebGL 勾選記住登入後重新整理、關閉再開啟瀏覽器可登入；未勾選不會留下長效憑證；登出後重新整理仍停留登入頁；Cookie 為 HttpOnly／Secure，JavaScript 無法讀取；API 與 WSS 均走 ws.oldshaw-webrpg.com，遊戲頁面走 oldshaw-webrpg.com。

## 桌面版記住我 Cookie

- Windows、macOS 和 Linux 的 Unity Editor 與獨立版使用作業系統的憑證儲存。Windows 使用認證管理員，macOS 使用 Keychain，Linux 透過 `secret-tool` 使用 Secret Service。Linux 需要可用的桌面金鑰圈與 `secret-tool`，缺少其中一項時仍可手動登入。
- 同一作業系統使用者的 Editor 與獨立版依伺服器的協定、主機與連接埠共用 Cookie。啟動時讀取 Cookie 並送出恢復登入請求。伺服器輪替 Cookie 後覆寫儲存值，遊戲內登出成功後清除共用 Cookie。

## WebGL 跨來源憑證

- AccountBrowserRequestHelper 在 UnityWebRequest 送出前登記該次完整帳號 API URL；WebRPGAccountCredentials.jslib 為 Unity 6.3 的 Module.fetchWithProgress 與 Module.cachedFetch 設定有範圍限制的包裝，只讓已登記 URL 的 POST 要求使用 credentials: include。只接受 register、login、refresh、logout 路徑，不更動其他主機、資產下載、GET 或未登記 URL 的憑證模式；保留取消訊號、逾時與進度回呼。Editor 與桌面不呼叫此 WebGL 橋接。
- AccountCorsHelper 以 Accounts:AllowedOrigins 的明確來源清單啟用 AllowCredentials，預設僅 https://oldshaw-webrpg.com ，不使用萬用來源。預檢允許 POST、Content-Type、Authorization 與 X-WebRPG-Request；Origin 與 Sec-Fetch-Site 防護維持原規則。
- Cookie 仍為 HttpOnly、Secure、SameSite=Strict、host-only、Path=/api/accounts，僅屬於 API 子網域。沒有改成 SameSite=None，沒有設定涵蓋所有子網域的 Domain，也沒有讓遊戲 JavaScript 讀取長效憑證。
- 更新伺服器與 WebGL 客戶端須配套發布；只更新 CORS 或只更新瀏覽器端，都無法完成跨來源記住登入。

## UI

- `AccountCanvas.prefab` 保存帳號表單及綁定於 `AccountView` 的 RememberMe Toggle。密碼欄位使用遮罩。`Main.unity` 綁定帳號 Prefab，`MainMenuCanvas.prefab` 提供登出按鈕。
- `AccountModel` 管理 HTTP 與憑證，`AccountView` 顯示表單並轉送操作，`AccountPresenter` 控制忙碌與錯誤狀態，`Main` 組裝並管理登入與主選單。

## 測試與驗收

- 在 Unity Test Runner 的 EditMode 執行 `AccountPrefabTests`、`AccountClientTests` 與 `AccountRememberedClientTests`。帳號 HTTP 測試使用本機模擬回應，PostgreSQL 整合測試見下節。
- 在各桌面平台的 Editor 與獨立版勾選「記住我」登入。停止並再次進入 Play Mode，接著關閉整個 Editor 或獨立版後重新啟動。確認連續兩次自動登入都成功，且同一使用者與伺服器的 Editor 和獨立版可共用登入狀態。從主選單登出後重新啟動，確認顯示登入表單。
- 執行 `node --test WebRPGPage/ci/account-credentials.test.mjs` 檢查 WebGL 帳號請求的憑證模式。瀏覽器整合測試使用 `AccountCrossOriginBrowserTests`，需和 `RememberedBrowserTests` 一樣設定 `WEBRPG_TEST_BROWSER_BIN`。測試使用隔離的瀏覽器設定檔及本機 HTTPS；正式 WebGL 遊戲依部署章節驗收。


## 本機資料庫測試

- 可使用 `%LOCALAPPDATA%/WebRPGTest/PostgreSQL-17.11/pgsql/bin` 的免安裝執行檔，來源為 PostgreSQL 官網連結的 [EDB 壓縮套件](https://www.enterprisedb.com/download-postgresql-binaries)。不安裝 Windows 服務、不更改系統 PATH。
- `RememberedBrowserTests` 另需設定 WEBRPG_TEST_BROWSER_BIN 指向 Edge／Chromium 執行檔；使用臨時獨立 user-data-dir 與本機 API，驗證 Cookie 跨瀏覽器程序保存、HttpOnly 與登出後失效，不讀取使用者原有設定檔。這項測試不等於正式 WebGL 遊戲端到端驗收。
- `AccountPostgresTests` 透過 `LocalAccountDatabase` 為每次測試建立 `%LOCALAPPDATA%/WebRPGTest/Runs/<隨機 ID>/data`，自動選埠，只監聽 `127.0.0.1`。測試專用 cluster 使用 trust 驗證及虛構帳號資料，不得拿來保存正式使用者資料；不讀取或使用正式連線設定。
- 測試結束自動停止 PostgreSQL，資料檔保留供排查；沒有常駐資料庫或系統服務。未提供執行檔路徑時略過需要該工具的本機整合測試；遠端測試仍需明確指定。
- 若工作目錄已有可用的共用專案建置產物，可用以下指令略過共用專案建置。乾淨工作目錄需依 AGENTS.md 確認共用產物後先正常建置。

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

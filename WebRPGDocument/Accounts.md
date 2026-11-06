# 帳號登入

- 設計狀態：已核定 Email／密碼登入、註冊設定可重複的使用者名稱、開啟遊戲先登入，沿用 PostgreSQL，以及支援 WebGL 的記住登入狀態。
- 實作狀態：已加入帳密登入與長效 Cookie 恢復登入，通過本機 PostgreSQL、Unity Editor 帳號測試、獨立 Edge 跨程序 Cookie 驗證與 WebGL 建置；正式環境驗證及部署尚未進行。
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
- 10：主選單登出會撤銷目前長效憑證及關聯 Access Token，再清除 Cookie。僅有 Bearer 的登出同樣撤銷所屬長效工作階段；網路失敗保留本機狀態以便重試。Access Token 到期仍返回登入頁並結束目前遊戲，不在遊玩途中自動換發；下次開啟或重新整理時才恢復登入。清除網站資料、封鎖 Cookie 或私人模式結束後可能需要重新登入。
- 11：帳號 API 拒絕未允許的 Origin 與 Sec-Fetch-Site: cross-site 要求；恢復登入與攜帶 Cookie 的登出另須提供 X-WebRPG-Request: 1，防止跨站表單偽造。WebGL 的 Cookie 由瀏覽器管理，遊戲程式不讀取或自行設定 Cookie header。

## API 與設定

| 方法與路徑 | 輸入 | 成功結果 |
| --- | --- | --- |
| `POST /api/accounts/register` | `email`、`password`、`username` | 200，註冊提示 |
| `POST /api/accounts/login` | `email`、`password`、`rememberMe`（預設 false） | 200，`accessToken`、`accountId`、`username`、`expiresAt`；勾選時另設定 Cookie |
| `POST /api/accounts/refresh` | Cookie、`X-WebRPG-Request: 1` | 200，同登入結果並輪替 Cookie；未登入／過期為 401 |
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

- API 代理保留 Set-Cookie、Origin 與 CORS 回應標頭，不把 /api/accounts/ 回應快取或改寫成 index.html。正式請使用 oldshaw-webrpg.com 的遊戲頁面；Firebase 預設 web.app／firebaseapp.com 網址屬於另一個站點，不在預設來源清單內，也不承諾其跨站 Cookie 登入。正式 gateway、憑證、DNS 與 hosting 不由本次程式修正變更。
- 上線驗收須實際確認：WebGL 勾選記住登入後重新整理、關閉再開啟瀏覽器可登入；未勾選不會留下長效憑證；登出後重新整理仍停留登入頁；Cookie 為 HttpOnly／Secure，JavaScript 無法讀取；API 與 WSS 均走 ws.oldshaw-webrpg.com，遊戲頁面走 oldshaw-webrpg.com。

## 桌面版記住我 Cookie

- Windows、macOS 和 Linux 的 Unity Editor 與獨立版使用作業系統的憑證儲存。Windows 使用認證管理員，macOS 使用 Keychain，Linux 透過 `secret-tool` 使用 Secret Service。Linux 需要可用的桌面金鑰圈與 `secret-tool`，缺少其中一項時仍可手動登入。
- 同一作業系統使用者的 Editor 與獨立版依伺服器的協定、主機與連接埠共用 Cookie。啟動時讀取 Cookie 並送出恢復登入請求。伺服器輪替 Cookie 後覆寫儲存值，遊戲內登出成功後清除共用 Cookie。
- 三個桌面平台的 Editor 與獨立版 C# 編譯分支均已通過檢查。實機打包與重啟登入仍待驗證。


## WebGL 跨來源憑證

- AccountBrowserRequestHelper 在 UnityWebRequest 送出前登記該次完整帳號 API URL；WebRPGAccountCredentials.jslib 為 Unity 6.3 的 Module.fetchWithProgress 與 Module.cachedFetch 設定有範圍限制的包裝，只讓已登記 URL 的 POST 要求使用 credentials: include。只接受 register、login、refresh、logout 路徑，不更動其他主機、資產下載、GET 或未登記 URL 的憑證模式；保留取消訊號、逾時與進度回呼。Editor 與桌面不呼叫此 WebGL 橋接。
- AccountCorsHelper 以 Accounts:AllowedOrigins 的明確來源清單啟用 AllowCredentials，預設僅 https://oldshaw-webrpg.com ，不使用萬用來源。預檢允許 POST、Content-Type、Authorization 與 X-WebRPG-Request；Origin 與 Sec-Fetch-Site 防護維持原規則。
- Cookie 仍為 HttpOnly、Secure、SameSite=Strict、host-only、Path=/api/accounts，僅屬於 API 子網域。沒有改成 SameSite=None，沒有設定涵蓋所有子網域的 Domain，也沒有讓遊戲 JavaScript 讀取長效憑證。
- 更新伺服器與 WebGL 客戶端須配套發布；只更新 CORS 或只更新瀏覽器端，都無法完成跨來源記住登入。

## UI 與驗證

- `AccountCanvas.prefab` 保存固定帳號表單與綁定於 AccountView 的 RememberMe Toggle，可於 Editor 調整；密碼欄位使用遮罩。`Main.unity` 綁定此 Prefab，`MainMenuCanvas.prefab` 新增登出按鈕。
- `AccountModel` 管理 HTTP 與憑證；`AccountView` 顯示表單並轉送操作；`AccountPresenter` 控制忙碌與錯誤狀態；`Main` 組裝並管理登入與主選單。
- 已執行伺服器建置、欄位／雜湊、真實本機 WebSocket 認證／撤銷、資料庫未設定的 HTTP API、Unity Prefab 與 HTTP 登入／登出測試，以及既有回歸測試。Unity HTTP 測試使用本機模擬回應，不能代替 PostgreSQL 驗證。
- 已建立免安裝 PostgreSQL 17.11 本機測試環境，驗證實際註冊／登入／登出、Email 大小寫唯一約束、同名帳號不同 ID、密碼與憑證僅存雜湊、憑證到期／撤銷，以及 PostgreSQL 與正式 Program 重啟後保留帳號 ID。實測找出的帶參數多 SQL 命令問題已修正為分開執行清理與新增。
- 舊大廳測試使用隔離的本機 Kestrel 主機，可依情境注入認證；正式 Program 始終啟用帳號驗證。
- 既有遠端部署測試仍使用匿名連線，無法通過新版認證。CI 認證更新因自動核准審查拒絕而未寫入，需另外授權處理測試帳號與遠端登入。尚未執行遠端測試、提交、推送或部署。
- 新舊大廳認證不相容，用戶端與伺服器需配套發布；正式資料庫、反向代理與 WebGL 部署驗證完成前，尚未完成上線驗收。
- .NET 建置略過共用專案，沒有覆寫 `Assets/Plugins/WebRPGShared.dll` 或 PDB；共用 DLL 的 SHA-256 保持 09DBE9DF7F6A5401207AE3DE3F907693EE7E815948E6DCB5FCC0AA59C51874B9。Unity 預覽／建置觸發原本已有修改的 NotoSansTC-Regular SDF.asset 自動更新；未自行還原該檔案，其後續狀態另有備份。

## 本次記住登入驗證

- 完整本機伺服器測試 28 項通過，包含長效憑證雜湊、到期、單次換發、同時換發、未勾選撤銷舊狀態、登出、資料庫重啟及 Edge 跨程序 Cookie 恢復；未執行明確標記的遠端測試。
- Unity Editor 帳號測試 13 項通過，包含表單綁定、記住登入送出、忙碌停用、啟動恢復、401 與服務失敗；使用 Unity 預覽場景確認登入表單與新選項排版，不更動正式場景。
- Unity 6000.3.11f1 WebGL 建置成功，零錯誤；唯一警告為沒有 RuntimePipelineConfig，故開發用 Pipeline 在 Player 中停用，與帳號功能無關。輸出位於本機暫存目錄 WebRPGRememberLogin-WebGL-20261105-verify，未上傳或部署。
- 已清除本次建置落盤的暫時 preloadedAssets 項目，ProjectSettings.asset 與原始基準一致；Editor 作用中平台切回原來的 StandaloneWindows64。沒有修改 WebSocket 封包或共用 DLL/PDB。
- 本機瀏覽器測試使用相同帳號端點與獨立測試頁；正式 WebGL 遊戲在 oldshaw-webrpg.com 的整合、TLS／代理與多瀏覽器驗收仍須部署後執行。

## 跨來源修正驗證

- 完整本機伺服器測試 32 項通過，包含正式來源的 credentialed CORS 預檢，以及不允許的網站／Firebase 預設來源不會取得憑證式 CORS 授權；未執行遠端測試。
- Node 內建測試 4 項通過，驗證 Unity 的兩條 fetch 路徑、原有取消／進度參數保留、重複登記不疊加包裝，以及非帳號要求不帶入額外 Cookie。執行命令為 node --test WebRPGPage/ci/account-credentials.test.mjs。
- AccountCrossOriginBrowserTests 使用 webrpg.test 與 api.webrpg.test 的本機 HTTPS、獨立 Edge 設定檔與實際 jslib 原始碼，驗證登入保存 Cookie、跨瀏覽器程序重啟恢復及登出撤銷。省略 credentials 的對照要求為 401，透過插件設定 include 後成功恢復；SameSite=Strict 維持不變。測試 DNS 僅在該瀏覽器程序映射至 loopback，不更動系統 hosts、正式 DNS 或使用者設定檔。
- 本機 HTTPS 憑證僅供測試，未安裝憑證或信任根、未寫入 Git；Windows Schannel 需要暫時使用者金鑰容器，憑證物件釋放時清理。忽略憑證信任檢查的旗標僅用於這個隔離瀏覽器程序。
- Unity Editor 帳號測試 13 項通過；Unity 6000.3.11f1 WebGL 建置成功、零錯誤，並核對產生的 framework.js 含帳號橋接與執行期憑證登記邏輯。建置有未連結 Unity Services project ID、未設定 Pipeline runtime 兩項非帳號警告，未因此改動服務設定。
- WebGL 輸出位於本機暫存目錄 WebRPGCrossOrigin-WebGL-20261105-verify。DLL/PDB 未覆寫；字型原先已有修改，匯入／建置又觸發自動更新，匯入前狀態已備份。建置暫時的預載資產項目已清理；正式伺服器與 Firebase WebGL 尚未由本次修正部署，正式網域整合仍須配套發布後驗收。

## 跨來源修正的還原基準

- 比較基準為 origin/main 的 82bb92d8351b62643330b70312582c0d97bc6b41；本次首次修改的程式、測試及文件均與此版本一致，既有 DLL 不在修改範圍。
- 驗證前快照 refs/backups/20261105-101902475-cross-origin-login 涵蓋 Program.cs、AccountCorsHelper.cs、RememberedLoginTests.cs、RememberedBrowserHelper.cs、AccountCrossOriginBrowserTests.cs、AccountModel.cs、AccountBrowserRequestHelper.cs、WebRPGAccountCredentials.jslib、WebRPGPage/ci/account-credentials.test.mjs，以及 Unity 匯入前已存在的 NotoSansTC-Regular SDF.asset 修改。原暫存區保持不變，備份不推送遠端。
- 清理前另有 refs/backups/20261105-102817712-cross-origin-build-cleanup，涵蓋當時的 ProjectSettings.asset、Accounts.md 與 MultiplayerLobby.md；保護建置暫時狀態及驗證紀錄編輯前的文件。
- 可用 git show <reference>:<path> 查看指定內容；僅在使用者要求時，以 git restore --source=<reference> -- <指定路徑> 還原指定檔案。要取得本次修正前的原始程式，使用上述 upstream 基準；驗證快照保留的是中途實作及字型狀態。

## 前次修改的還原基準

- 額外驗證快照：refs/backups/20261105-091336725-remember-login-import，涵蓋 Unity 自動更新後的 NotoSansTC-Regular SDF.asset、建置中的 ProjectSettings.asset，以及 Accounts.md、MultiplayerLobby.md、AccountHttpTestServer.cs、AccountRememberedClientTests.cs、RememberedLoginTests.cs、RememberedBrowserTests.cs、RememberedBrowserHelper.cs。字型在任務前已有修改，本快照是匯入後狀態，不是任務開始前的原始字型版本；未改動原暫存區。
- 原始比較基準：origin/main，6864971b83ad034d520c9e6d90aca87b2fb6727d。首次寫入前本次涉及的既有檔案皆無相對 upstream 的未推送變更，故原始內容以此版本為還原基準。
- 驗證修正前的實作快照：refs/backups/20261105-085900443-remember-login-validation，已於 109a68e 成功推送並核對後清除本機 reference。涵蓋帳號伺服器的 AccountContracts、AccountEndpoints、PostgresAccountStore、Program、新增的 Cookie／Token helper 與 RememberedSessions、RememberedLoginTests，以及 Unity AccountModel／Presenter／View、Main、LobbyClient.json、AccountCanvas.prefab、新增的 AccountRememberedClientTests／AccountClientConfigurationHelper 與其 meta。未包含既有 DLL 或字型修改，也未改動原暫存區。
- 可用 git show <reference>:<path> 查看指定內容；使用者要求還原時才執行 git restore --source=<reference> -- <指定路徑>，僅處理指定檔案。上述 backup 是驗證修正前的實作快照，若要取得修改前內容須使用原始基準。後續首次修改的 AccountHttpTestServer.cs 與文件本身原先亦與該基準一致。

## 本機資料庫測試

- 此工作站使用 `%LOCALAPPDATA%/WebRPGTest/PostgreSQL-17.11/pgsql/bin` 的免安裝執行檔，來源為 PostgreSQL 官網連結的 [EDB 壓縮套件](https://www.enterprisedb.com/download-postgresql-binaries)。不安裝 Windows 服務、不更改系統 PATH。
- `RememberedBrowserTests` 另需設定 WEBRPG_TEST_BROWSER_BIN 指向 Edge／Chromium 執行檔；使用臨時獨立 user-data-dir 與本機 API，驗證 Cookie 跨瀏覽器程序保存、HttpOnly 與登出後失效，不讀取使用者原有設定檔。這項測試不等於正式 WebGL 遊戲端到端驗收。
- `AccountPostgresTests` 透過 `LocalAccountDatabase` 為每次測試建立 `%LOCALAPPDATA%/WebRPGTest/Runs/<隨機 ID>/data`，自動選埠，只監聽 `127.0.0.1`。測試專用 cluster 使用 trust 驗證及虛構帳號資料，不得拿來保存正式使用者資料；不讀取或使用正式連線設定。
- 測試結束自動停止 PostgreSQL，資料檔保留供排查；沒有常駐資料庫或系統服務。未提供執行檔路徑時略過需要該工具的本機整合測試；遠端測試仍需明確指定。
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

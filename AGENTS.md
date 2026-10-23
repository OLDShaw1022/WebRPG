# AGENTS.md

## 專案概況

- `WebRPG/`：Unity 6.3（6000.3.11f1）2D 遊戲專案，使用 URP、Input System 和 NativeWebSocket。
- `WebRPGServer/`：以 .NET 9 建置的 ASP.NET Core WebSocket 伺服器，WebSocket 路徑是 `/ws`。
- `WebRPGShared/`：目標為 .NET Standard 2.1 的共用 C# 程式庫；建置後會複製 DLL/PDB 到 `WebRPG/Assets/Plugins/`。
- `WebRPGDocument/`：存放遊戲設計、技術規範與文件範本，由 `Document Directory.md` 提供文件索引。
- `WebRPG.sln`：根目錄的 .NET solution。

## 修改規則

- 本專案任何檔案修改前，可先進行唯讀檢查與分析；每批修改先提供完整 diff，待使用者同意該版本後才寫入，不另行要求前置許可或重複確認。
- 尚未核准的 diff 若需修訂，預設只提供相對上一版的完整增量差異並等待同意；使用者要求或版本不明時，再提供整份 diff。
- 已核准的修改可直接執行並進行相關驗證；若需追加未核准的修改，先提供追加 diff 並等待同意。
- 需求不明且會影響功能、修改範圍、相容性或資料安全時，先向使用者確認；其餘可逆、低影響的實作細節依既有慣例判斷，必要時說明假設。
- 不要覆寫、提交或輸出憑證、私密金鑰或其他機密資料。不要把秘密值寫進程式碼或文件。
- 新增規則時，寫清楚它適用的路徑、要做的事，以及必要時的例外情況。

## 建置與驗證

- 若修改 `WebRPGServer` 或 `WebRPGShared`，可從根目錄執行 `dotnet build WebRPG.sln`。
- `WebRPGShared` 建置會把輸出複製到 Unity 的 `Assets/Plugins/`；檢查版本控制差異，確認產生的檔案是否應納入這次修改。
- Unity 場景或資產的行為需要在 Unity Editor 中確認。若無法執行 Editor，請在回覆中說明尚未確認的部分。
- 只執行與本次修改相關且可用的驗證；不要宣稱未執行的檢查已通過。

## AI 文件閱讀流程

- 首次處理本專案任務時，先閱讀 [文件目錄](WebRPGDocument/Document%20Directory.md)，依任務選讀相關文件。
- 以下閱讀要求適用於本專案文件：同一聊天中已讀、仍保留於上下文且內容未變更的文件不必重讀；已載入上下文的 AGENTS.md 同樣適用。
- 僅在有外部修改跡象、恢復工作時無法確認文件版本，或上下文不足時，檢查相關文件是否變更，不必每輪例行檢查。優先比對與上次讀取版本的差異；Git 狀態相同不代表內容與上次讀取時相同，Git 差異也須確認比較基準。無法確認時，補讀必要段落，不要為了確認變更而一律重讀全文。
- 文件有變更時優先讀取差異；任務擴大時補讀相關文件的必要部分。
- 涉及撰寫、修改或審查程式碼時，必須先閱讀並遵守 [Coding 規則](WebRPGDocument/CodingRules.md)。
- 涉及 Git Commit 時，必須先閱讀並遵守 [Git 規則](WebRPGDocument/GitRules.md)。
- 涉及修改 `WebRPG/` 內的 Unity 專案檔案時，必須先閱讀並遵守 [Unity 規則](WebRPGDocument/UnityRules.md)。
- 不要一次載入全部文件；任務涉及多個系統時，再補讀相關文件。
- 搜尋本專案檔案或內容時，優先限定相關路徑與檔案類型；預設排除 Unity 與 .NET 產生物（例如 `Library/`、`Temp/`、`obj/`、`bin/`、`Logs/`），僅在任務需要時納入。
- 讀取檔案與輸出工具結果時，優先提供相關段落、命中位置或摘要；大型場景、Prefab、資產及日誌避免直接輸出全文。需要完整內容才能判斷時，再擴大讀取範圍。

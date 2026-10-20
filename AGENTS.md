# AGENTS.md

## 專案概況

- `WebRPG/`：Unity 6.3（6000.3.11f1）2D 遊戲專案，使用 URP、Input System 和 NativeWebSocket。
- `WebRPGServer/`：以 .NET 9 建置的 ASP.NET Core WebSocket 伺服器，WebSocket 路徑是 `/ws`。
- `WebRPGShared/`：目標為 .NET Standard 2.1 的共用 C# 程式庫；建置後會複製 DLL/PDB 到 `WebRPG/Assets/Plugins/`。
- `WebRPG.sln`：根目錄的 .NET solution。

## 修改規則

- 修改前先問使用者。
- 在允許前不要直接更改檔案。
- 更改檔案時先提供完整diff檔案並等待使用者同意才進行直接修改。
- 先閱讀使用者準備修改的程式碼及相關設定，再做最小且完整的修改。
- 程式修改要符合SOLID。
- 命名時要完整不要簡寫。
- 撰寫程式碼時使用以下網址撰寫的coding conventions : https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions 。
- 不要寫一堆註解，只寫註解在複雜的東西前一行。
- Git Commit命名使用以下網址撰寫的naming conventions : https://www.conventionalcommits.org/en/v1.0.0/ 。
- Git Commit前先向使用者確認是否同意命名。
- Unity 的資產、場景、Prefab、Project Settings 和套件設定可能彼此相依；修改前先確認影響範圍。
- `WebRPGShared` 必須維持與 Unity 相容：目前目標框架是 `netstandard2.1`，C# 語言版本是 9.0。不要直接使用只適用於 .NET 9 的 API。
- 修改 WebSocket 訊息格式或連線行為時，同時檢查 Unity 用戶端與伺服器是否一致。
- 不要編輯 Unity 自動產生的資料夾（例如 `Library/`、`Temp/`、`obj/`、`Logs/`、`UserSettings/`），除非使用者明確要求處理其中內容。
- 不要覆寫、提交或輸出憑證、私密金鑰或其他機密資料。不要把秘密值寫進程式碼或文件。
- 不要為了方便而移除或重建使用者已有的場景、資產或設定。
- 新增規則時，寫清楚它適用的路徑、要做的事，以及必要時的例外情況。

## 建置與驗證

- 若修改 `WebRPGServer` 或 `WebRPGShared`，可從根目錄執行 `dotnet build WebRPG.sln`。
- `WebRPGShared` 建置會把輸出複製到 Unity 的 `Assets/Plugins/`；檢查版本控制差異，確認產生的檔案是否應納入這次修改。
- Unity 場景或資產的行為需要在 Unity Editor 中確認。若無法執行 Editor，請在回覆中說明尚未確認的部分。
- 只執行與本次修改相關且可用的驗證；不要宣稱未執行的檢查已通過。

# Coding 規則

- 用途：統一本專案的程式碼撰寫、修改與審查規則。
- 適用範圍：本專案的程式碼；相容性與通訊規則依涉及的模組適用。

## 規則

- 1：先閱讀使用者準備修改的程式碼及相關設定，再做最小且完整的修改。
- 2：程式修改要符合 SOLID。
- 3：命名時要完整，不要簡寫。
- 4：撰寫程式碼時遵守 [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)，並沿用相關檔案既有的縮排、括號與排版慣例。對已掌握且無疑義的慣例不必重新查閱外部文件；規範有疑義或需要核對時，只查閱相關章節。
- 5：本專案自行維護的 C# 類別（包含巢狀類別、測試與 CI 類別）及介面，須在各宣告前以一行繁體中文註解簡述主要功能；宣告帶有屬性標記時，註解放在屬性標記上方。第三方與自動產生的程式碼不適用。其餘註解僅在複雜內容的前一行撰寫，避免過多註解。
- 6：`WebRPGShared` 必須維持與 Unity 相容：目前目標框架是 `netstandard2.1`，C# 語言版本是 9.0。不要直接使用只適用於 .NET 9 的 API。
- 7：修改 WebSocket 訊息格式或連線行為時，同時檢查 Unity 用戶端與伺服器是否一致。
- 8：新增或修改 UI 相關程式碼時，採用 Model–View–Presenter（MVP）架構：Model 負責資料與業務邏輯，View 負責畫面呈現與轉送使用者操作，Presenter 負責處理操作並協調 Model 與 View；Model 不依賴 View 或 Presenter，View 不直接處理業務邏輯。WebRPG/ 內的 Model 使用一般 C# 類別，不繼承 MonoBehaviour；設定由類別內定義或透過建構子傳入，不透過 Inspector 配置。修改既有 UI 時，套用範圍限於本次需求涉及的程式碼。
- 9：新增或修改系統程式碼時，若有適合目前需求的設計模式（Design Pattern），應予以套用；避免為套用模式而增加不必要的抽象與複雜度。
- 10：本專案自行維護的 C# 型別與成員，在語法允許時必須明確標示存取修飾詞（例如 public、private、protected、internal），包含介面成員，不依賴預設可見性；補齊修飾詞時，維持原本或需求指定的可見性。
- 11：採用 MVP 的 C# 類別，名稱必須包含其職責對應的 Model、View 或 Presenter；繼承相關基底類別或實作相關介面的類別亦同，例如 MainMenuModel、MainMenuView、MainMenuPresenter。
- 12：本專案自行維護的 C# 程式碼不得使用 async void，包含轉換為 void 委派的非同步 lambda。非同步作業使用 Awaitable、Awaitable<T>、Task、Task<T> 等可等待的回傳型別；受 Unity 生命週期或事件委派簽章限制時，保留同步 void 入口，再啟動非同步方法。無法 await 的工作必須在非同步流程內或透過觀察機制處理例外。
- 13：重新命名 WebRPG/ 內的 Unity 元件類別或序列化欄位時，同步更新受影響的程式引用、場景、Prefab 與其他序列化資產，直接設定新欄位的參照，不使用 FormerlySerializedAs。Unity 元件類別與腳本檔名須一致；重新命名腳本時同步移動原有 .meta，保留 GUID 與既有元件綁定。
- 14：WebRPG/ 內自行維護的遊戲非同步流程使用 Unity Awaitable／Awaitable<T>，並維持 WebGL 相容；等待與排程不得依賴 Task.Delay、Task.Run 或背景執行緒計時器。外部套件提供的 Task API 可直接 await；WebRPGServer 與 WebRPGShared 不受此 Awaitable 要求限制。
- 15：WebRPG/ 內每個 Awaitable 實例只消耗一次：可等待的呼叫端使用 await；同步 void 入口無法直接 await 時，使用 AwaitableExtensions.Forget() 觀察完成與例外，並釋放 Awaitable。不得對同一實例同時使用 await 與 Forget()。方法內已有 try/catch，不代表呼叫端可以省略完成觀察。
- 16：WebRPG/ 內對 MonoBehaviour、GameObject、Component 及其他 UnityEngine.Object 衍生型別進行生命週期檢查時，使用 if (unityObject)／if (!unityObject)，不使用 == null、!= null 或 C# null 模式與運算子判斷其存活。一般 C# 類別維持一般 null 檢查。
- 17：WebRPG/ 內採用 MVP 時，Model、View、Presenter 由 MVP 以外的高階類別建立、組裝並管理生命週期，例如 Main。高階類別生成 UI Prefab 時，序列化欄位直接引用該 Prefab 的 View 元件。已知且固定的 Unity 元件相依透過序列化欄位直接綁定，不以 GetComponent、TryGetComponent 或場景搜尋取代。
- 18：WebRPG/ 內不受 Time.timeScale 影響的秒數等待，統一使用 AwaitableExtensions.WaitForSecondsRealtimeAsync(seconds, cancellationToken)。此方法在 Unity 主執行緒以 Awaitable.NextFrameAsync() 搭配 Time.unscaledDeltaTime 累計時間，支援 CancellationToken，採靜態方法，不以 float 作為擴充方法的目標。
- 19：本專案自行維護的一般 C# 方法中，區域函式（local function）放在所屬方法最後，位於主流程結束的 return 敘述之後。
- 20：在 WebRPG/、WebRPGServer/ 與 WebRPGShared/ 開發新功能時，先撰寫驗證預期行為的測試，確認測試因功能尚未實作而失敗，再實作功能；驗證時必須執行相關測試，全部通過後才算完成實作。
- 21：`WebRPG/`、`WebRPGServer/` 與 `WebRPGShared/` 內新增或修改程式碼時，若需要 helper method，須放在獨立的腳本／程式碼檔案，不限於 UI 功能；依用途命名，與使用端類別分開維護。屬於類別自身職責的業務邏輯、事件處理及生命週期方法仍保留在該類別。既有且不在本次修改範圍內的程式碼，不因此要求全面重構。
- 22：`WebRPG/Assets/WebRPG/Code/` 內的正式遊戲腳本，方法中使用的固定字串須宣告為類別層級的具名 `const string`，不得直接寫在方法內；包含畫面文字、錯誤訊息、設定與狀態識別值、分隔符號及格式字串。動態內容以常數與變數組合，或使用格式字串常數搭配 `string.Format`；空字串使用 `string.Empty`，成員名稱使用 `nameof`。此規則不套用至測試腳本。
- 23：上述腳本含中文或特殊符號時，讀寫與轉換須明確使用 UTF-8，不依賴系統預設編碼；修改後核對 diff 中的實際字元，避免亂碼或分隔符號被錯誤替換。
- 24：`WebRPG/Assets/WebRPG/` 內新增或修改遊戲美術與 UI 時，不以程式繪製或生成圖示、插圖、紋理等美術圖片；使用可替換的圖片資產。尚無圖片時，以標準 UI Image 方塊佔位並在下方顯示名稱，保留後續直接指定 Sprite 的方式。此規則不限制 UI 排版、讀條與地圖連線等必要的功能呈現，也不要求改動本次需求範圍外的既有內容。

- 25：`WebRPG/`、`WebRPGServer/`、`WebRPGShared/` 與其測試中的自行維護 C# 程式碼，具有業務、協定、限制、時間、容量、比例、版面或容差意義的固定數值，須先宣告為具名常數或既有設定，再於運算、比較及呼叫參數中使用；名稱須表達用途，涉及單位時一併標明，例如 `MaximumAccumulatedTimeSeconds`，不可只用數值命名。即使只使用一次也適用；同一規則的數值應重用同一宣告。明確表示零值、索引起點、遞增量、正規化範圍或幾何中心等通用數學意義的數值，以及已具語意名稱的常數／設定預設值、列舉值與直接描述測試輸入或預期結果的資料，可保留字面值；第三方及自動產生程式碼不適用。

- 26、`WebRPGServer/`、`WebRPGServer.Tests/`、`WebRPG/Assets/WebRPG/Code/` 與 `WebRPG/Assets/WebRPG/Tests/Editor/` 的自有 C# 腳本按功能放入子資料夾。新增或搬移腳本時，優先沿用既有分類，只有新增獨立功能時才建立新的功能資料夾。跨功能共用的測試輔助程式可集中在 `Common/`，只供單一功能使用的輔助程式應與對應測試放在一起。
  - `WebRPGServer/` 依功能使用 `Accounts/`、`Chat/`、`Lobby/` 等資料夾，應用程式入口 `Program.cs` 留在專案根目錄。
  - `WebRPGServer.Tests/` 依功能使用 `Accounts/`、`Lobby/`、`Multiplayer/` 等資料夾。需明確指定遠端環境才執行的測試及其專用輔助程式放在 `Remote/`。
  - `WebRPG/Assets/WebRPG/Code/` 的遊戲腳本按功能放入 `Account/`、`Adventure/`、`Chat/`、`Lobby/`、`MainMenu/` 等資料夾。啟動流程放在 `Bootstrap/`，跨功能共用的程式放在 `Common/`。依既有組件邊界分類，搬移腳本時一併搬移原有 `.meta`，以保留 GUID。
  - `WebRPG/Assets/WebRPG/Tests/Editor/` 依功能使用 `Account/`、`Adventure/`、`Lobby/`、`MainMenu/`、`Multiplayer/` 等資料夾。分類時保留 `WebRPG.Tests.Editor.asmdef` 在 `Editor/`，搬移腳本時一併搬移原有 `.meta`，以保留 GUID。
  - 僅調整資料夾時，保留腳本內容與命名空間，並更新文件及其他依賴舊路徑的引用。

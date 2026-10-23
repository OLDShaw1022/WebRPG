# Coding 規則

- 用途：統一本專案的程式碼撰寫、修改與審查規則。
- 適用範圍：本專案的程式碼；相容性與通訊規則依涉及的模組適用。

## 規則

- 1：先閱讀使用者準備修改的程式碼及相關設定，再做最小且完整的修改。
- 2：程式修改要符合 SOLID。
- 3：命名時要完整，不要簡寫。
- 4：撰寫程式碼時遵守 [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)，並沿用相關檔案既有的縮排、括號與排版慣例。對已掌握且無疑義的慣例不必重新查閱外部文件；規範有疑義或需要核對時，只查閱相關章節。
- 5：不要撰寫過多註解；僅在複雜內容的前一行撰寫註解。
- 6：`WebRPGShared` 必須維持與 Unity 相容：目前目標框架是 `netstandard2.1`，C# 語言版本是 9.0。不要直接使用只適用於 .NET 9 的 API。
- 7：修改 WebSocket 訊息格式或連線行為時，同時檢查 Unity 用戶端與伺服器是否一致。
- 8：新增或修改 UI 相關程式碼時，採用 Model–View–Presenter（MVP）架構：Model 負責資料與業務邏輯，View 負責畫面呈現與轉送使用者操作，Presenter 負責處理操作並協調 Model 與 View；Model 不依賴 View 或 Presenter，View 不直接處理業務邏輯。修改既有 UI 時，套用範圍限於本次需求涉及的程式碼。
- 9：新增或修改系統程式碼時，若有適合目前需求的設計模式（Design Pattern），應予以套用；避免為套用模式而增加不必要的抽象與複雜度。
- 10：本專案自行維護的 C# 型別與成員，在語法允許時必須明確標示存取修飾詞（例如 public、private、protected、internal），包含介面成員，不依賴預設可見性；補齊修飾詞時，維持原本或需求指定的可見性。
- 11：採用 MVP 的 C# 類別，名稱必須包含其職責對應的 Model、View 或 Presenter；繼承相關基底類別或實作相關介面的類別亦同，例如 ChatModel、ChatView、ChatPresenter。
- 12：本專案自行維護的 C# 程式碼不得使用 async void，包含轉換為 void 委派的非同步 lambda。非同步作業使用 Task、Task<T> 等可等待的回傳型別；受 Unity 生命週期或事件委派簽章限制時，保留同步 void 入口，再啟動非同步方法。無法 await 的工作必須在非同步流程內或透過觀察機制處理例外。
- 13：重新命名 WebRPG/ 內的 Unity 元件類別或序列化欄位時，同步更新受影響的程式引用、場景、Prefab 與其他序列化資產，直接設定新欄位的參照，不使用 FormerlySerializedAs。Unity 元件類別與腳本檔名須一致；重新命名腳本時同步移動原有 .meta，保留 GUID 與既有元件綁定。

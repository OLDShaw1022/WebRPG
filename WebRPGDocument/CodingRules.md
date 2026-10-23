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

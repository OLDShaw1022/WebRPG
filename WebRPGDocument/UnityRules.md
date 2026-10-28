# Unity 規則

- 適用範圍：`WebRPG/` 內的 Unity 專案檔案。
- 用途：規範 Unity 檔案修改的影響範圍與既有內容保護。

## 規則

- 1：Unity 的資產、場景、Prefab、Project Settings 和套件設定可能彼此相依；修改前先確認影響範圍。
- 2：不要編輯 Unity 自動產生的資料夾（例如 `Library/`、`Temp/`、`obj/`、`Logs/`、`UserSettings/`），除非使用者明確要求處理其中內容。
- 3：不要為了方便而移除或重建使用者已有的場景、資產或設定。
- 4：`WebRPG/Assets/WebRPG/` 內新增或修改的 UI，其固定基底與版面以 Prefab 製作，由程式生成 Prefab 實例；背景、標題、固定按鈕、文字、元件引用及樣式須能在 Unity Editor 中視覺化調整。既有 UI 未納入修改範圍時，不因此要求全面重構。
- 5：上述 UI 中需要依執行期資料動態生成的部分，由腳本控制；固定版面不在執行時重新建立或覆寫，使 Editor 中的調整能保留。
- 6：`WebRPG/`、`WebRPGServer/` 與 `WebRPGShared/` 內新增或修改程式碼時，helper method 的拆分要求統一依 [Coding 規則](CodingRules.md)規則 21 執行。

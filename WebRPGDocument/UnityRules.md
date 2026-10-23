# Unity 規則

- 適用範圍：`WebRPG/` 內的 Unity 專案檔案。
- 用途：規範 Unity 檔案修改的影響範圍與既有內容保護。

## 規則

- 1：Unity 的資產、場景、Prefab、Project Settings 和套件設定可能彼此相依；修改前先確認影響範圍。
- 2：不要編輯 Unity 自動產生的資料夾（例如 `Library/`、`Temp/`、`obj/`、`Logs/`、`UserSettings/`），除非使用者明確要求處理其中內容。
- 3：不要為了方便而移除或重建使用者已有的場景、資產或設定。

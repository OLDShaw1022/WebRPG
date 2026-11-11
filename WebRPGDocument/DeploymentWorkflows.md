# GitHub Actions 部署工作流程

- 適用範圍為 WebRPG 正式與測試環境的 EC2 伺服器和 Unity WebGL 部署。
- 四支 workflow 都從 GitHub Actions 手動觸發，只部署遠端 `main` 的最新提交。

## 工作流程與部署目標

| GitHub Actions 名稱 | 部署目標 | 執行條件 |
| --- | --- | --- |
| [Deploy WebRPG server to EC2](../.github/workflows/deploy-server-to-ec2.yml) | EC2 的 `myserver` 目錄與 `webrpg` 服務。後端為 `ws.oldshaw-webrpg.com` | 選 `main` 並輸入 `deploy` |
| [Deploy WebRPG test server to EC2](../.github/workflows/deploy-test-server-to-ec2.yml) | 同一台 EC2 的 `myserver-test` 目錄與 `webrpg-test` 服務。後端為 `ws-test.oldshaw-webrpg.com` | 選 `main` 並輸入 `deploy` |
| [Build and deploy WebRPG Web](../.github/workflows/deploy-web-to-firebase.yml) | Firebase 正式站點，網址為 `https://oldshaw-webrpg.com/` | 選 `main` |
| [Build and deploy WebRPG test WebGL](../.github/workflows/deploy-test-web-to-firebase.yml) | Firebase 測試站點 `oldshaw-web-rpg-test`，網址為 `https://test.oldshaw-webrpg.com/` | 選 `main` |

## 設定

- EC2 部署使用 GitHub Actions secrets `EC2_HOST`、`EC2_USER`、`EC2_SSH_PRIVATE_KEY`。正式伺服器 workflow 另用 `WEBRPG_TEST_HOST_EMAIL`、`WEBRPG_TEST_HOST_PASSWORD`、`WEBRPG_TEST_SECOND_EMAIL` 和 `WEBRPG_TEST_SECOND_PASSWORD`，透過兩個不同的帳號驗證正式後端的大廳連線。
- Unity 建置使用 GitHub Actions secrets `UNITY_EMAIL`、`UNITY_PASSWORD`、`UNITY_LICENSE`。
- 在 GitHub repository 的 **Settings → Environments**，分別為 `firebase-production` 和 `firebase-test` 設定 `FIREBASE_WORKLOAD_IDENTITY_PROVIDER` 與 `FIREBASE_DEPLOY_SERVICE_ACCOUNT`。前者是 Google Cloud Workload Identity Provider 的完整資源名稱，後者是部署用服務帳號的 email。Google Cloud 的 Provider 條件和服務帳號授權必須允許對應的 workflow 與環境。
- 測試 WebGL workflow 在 CI 建置時將伺服器清單設為測試後端，部署時建立 `hosting:test` 對應及 Firebase 設定。

## 操作與驗收

1. 在 GitHub 的 **Actions** 選擇對應環境的 workflow，點 **Run workflow**，依上表選擇分支及輸入部署確認值。若同批變更包含後端和 WebGL，先部署後端並確認成功，再部署 WebGL。
2. EC2 workflow 會先跑伺服器測試，再發布和重啟對應服務。正式流程部署後執行遠端大廳驗證。測試流程若本機啟動檢查失敗，會還原上一版，並在啟動成功後檢查公開帳號 API 的 CORS 預檢。
3. WebGL workflow 會建置 Unity、打包 Firebase Hosting 產物並核對檔案雜湊。測試流程另會驗證公開首頁與 WASM 的回應。
4. 在對應網站開啟遊戲，確認登入及建立、加入房間的流程。若 workflow 失敗，可在該次 Actions 執行記錄展開失敗步驟，查看建置、驗證或部署結果。

## 相關文件

- [帳號登入](Accounts.md)
- [房間大廳](MultiplayerLobby.md)

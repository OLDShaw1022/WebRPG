# 貨幣與商店

- 設計狀態：待確認。
- 實作狀態：帳號錢包、兌換目錄、水晶兌換與 PayPal 儲值已接入。挑戰發獎待後續階段。
- 適用範圍：Unity 主選單與商店、挑戰結算、WebRPGServer 帳號錢包及水晶儲值。
- 用途：定義雙貨幣規則、商店操作與 PayPal 付款入帳流程。

## 規則

- 1：帳號錢包保存整數金幣與水晶。金幣來自遊戲獎勵及水晶兌換，供後續局外成長使用。水晶在儲值付款經伺服器確認後發放，首版用於兌換金幣。
- 2：當輪金幣與帳號金幣分開。新一輪依冒險規則初始化當輪金幣。結算時由伺服器依可驗證的單人或多人挑戰結果計算帳號金幣獎勵。每輪每名玩家只能入帳一次。結算畫面分別顯示本輪金幣與本次入帳。
- 3：玩家登入後可從主選單開啟商店，查看兩種貨幣餘額，並切換「水晶兌換金幣」與「儲值水晶」。關閉商店後返回主選單。
- 4：兌換採固定組合，顯示所需水晶與可得金幣，確認後由伺服器扣除水晶並發放金幣。餘額不足時顯示差額，並提供切換至儲值頁的入口。商品條件已變更時，顯示最新條件並請玩家重新確認。
- 5：儲值方案顯示應付金額、幣別與可得水晶。玩家付款後可查詢「等待付款」「處理中」「已入帳」「失敗」或「已取消」。離開商店或登出後，可用同一帳號重新登入查詢訂單。
- 6：伺服器管理兌換比例、商品條件與挑戰獎勵。兌換與獎勵數值依主玩法規劃保留 JSON 設定能力。建立儲值訂單時保存帳號、商品、金額、幣別與應發水晶數，後續依該筆訂單的條件驗證及入帳。
- 7：部分退款只更新訂單狀態。累計全額退款或 PayPal 確認拒付後，扣除尚未使用的該筆水晶，並撤銷任何使用該筆水晶的兌換。混用其他來源水晶的兌換會退還其他來源的水晶。金幣餘額不足時先扣除現有餘額，差額記為欠額，由後續取得的帳號金幣抵扣。保留原入帳流水，以沖銷及抵扣流水記錄變動。

## 伺服器與資料

| 項目 | 職責 |
| --- | --- |
| 錢包 | 沿用登入帳號 UUID，在 PostgreSQL 保存金幣與水晶餘額。 |
| 錢包流水 | 記錄帳號、貨幣、變動數量、來源、關聯單號與時間，供查詢與對帳。 |
| 商品目錄 | 保存兌換組合與儲值方案的商品識別、啟用狀態與商品條件。 |
| 儲值訂單 | 保存建立時的商品條件、付款狀態、外部交易識別與入帳結果。 |
| 遊戲 API | 沿用帳號驗證，提供查詢餘額、商品與本人訂單，以及兌換和建立儲值訂單的 HTTP API。 |

- 兌換的扣水晶、加金幣與流水在同一筆資料庫交易完成。相同兌換請求重送時回傳原結果，同時兌換時依資料庫餘額檢查，避免超扣。
- 挑戰發獎以挑戰識別與玩家帳號作為唯一依據，將發獎紀錄、餘額與流水一起寫入。
- 儲值由後端核對可信付款通知或平台收據，驗證交易與帳號、訂單及商品的對應關係，並依通路核對付款狀態、金額與幣別。以通路及外部交易識別防止重複入帳，訂單狀態、水晶餘額與流水在同一筆交易更新。遊戲依伺服器回傳狀態顯示結果。
- WebGL 與 PC 使用 PayPal 託管結帳。伺服器經 PayPal API 查詢及請款，確認交易後才入帳。帳號先使用既有無儲值來源的水晶，付費水晶依訂單建立時間分配至兌換。 [PayPal Orders API](https://developer.paypal.com/api/rest/integration/orders-api) · [PayPal Webhooks](https://developer.paypal.com/api/rest/webhooks/)

## 第一階段設定與操作

- 伺服器啟動時在帳號資料庫建立 `game_wallets`、`game_shop_exchanges` 與 `game_wallet_entries`。帳號初始金幣與水晶皆為 0。
- 在伺服器 JSON 設定的 `Shop:Offers` 陣列配置商品。每筆使用唯一的 `Id`。金幣兌換使用非負整數 `CrystalCost` 與正整數 `GoldAmount`。空物件使用正整數 `GoldCost`，並將 `CrystalCost` 與 `GoldAmount` 設為 0。預設目錄提供「0 水晶兌換 100 金幣」、「10 水晶兌換 100 金幣」及「100 金幣購買空物件」。空物件成交時只扣金幣並記錄流水。修改設定後重啟伺服器。未配置時，商店仍顯示兩種餘額，商品目錄為空。
- 登入後由主選單進入商店，左上角顯示金幣與水晶餘額。商品以可捲動的格子排列，每格顯示圖示區、商品名稱和金幣或水晶花費。點擊商品後可在彈窗確認或取消購買。購買成功、餘額不足與其他操作結果會以彈窗顯示，餘額不足時會列出差額。若送出後發生連線錯誤，可在彈窗重試同一筆請求。
- 查詢使用 `POST /api/accounts/shop`，兌換使用 `POST /api/accounts/shop/exchange`。兩者沿用帳號 API 的 Bearer Token 驗證。兌換請求附上 UUID 與玩家已確認的商品條件。

## PayPal 儲值設定與操作

- 在伺服器的 `Shop:TopUpOffers` 陣列設定各方案的 `Id`、`Currency`、`Amount`、`Crystals` 與 `Enabled`。預設方案為 NT$1 購買 100 水晶，PayPal 儲值功能預設關閉。啟用的方案需要唯一識別、正數金額和正整數水晶。金額使用幣別的主要單位，例如 USD 的 `4.99` 或 TWD 的 `150`。 [PayPal 支援幣別](https://developer.paypal.com/api/codes/currency/)
- 部署環境設定 `Shop__PayPal__ClientId`、`Shop__PayPal__ClientSecret`、`Shop__PayPal__WebhookId`、`Shop__PayPal__CallbackUrl` 和 `Shop__PayPal__GameUrl`。後兩項分別是後端的公開 HTTPS `/api/payments/paypal/return` 網址及同環境的 WebGL 首頁。反向代理須將 `/api/payments/paypal/` 轉發到該環境的 WebRPGServer。
- 在相同 PayPal REST 應用程式設定 HTTPS `/api/payments/paypal/webhook`，訂閱 `CHECKOUT.ORDER.APPROVED`、`CHECKOUT.PAYMENT-APPROVAL.REVERSED`、`PAYMENT.CAPTURE.PENDING`、`PAYMENT.CAPTURE.COMPLETED`、`PAYMENT.CAPTURE.DECLINED`、`PAYMENT.CAPTURE.REFUNDED` 與 `PAYMENT.CAPTURE.REVERSED`。PayPal 回傳的 Webhook ID 需與該應用程式及環境一致。 [PayPal 通知事件](https://developer.paypal.com/api/rest/webhooks/event-names/)
- 測試環境使用獨立的 PostgreSQL、PayPal Sandbox 應用程式及沙盒買家帳號，設定 `Shop__PayPal__Enabled=true` 和 `Shop__PayPal__Live=false`。正式環境以正式商戶的 REST 應用程式設定 `Shop__PayPal__Live=true`，在完成付款、退款、拒付及對帳驗收後設定 `Shop__PayPal__Enabled=true`。台灣 PayPal 帳戶目前僅可付款至境外註冊的 PayPal 帳戶。 [PayPal 台灣付款說明](https://www.paypal.com/tw/cshelp/article/%E6%88%91%E8%A6%81%E5%A6%82%E4%BD%95%E4%BB%98%E6%AC%BE%E5%91%A2%EF%BC%9F-help293?locale.x=zh_TW)
- 在商店點「儲值水晶」選擇方案、建立訂單，接著點「前往 PayPal」開啟付款頁。付款完成後回到遊戲重新整理，或從訂單明細同步狀態。儲值頁顯示該帳號最近 30 筆同環境訂單，可從等待付款的訂單重新開啟 PayPal。
- 商店查詢回傳方案和訂單。建單使用 `POST /api/accounts/shop/top-ups`，付款補查使用 `POST /api/accounts/shop/top-ups/{orderId}/sync`，均使用登入帳號的 Bearer Token。訂單以建立時的金額、幣別及發放數量為準。
- 對帳時以 `game_shop_top_ups` 的 PayPal 訂單和收款識別對應 PayPal 商戶交易。用訂單 UUID 串接 `game_wallet_entries` 的入帳及沖銷流水，從 `game_shop_top_up_allocations` 查詢受影響的兌換。退款可從 PayPal 商戶後台操作。

## 驗收情境

- 對應規則 1 至 3：玩家可從主選單開啟商店，看到兩種餘額。開始新一輪後帳號餘額仍在，重新登入同一帳號後可讀取餘額。
- 對應規則 2：對同一玩家重送同一輪結算，只發放一次獎勵。伺服器無法驗證的挑戰結果不得發獎。
- 對應規則 4 與 6：成功兌換時兩種餘額一起更新。餘額不足或商品調價時保留原餘額。相同請求重送只扣一次，兩台裝置同時兌換也不超扣。
- 對應規則 5 與 6：等待、失敗、取消或驗證失敗的付款不入帳。同一筆付款通知重送只發放一次水晶。付款後關閉遊戲再登入，仍可查詢訂單及正確餘額。
- 對應規則 7：部分退款顯示狀態且保留餘額。全額退款或拒付只沖銷一次。驗證混用其他訂單水晶時會退還其他來源的水晶，金幣不足時由後續所得抵扣差額。

## 相關文件

- [主選單](MainMenu.md)：記錄登入後入口與介面分工。
- [帳號登入](Accounts.md)：記錄帳號識別、驗證與 PostgreSQL。
- [冒險原型](AdventurePrototype.md)：記錄目前當輪金幣與結算行為。
- [主玩法](CoreGameplay.md)：記錄雙貨幣與挑戰結算在核心循環中的位置。

## 待確認

- 金幣結算公式，包括成功、失敗和中途放棄的獎勵，以及各模式可用的伺服器驗證依據。
- 水晶兌換組合、金幣用途與局外成長成本。
- 正式販售的儲值方案面額、幣別與發放水晶數。

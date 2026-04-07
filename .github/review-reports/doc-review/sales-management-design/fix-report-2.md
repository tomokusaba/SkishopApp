# fix-report-2: sales-management-design.md

## 修正サマリー

| 指摘 ID | 重要度 | 内容 | 対応状況 |
|---------|--------|------|---------|
| H-1 | High | デフォルト送料が 3 箇所で矛盾（500/800/550 円） | ✅ 修正完了 |
| H-2 | High | MemberRankDiscounts config キーが誤マッピング | ✅ 修正完了 |
| H-3 | High | Shipped→Returned, Cancelled→Refunded の 2 遷移が欠落 | ✅ 修正完了 |
| H-4 | High | PaymentService の通信プロトコル矛盾（HTTPS vs gRPC） | ✅ 修正完了 |

## 修正詳細

### H-1: デフォルト送料を spec.md に統一
- 配送料テーブル: `500 円` → `550 円（税込）`（spec.md 準拠）
- appsettings.json: `DefaultShippingFee: 800` → `DefaultShippingFee: 550`
- spec.md の地域別・オプション別料金も配送料テーブルに追加（北海道・沖縄: 1,100 円、お急ぎ便: +330 円、大型商品: +1,650 円）

### H-2: MemberRankDiscounts 修正
- 修正前: `"Gold": 8000, "Platinum": 5000`
- 修正後: `"Silver": 8000, "Gold": 5000, "Platinum": 0`（Platinum は常時無料）

### H-3: 状態遷移追加
- Mermaid stateDiagram-v2 に追加:
  - `Shipped --> Returned : 受取拒否 / 配送事故`
  - `Cancelled --> Refunded : 決済キャプチャ済みの場合の返金`
- OrderStateMachine に追加:
  - `{ (OrderStatus.Shipped, OrderStatus.Returned), true }`
  - `{ (OrderStatus.Cancelled, OrderStatus.Refunded), true }`

### H-4: PaymentService プロトコル統一（gRPC）
- ステップ詳細テーブル: 通信プロトコルを `HTTPS` → `gRPC（内部）/ HTTPS（外部 PG）` に修正
- アーキテクチャ図: `PAY_HTTPS[PaymentService<br>HTTPS]` → `PAY_GRPC[PaymentService<br>gRPC]`
- シーケンス図: `participant Pay as PaymentService<br>(HTTPS)` → `(gRPC)`
- SagaCoordinator の `RpcException` キャッチは内部 PaymentService gRPC ラッパーと整合

## 結果: Critical 0 / High 0（4 件修正完了）

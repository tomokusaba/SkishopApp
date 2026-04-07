# ドキュメント修正ログ

- **対象**: design-docs/user-management-design.md
- **イテレーション**: 1 / 5
- **修正日時**: 2026-04-03 12:08
- **対応レビュー**: check-report-1.md

## 修正サマリー
| 修正件数 | Critical | High | 合計 |
|---------|----------|------|------|
| 修正済み | 12 | 14 | 26 |
| 修正不可（エスカレーション） | 0 | 0 | 0 |

## 修正詳細
| # | 指摘ID | 重要度 | 出典Agent | 修正パターン | 修正箇所（セクション） | 修正内容の概要 |
|---|--------|--------|----------|-------------|---------------------|-------------|
| 1 | C-01 | Critical | architect | A: 既存修正 | §1概要, §3アーキテクチャ, §4 API, §14実装コード | AuthService責務（ユーザー登録・パスワード管理・ロール管理）を除去。プロファイル管理特化に書き換え |
| 2 | C-02 | Critical | architect | B: 新規追加 | §3 ER図, §4 API, §14エンティティ定義 | Address, Wishlist, WishlistItem エンティティを追加 |
| 3 | C-03 | Critical | architect | B: 新規追加 | §3 ER図, §14エンティティ, 新規BackgroundServiceセクション | MemberRank エンティティ + MemberRankEvaluationService（年次バッチ）を追加 |
| 4 | C-04 | Critical | compliance | B: 新規追加 | 新規§GDPR/DSRセクション, §3 ER図, §5イベント | DeletionRequest エンティティ、14日猶予期間、DsrTimeoutMonitorService、データ削除フロー全体を追加 |
| 5 | C-05 | Critical | compliance | B: 新規追加 | 新規§同意管理セクション, §3 ER図, §4 API, §5イベント | Consent エンティティ、同意API、anonymous_consents、consent.revoked イベントを追加 |
| 6 | C-06 | Critical | dba | A: 既存修正 | §3 ER図, §14 User エンティティ | User エンティティから password_hash カラムを除去 |
| 7 | C-07 | Critical | dba | A: 既存修正 | §3 ER図, §3コンポーネント図, §14 User エンティティ | roleId FK, Role/Permission エンティティ, RoleService/RoleRepository を除去 |
| 8 | C-08 | Critical | dba | A: 既存修正 | §11環境変数 | DB名を skishopdb → userdb に変更 |
| 9 | C-09 | Critical | architect | A: 既存修正 | §5イベント設計 | Kafka トピックを spec.md 準拠に全面書き換え（発行: user.deleted, user.profile-updated, consent.revoked / 購読: user.registered, user.deletion.completed） |
| 10 | C-10 | Critical | security | B: 新規追加 | 新規§Outboxパターンセクション, §3 ER図 | outbox_events テーブル、OutboxPublisher BackgroundService（動的バックオフ100ms〜5s, Advisory Lock）を追加 |
| 11 | C-11 | Critical | security | A: 既存修正 | §7エラー処理 | エラーレスポンスを RFC 9457 Problem Details 形式に変更 |
| 12 | C-12 | Critical | business | B: 新規追加 | §14 User エンティティ, §GDPR | processing_restricted, restriction_reason, restricted_at カラムを追加 |
| 13 | H-01 | High | architect | B: 新規追加 | BackgroundServiceセクション | MemberRankEvaluationService に Advisory Lock（hashtext('member_rank_eval')）を追加 |
| 14 | H-02 | High | architect | B: 新規追加 | GDPR/DSRセクション | DsrTimeoutMonitorService（1時間ポーリング、24時間タイムアウト、最大3リトライ）を追加 |
| 15 | H-03 | High | architect | A: 既存修正 | §2 NuGet, §3コンポーネント図, §8キャッシュ, §11環境変数 | Redis関連記述（StackExchange.Redis, CacheService, Redis環境変数）を全除去 |
| 16 | H-04 | High | dba | B: 新規追加 | 新規§CHECK制約セクション | 全ステータスカラムに CHECK 制約を追加（spec.md定義準拠） |
| 17 | H-05 | High | dba | A: 既存修正 | §3 ER図, §14エンティティ定義 | 全日時カラムを TIMESTAMP WITH TIME ZONE / DateTimeOffset に変更 |
| 18 | H-06 | High | dba | B: 新規追加 | 新規§FK制約セクション | FK 制約テーブル（ON DELETE/ON UPDATE）を追加 |
| 19 | H-07 | High | security | A: 既存修正 | §14 Endpoints実装例 | IDOR防止（ClaimsPrincipal からのオーナーシップ検証）を追加 |
| 20 | H-08 | High | security | A: 既存修正 | §14 サービス実装例 | PII ログ出力を修正（{Email} → {UserId}） |
| 21 | H-09 | High | programing | A: 既存修正 | §2技術スタック, §10テスト戦略 | Moq → NSubstitute に変更、Shouldly を追加 |
| 22 | H-10 | High | programing | A: 既存修正 | §3シーケンス図 | ユーザー登録フローを AuthService→Kafka(user.registered)→UserManagementService に修正 |
| 23 | H-11 | High | business | B: 新規追加 | §GDPR/DSRセクション, §4 API | データエクスポートAPI（POST /api/v1/users/{userId}/data-export）を追加 |
| 24 | H-12 | High | qa-manager | B: 新規追加 | §10テスト戦略 | MemberRank テスト戦略（昇格・降格・Advisory Lock 検証）を追加 |
| 25 | H-13 | High | architect | A: 既存修正 | §4 API設計 | 全APIパスに /api/v1/ プレフィックスを追加 |
| 26 | H-14 | High | programing | A: 既存修正 | §14 UserService | roleRepository 参照を除去 |

## 修正不可事項（人間対応が必要）
なし

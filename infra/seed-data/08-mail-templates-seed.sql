-- SkiShop Seed Data: MailSendService — 追加メールテンプレート 6件
-- 対象DB: mailsenddb
-- 既存テンプレート: welcome-email, order-confirmation, newsletter-spring, to-be-deleted
-- べき等実行: ON CONFLICT (id) DO NOTHING

BEGIN;

-- =============================================================
-- 追加メールテンプレート（6件）
-- =============================================================
INSERT INTO mail_templates (
    id, name, subject,
    html_body, text_body,
    template_type, variables, is_active,
    created_at, updated_at
) VALUES

-- 1. パスワードリセット
('tmpl-password-reset', 'password-reset',
 'パスワードリセットのご案内 — SkiShop',
 '<!DOCTYPE html>
<html lang="ja">
<head><meta charset="UTF-8"></head>
<body style="font-family: sans-serif; color: #333; max-width: 600px; margin: 0 auto;">
  <h2 style="color: #1a73e8;">パスワードリセット</h2>
  <p>{{userName}} 様</p>
  <p>パスワードリセットのリクエストを受け付けました。</p>
  <p>以下のリンクをクリックして、新しいパスワードを設定してください。</p>
  <p style="text-align: center; margin: 30px 0;">
    <a href="{{resetUrl}}" style="background: #1a73e8; color: #fff; padding: 12px 30px; text-decoration: none; border-radius: 4px;">パスワードをリセット</a>
  </p>
  <p style="color: #666; font-size: 14px;">このリンクの有効期限は {{expiresInMinutes}} 分です。</p>
  <p style="color: #666; font-size: 14px;">このメールに心当たりがない場合は、無視してください。</p>
  <hr style="border: none; border-top: 1px solid #eee; margin: 30px 0;">
  <p style="color: #999; font-size: 12px;">SkiShop カスタマーサポート</p>
</body>
</html>',
 '{{userName}} 様

パスワードリセットのリクエストを受け付けました。

以下のURLにアクセスして、新しいパスワードを設定してください：
{{resetUrl}}

有効期限: {{expiresInMinutes}} 分

このメールに心当たりがない場合は、無視してください。

SkiShop カスタマーサポート',
 'TRANSACTIONAL',
 '["userName", "resetUrl", "expiresInMinutes"]'::jsonb,
 true,
 '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09'),

-- 2. 発送完了通知
('tmpl-order-shipped', 'order-shipped',
 '【SkiShop】ご注文の商品を発送しました（{{orderNumber}}）',
 '<!DOCTYPE html>
<html lang="ja">
<head><meta charset="UTF-8"></head>
<body style="font-family: sans-serif; color: #333; max-width: 600px; margin: 0 auto;">
  <h2 style="color: #1a73e8;">発送完了のお知らせ</h2>
  <p>{{userName}} 様</p>
  <p>ご注文番号 <strong>{{orderNumber}}</strong> の商品を発送いたしました。</p>
  <table style="width: 100%; border-collapse: collapse; margin: 20px 0;">
    <tr style="background: #f5f5f5;">
      <td style="padding: 10px; border: 1px solid #ddd;">配送業者</td>
      <td style="padding: 10px; border: 1px solid #ddd;">{{carrier}}</td>
    </tr>
    <tr>
      <td style="padding: 10px; border: 1px solid #ddd;">追跡番号</td>
      <td style="padding: 10px; border: 1px solid #ddd;">{{trackingNumber}}</td>
    </tr>
    <tr style="background: #f5f5f5;">
      <td style="padding: 10px; border: 1px solid #ddd;">お届け予定日</td>
      <td style="padding: 10px; border: 1px solid #ddd;">{{estimatedDeliveryDate}}</td>
    </tr>
  </table>
  <p style="text-align: center; margin: 30px 0;">
    <a href="{{trackingUrl}}" style="background: #1a73e8; color: #fff; padding: 12px 30px; text-decoration: none; border-radius: 4px;">配送状況を確認</a>
  </p>
  <hr style="border: none; border-top: 1px solid #eee; margin: 30px 0;">
  <p style="color: #999; font-size: 12px;">SkiShop カスタマーサポート</p>
</body>
</html>',
 '{{userName}} 様

ご注文番号 {{orderNumber}} の商品を発送いたしました。

配送業者: {{carrier}}
追跡番号: {{trackingNumber}}
お届け予定日: {{estimatedDeliveryDate}}

配送状況の確認: {{trackingUrl}}

SkiShop カスタマーサポート',
 'TRANSACTIONAL',
 '["userName", "orderNumber", "carrier", "trackingNumber", "estimatedDeliveryDate", "trackingUrl"]'::jsonb,
 true,
 '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09'),

-- 3. 注文キャンセル
('tmpl-order-cancelled', 'order-cancelled',
 '【SkiShop】ご注文のキャンセルが完了しました（{{orderNumber}}）',
 '<!DOCTYPE html>
<html lang="ja">
<head><meta charset="UTF-8"></head>
<body style="font-family: sans-serif; color: #333; max-width: 600px; margin: 0 auto;">
  <h2 style="color: #e53935;">注文キャンセル完了</h2>
  <p>{{userName}} 様</p>
  <p>ご注文番号 <strong>{{orderNumber}}</strong> のキャンセルが完了しました。</p>
  <table style="width: 100%; border-collapse: collapse; margin: 20px 0;">
    <tr style="background: #f5f5f5;">
      <td style="padding: 10px; border: 1px solid #ddd;">キャンセル理由</td>
      <td style="padding: 10px; border: 1px solid #ddd;">{{cancelReason}}</td>
    </tr>
    <tr>
      <td style="padding: 10px; border: 1px solid #ddd;">返金額</td>
      <td style="padding: 10px; border: 1px solid #ddd;">¥{{refundAmount}}</td>
    </tr>
    <tr style="background: #f5f5f5;">
      <td style="padding: 10px; border: 1px solid #ddd;">返金方法</td>
      <td style="padding: 10px; border: 1px solid #ddd;">{{refundMethod}}</td>
    </tr>
  </table>
  <p style="color: #666; font-size: 14px;">返金の反映には数営業日かかる場合がございます。</p>
  <hr style="border: none; border-top: 1px solid #eee; margin: 30px 0;">
  <p style="color: #999; font-size: 12px;">SkiShop カスタマーサポート</p>
</body>
</html>',
 '{{userName}} 様

ご注文番号 {{orderNumber}} のキャンセルが完了しました。

キャンセル理由: {{cancelReason}}
返金額: ¥{{refundAmount}}
返金方法: {{refundMethod}}

返金の反映には数営業日かかる場合がございます。

SkiShop カスタマーサポート',
 'TRANSACTIONAL',
 '["userName", "orderNumber", "cancelReason", "refundAmount", "refundMethod"]'::jsonb,
 true,
 '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09'),

-- 4. ポイント獲得通知
('tmpl-point-earned', 'point-earned',
 '【SkiShop】{{earnedPoints}}ポイントが付与されました',
 '<!DOCTYPE html>
<html lang="ja">
<head><meta charset="UTF-8"></head>
<body style="font-family: sans-serif; color: #333; max-width: 600px; margin: 0 auto;">
  <h2 style="color: #43a047;">ポイント付与のお知らせ</h2>
  <p>{{userName}} 様</p>
  <p>ご利用ありがとうございます。以下のポイントが付与されました。</p>
  <div style="background: #e8f5e9; padding: 20px; border-radius: 8px; text-align: center; margin: 20px 0;">
    <p style="font-size: 36px; font-weight: bold; color: #2e7d32; margin: 0;">+{{earnedPoints}} pt</p>
    <p style="color: #666; margin: 5px 0 0;">{{reason}}</p>
  </div>
  <table style="width: 100%; border-collapse: collapse; margin: 20px 0;">
    <tr style="background: #f5f5f5;">
      <td style="padding: 10px; border: 1px solid #ddd;">現在の保有ポイント</td>
      <td style="padding: 10px; border: 1px solid #ddd; font-weight: bold;">{{totalPoints}} pt</td>
    </tr>
    <tr>
      <td style="padding: 10px; border: 1px solid #ddd;">有効期限</td>
      <td style="padding: 10px; border: 1px solid #ddd;">{{expiresAt}}</td>
    </tr>
  </table>
  <hr style="border: none; border-top: 1px solid #eee; margin: 30px 0;">
  <p style="color: #999; font-size: 12px;">SkiShop カスタマーサポート</p>
</body>
</html>',
 '{{userName}} 様

ご利用ありがとうございます。以下のポイントが付与されました。

付与ポイント: +{{earnedPoints}} pt
理由: {{reason}}
現在の保有ポイント: {{totalPoints}} pt
有効期限: {{expiresAt}}

SkiShop カスタマーサポート',
 'TRANSACTIONAL',
 '["userName", "earnedPoints", "reason", "totalPoints", "expiresAt"]'::jsonb,
 true,
 '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09'),

-- 5. クーポン発行通知
('tmpl-coupon-issued', 'coupon-issued',
 '【SkiShop】お得なクーポンをプレゼント！',
 '<!DOCTYPE html>
<html lang="ja">
<head><meta charset="UTF-8"></head>
<body style="font-family: sans-serif; color: #333; max-width: 600px; margin: 0 auto;">
  <h2 style="color: #ff6f00;">クーポン発行のお知らせ</h2>
  <p>{{userName}} 様</p>
  <p>以下のクーポンが発行されました。ぜひご利用ください！</p>
  <div style="background: #fff3e0; padding: 20px; border: 2px dashed #ff9800; border-radius: 8px; text-align: center; margin: 20px 0;">
    <p style="font-size: 28px; font-weight: bold; color: #e65100; letter-spacing: 4px; margin: 0;">{{couponCode}}</p>
    <p style="color: #666; margin: 10px 0 0;">{{couponDescription}}</p>
  </div>
  <table style="width: 100%; border-collapse: collapse; margin: 20px 0;">
    <tr style="background: #f5f5f5;">
      <td style="padding: 10px; border: 1px solid #ddd;">割引内容</td>
      <td style="padding: 10px; border: 1px solid #ddd;">{{discountDescription}}</td>
    </tr>
    <tr>
      <td style="padding: 10px; border: 1px solid #ddd;">最低注文金額</td>
      <td style="padding: 10px; border: 1px solid #ddd;">¥{{minOrderAmount}}</td>
    </tr>
    <tr style="background: #f5f5f5;">
      <td style="padding: 10px; border: 1px solid #ddd;">有効期限</td>
      <td style="padding: 10px; border: 1px solid #ddd;">{{validUntil}}</td>
    </tr>
  </table>
  <p style="text-align: center; margin: 30px 0;">
    <a href="{{shopUrl}}" style="background: #ff6f00; color: #fff; padding: 12px 30px; text-decoration: none; border-radius: 4px;">ショップを見る</a>
  </p>
  <hr style="border: none; border-top: 1px solid #eee; margin: 30px 0;">
  <p style="color: #999; font-size: 12px;">SkiShop カスタマーサポート</p>
</body>
</html>',
 '{{userName}} 様

以下のクーポンが発行されました。ぜひご利用ください！

クーポンコード: {{couponCode}}
{{couponDescription}}

割引内容: {{discountDescription}}
最低注文金額: ¥{{minOrderAmount}}
有効期限: {{validUntil}}

ショップはこちら: {{shopUrl}}

SkiShop カスタマーサポート',
 'MARKETING',
 '["userName", "couponCode", "couponDescription", "discountDescription", "minOrderAmount", "validUntil", "shopUrl"]'::jsonb,
 true,
 '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09'),

-- 6. レビュー承認通知
('tmpl-review-approved', 'review-approved',
 '【SkiShop】レビューが承認されました',
 '<!DOCTYPE html>
<html lang="ja">
<head><meta charset="UTF-8"></head>
<body style="font-family: sans-serif; color: #333; max-width: 600px; margin: 0 auto;">
  <h2 style="color: #1a73e8;">レビュー承認のお知らせ</h2>
  <p>{{userName}} 様</p>
  <p>ご投稿いただいたレビューが承認され、公開されました。</p>
  <div style="background: #f5f5f5; padding: 15px; border-radius: 8px; margin: 20px 0;">
    <p style="font-weight: bold; margin: 0 0 5px;">{{productName}}</p>
    <p style="color: #ff9800; margin: 0;">★ {{rating}}</p>
    <p style="margin: 10px 0 0; font-style: italic;">"{{reviewTitle}}"</p>
  </div>
  <p>レビューの投稿ありがとうございます。他のお客様の参考になります。</p>
  <p style="text-align: center; margin: 30px 0;">
    <a href="{{reviewUrl}}" style="background: #1a73e8; color: #fff; padding: 12px 30px; text-decoration: none; border-radius: 4px;">レビューを見る</a>
  </p>
  <hr style="border: none; border-top: 1px solid #eee; margin: 30px 0;">
  <p style="color: #999; font-size: 12px;">SkiShop カスタマーサポート</p>
</body>
</html>',
 '{{userName}} 様

ご投稿いただいたレビューが承認され、公開されました。

商品名: {{productName}}
評価: ★ {{rating}}
タイトル: "{{reviewTitle}}"

レビューの投稿ありがとうございます。他のお客様の参考になります。

レビューを見る: {{reviewUrl}}

SkiShop カスタマーサポート',
 'TRANSACTIONAL',
 '["userName", "productName", "rating", "reviewTitle", "reviewUrl"]'::jsonb,
 true,
 '2026-01-01 00:00:00+09', '2026-01-01 00:00:00+09')

ON CONFLICT (id) DO NOTHING;

COMMIT;

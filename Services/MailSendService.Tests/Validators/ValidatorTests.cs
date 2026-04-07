using Xunit;
using FluentValidation.TestHelper;
using MailSendService.DTOs.Requests;
using MailSendService.Validators;

namespace MailSendService.Tests.Validators;

/// <summary>
/// <see cref="TestMailRequestValidator"/> の単体テスト。
/// テストメールリクエストのメールアドレス・テンプレート名バリデーションを検証する。
/// </summary>
[Trait("Category", "Unit")]
public class TestMailRequestValidatorTests
{
    private readonly TestMailRequestValidator _validator = new();

    /// <summary>
    /// 有効なメールアドレスとテンプレート名を含むリクエストの場合に、バリデーションエラーが発生しないことを検証する。
    /// </summary>
    [Fact]
    public async Task Should_Pass_When_ValidRequest()
    {
        // Arrange
        var request = new TestMailRequest("test@example.com", "welcome", null);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// メールアドレスが空文字の場合に、RecipientEmail フィールドのバリデーションエラーが発生することを検証する。
    /// </summary>
    [Fact]
    public async Task Should_Fail_When_EmailIsEmpty()
    {
        // Arrange
        var request = new TestMailRequest("", "welcome", null);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RecipientEmail);
    }

    /// <summary>
    /// 無効な形式のメールアドレスの場合に、RecipientEmail フィールドのバリデーションエラーが発生することを検証する。
    /// </summary>
    [Fact]
    public async Task Should_Fail_When_EmailIsInvalid()
    {
        // Arrange
        var request = new TestMailRequest("invalid-email", "welcome", null);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RecipientEmail);
    }

    /// <summary>
    /// テンプレート名が空文字の場合に、TemplateName フィールドのバリデーションエラーが発生することを検証する。
    /// </summary>
    [Fact]
    public async Task Should_Fail_When_TemplateNameIsEmpty()
    {
        // Arrange
        var request = new TestMailRequest("test@example.com", "", null);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TemplateName);
    }

    /// <summary>
    /// テンプレート名に無効な文字（大文字・記号等）が含まれる場合に、TemplateName フィールドのバリデーションエラーが発生することを検証する。
    /// </summary>
    [Fact]
    public async Task Should_Fail_When_TemplateNameContainsInvalidChars()
    {
        // Arrange
        var request = new TestMailRequest("test@example.com", "INVALID_NAME!", null);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TemplateName);
    }
}

/// <summary>
/// <see cref="TemplateCreateRequestValidator"/> の単体テスト。
/// テンプレート作成リクエストの Name・Subject 等の必須フィールドバリデーションを検証する。
/// </summary>
[Trait("Category", "Unit")]
public class TemplateCreateRequestValidatorTests
{
    private readonly TemplateCreateRequestValidator _validator = new();

    /// <summary>
    /// 全フィールドが有効なテンプレート作成リクエストの場合に、バリデーションエラーが発生しないことを検証する。
    /// </summary>
    [Fact]
    public async Task Should_Pass_When_ValidRequest()
    {
        // Arrange
        var request = new TemplateCreateRequest(
            "template-name", "Subject", "<p>Body</p>", "Body", "TRANSACTIONAL", "{}");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// テンプレート名が空文字の場合に、Name フィールドのバリデーションエラーが発生することを検証する。
    /// </summary>
    [Fact]
    public async Task Should_Fail_When_NameIsEmpty()
    {
        // Arrange
        var request = new TemplateCreateRequest("", "Subject", "<p>Body</p>", "Body", "TRANSACTIONAL", "{}");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    /// <summary>
    /// Subject が空文字の場合に、Subject フィールドのバリデーションエラーが発生することを検証する。
    /// </summary>
    [Fact]
    public async Task Should_Fail_When_SubjectIsEmpty()
    {
        // Arrange
        var request = new TemplateCreateRequest("name", "", "<p>Body</p>", "Body", "TRANSACTIONAL", "{}");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Subject);
    }
}

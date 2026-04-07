using MailSendService.DTOs.Requests;
using MailSendService.DTOs.Responses;
using MailSendService.Exceptions;
using MailSendService.Models;
using MailSendService.Repositories.Interfaces;
using MailSendService.Services;
using MailSendService.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace MailSendService.Tests.Services;

/// <summary>
/// <see cref="TemplateService"/> の追加テスト。
/// UpdateAsync、DeleteAsync、GetByIdAsync の正常系・異常系をカバーする。
/// </summary>
[Trait("Category", "Unit")]
public class TemplateServiceAdditionalTests
{
    private readonly IMailTemplateRepository _templateRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<TemplateService> _logger;
    private readonly TemplateService _sut;

    public TemplateServiceAdditionalTests()
    {
        _templateRepository = Substitute.For<IMailTemplateRepository>();
        _cache = Substitute.For<IDistributedCache>();
        _logger = Substitute.For<ILogger<TemplateService>>();
        _sut = new TemplateService(_templateRepository, _cache, _logger);
    }

    // ─── UpdateAsync ────────────────────────────────────────────────

    /// <summary>
    /// Subject のみを指定した部分更新リクエストで、Subject が正しく更新されキャッシュが無効化されることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_UpdateSubject_When_SubjectProvided()
    {
        // Arrange
        var template = new MailTemplate
        {
            Id = "t-upd-1",
            Name = "welcome",
            Subject = "Old Subject",
            HtmlBody = "<p>Body</p>",
            TemplateType = "TRANSACTIONAL"
        };
        _templateRepository.FindByIdAsync("t-upd-1", Arg.Any<CancellationToken>())
            .Returns(template);

        var request = new TemplateUpdateRequest("New Subject", null, null, null, null);

        // Act
        var result = await _sut.UpdateAsync("t-upd-1", request, default);

        // Assert
        result.ShouldNotBeNull();
        result.Subject.ShouldBe("New Subject");
        template.Subject.ShouldBe("New Subject");
        await _templateRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync($"mail:template:{template.Name}", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 全フィールド（Subject, HtmlBody, TextBody, Variables, IsActive）を指定した更新リクエストで、すべてのフィールドが正しく更新されることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_UpdateMultipleFields_When_AllFieldsProvided()
    {
        // Arrange
        var template = new MailTemplate
        {
            Id = "t-upd-2",
            Name = "order-confirm",
            Subject = "Old Subject",
            HtmlBody = "<p>Old</p>",
            TextBody = "Old text",
            Variables = "{}",
            IsActive = true,
            TemplateType = "TRANSACTIONAL"
        };
        _templateRepository.FindByIdAsync("t-upd-2", Arg.Any<CancellationToken>())
            .Returns(template);

        var request = new TemplateUpdateRequest(
            "New Subject",
            "<p>New HTML</p>",
            "New text",
            """{"key":"value"}""",
            false);

        // Act
        var result = await _sut.UpdateAsync("t-upd-2", request, default);

        // Assert
        result.ShouldNotBeNull();
        result.Subject.ShouldBe("New Subject");
        result.HtmlBody.ShouldBe("<p>New HTML</p>");
        result.TextBody.ShouldBe("New text");
        result.Variables.ShouldBe("""{"key":"value"}""");
        result.IsActive.ShouldBeFalse();
    }

    /// <summary>
    /// 全フィールドが null の部分更新リクエストで、既存の値が変更されないことを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_NotUpdateNullFields_When_PartialUpdateRequested()
    {
        // Arrange
        var template = new MailTemplate
        {
            Id = "t-upd-3",
            Name = "partial",
            Subject = "Keep This",
            HtmlBody = "<p>Keep</p>",
            TextBody = "Keep text",
            Variables = "{}",
            IsActive = true,
            TemplateType = "TRANSACTIONAL"
        };
        _templateRepository.FindByIdAsync("t-upd-3", Arg.Any<CancellationToken>())
            .Returns(template);

        var request = new TemplateUpdateRequest(null, null, null, null, null);

        // Act
        var result = await _sut.UpdateAsync("t-upd-3", request, default);

        // Assert
        result.Subject.ShouldBe("Keep This");
        result.HtmlBody.ShouldBe("<p>Keep</p>");
        result.TextBody.ShouldBe("Keep text");
        result.IsActive.ShouldBeTrue();
    }

    /// <summary>
    /// 存在しないテンプレート ID で更新を試行した場合に、<see cref="TemplateNotFoundException"/> がスローされることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowTemplateNotFound_When_UpdateNonExistentTemplate()
    {
        // Arrange
        _templateRepository.FindByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((MailTemplate?)null);

        var request = new TemplateUpdateRequest("Subject", null, null, null, null);

        // Act & Assert
        var act = async () => await _sut.UpdateAsync("nonexistent", request, default);
        var ex = await Should.ThrowAsync<TemplateNotFoundException>(act);
        ex.Message.ShouldContain("nonexistent");
    }

    // ─── DeleteAsync ────────────────────────────────────────────────

    /// <summary>
    /// 削除操作時にテンプレートが論理削除（IsActive=false）され、該当テンプレートのキャッシュが無効化されることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_DeactivateAndInvalidateCache_When_DeleteCalled()
    {
        // Arrange
        var template = new MailTemplate
        {
            Id = "t-del-1",
            Name = "old-template",
            IsActive = true,
            Subject = "Subject",
            TemplateType = "TRANSACTIONAL"
        };
        _templateRepository.FindByIdAsync("t-del-1", Arg.Any<CancellationToken>())
            .Returns(template);

        // Act
        await _sut.DeleteAsync("t-del-1", default);

        // Assert
        template.IsActive.ShouldBeFalse();
        await _templateRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync("mail:template:old-template", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 存在しないテンプレート ID で削除を試行した場合に、<see cref="TemplateNotFoundException"/> がスローされることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowTemplateNotFound_When_DeleteNonExistentTemplate()
    {
        // Arrange
        _templateRepository.FindByIdAsync("bad-id", Arg.Any<CancellationToken>())
            .Returns((MailTemplate?)null);

        // Act & Assert
        var act = async () => await _sut.DeleteAsync("bad-id", default);
        var ex = await Should.ThrowAsync<TemplateNotFoundException>(act);
        ex.Message.ShouldContain("bad-id");
    }

    // ─── GetByIdAsync ───────────────────────────────────────────────

    /// <summary>
    /// 有効なテンプレート ID で取得を実行した場合に、全プロパティが正しく返却されることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnTemplate_When_GetByIdWithValidId()
    {
        // Arrange
        var template = new MailTemplate
        {
            Id = "t-get-1",
            Name = "get-test",
            Subject = "Test Subject",
            HtmlBody = "<p>Test</p>",
            TextBody = "Test",
            TemplateType = "TRANSACTIONAL",
            Variables = "{}",
            IsActive = true
        };
        _templateRepository.FindByIdAsync("t-get-1", Arg.Any<CancellationToken>())
            .Returns(template);

        // Act
        var result = await _sut.GetByIdAsync("t-get-1", default);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("t-get-1");
        result.Name.ShouldBe("get-test");
        result.Subject.ShouldBe("Test Subject");
        result.HtmlBody.ShouldBe("<p>Test</p>");
        result.IsActive.ShouldBeTrue();
    }

    /// <summary>
    /// 存在しないテンプレート ID で取得を試行した場合に、<see cref="TemplateNotFoundException"/> がスローされることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowTemplateNotFound_When_GetByIdWithInvalidId()
    {
        // Arrange
        _templateRepository.FindByIdAsync("missing-id", Arg.Any<CancellationToken>())
            .Returns((MailTemplate?)null);

        // Act & Assert
        var act = async () => await _sut.GetByIdAsync("missing-id", default);
        var ex = await Should.ThrowAsync<TemplateNotFoundException>(act);
        ex.Message.ShouldContain("missing-id");
    }

    // ─── CancellationToken ──────────────────────────────────────────

    /// <summary>
    /// UpdateAsync の CancellationToken がキャンセル済みの場合に、<see cref="OperationCanceledException"/> がスローされることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowOperationCanceled_When_UpdateTokenCanceled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        _templateRepository.FindByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var request = new TemplateUpdateRequest("Subject", null, null, null, null);

        // Act & Assert
        var act = async () => await _sut.UpdateAsync("t-1", request, cts.Token);
        await Should.ThrowAsync<OperationCanceledException>(act);
    }

    /// <summary>
    /// GetByIdAsync の CancellationToken がキャンセル済みの場合に、<see cref="OperationCanceledException"/> がスローされることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowOperationCanceled_When_GetByIdTokenCanceled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        _templateRepository.FindByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        var act = async () => await _sut.GetByIdAsync("t-1", cts.Token);
        await Should.ThrowAsync<OperationCanceledException>(act);
    }
}

using Xunit;
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
using Shouldly;

namespace MailSendService.Tests.Services;

/// <summary>
/// <see cref="TemplateService"/> の単体テスト。
/// テンプレートのレンダリング、作成、重複チェック、論理削除の基本的なビジネスロジックを検証する。
/// </summary>
[Trait("Category", "Unit")]
public class TemplateServiceTests
{
    private readonly IMailTemplateRepository _templateRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<TemplateService> _logger;
    private readonly TemplateService _sut;

    public TemplateServiceTests()
    {
        _templateRepository = Substitute.For<IMailTemplateRepository>();
        _cache = Substitute.For<IDistributedCache>();
        _logger = Substitute.For<ILogger<TemplateService>>();
        _sut = new TemplateService(_templateRepository, _cache, _logger);
    }

    /// <summary>
    /// テンプレートが存在する場合に、変数プレースホルダー（{{name}}）が正しく置換されたレンダリング結果が返ることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_RenderTemplate_When_TemplateExists()
    {
        // Arrange
        var template = new MailTemplate
        {
            Name = "welcome",
            Subject = "Welcome {{name}}",
            HtmlBody = "<p>Hello {{name}}</p>",
            TextBody = "Hello {{name}}",
            Variables = "{}",
            TemplateType = "TRANSACTIONAL"
        };
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);
        _templateRepository.FindActiveByNameAsync("welcome", Arg.Any<CancellationToken>())
            .Returns(template);

        var variables = new Dictionary<string, object> { ["name"] = "John" };

        // Act
        var result = await _sut.RenderAsync("welcome", variables);

        // Assert
        result.Subject.ShouldBe("Welcome John");
        result.HtmlBody.ShouldBe("<p>Hello John</p>");
        result.PlainTextBody.ShouldBe("Hello John");
    }

    /// <summary>
    /// 存在しないテンプレート名でレンダリングを試行した場合に、<see cref="TemplateNotFoundException"/> がスローされることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_ThrowTemplateNotFound_When_TemplateDoesNotExist()
    {
        // Arrange
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);
        _templateRepository.FindActiveByNameAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((MailTemplate?)null);

        // Act & Assert
        var act = async () => await _sut.RenderAsync("nonexistent", new Dictionary<string, object>());
        var ex = await Should.ThrowAsync<TemplateNotFoundException>(act);
        ex.Message.ShouldContain("nonexistent");
    }

    /// <summary>
    /// 有効な作成リクエストを受信した場合に、テンプレートが正常に作成されリポジトリに保存されることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_CreateTemplate_When_ValidRequest()
    {
        // Arrange
        _templateRepository.ExistsByNameAsync("new-template", Arg.Any<CancellationToken>())
            .Returns(false);

        var request = new TemplateCreateRequest(
            "new-template", "Subject", "<p>Body</p>", "Body", "TRANSACTIONAL", "{}");

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("new-template");
        await _templateRepository.Received(1).AddAsync(Arg.Any<MailTemplate>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 既存のテンプレート名と重複する名前で作成を試行した場合に、<see cref="DuplicateTemplateNameException"/> がスローされることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_ThrowException_When_DuplicateTemplateName()
    {
        // Arrange
        _templateRepository.ExistsByNameAsync("existing", Arg.Any<CancellationToken>())
            .Returns(true);

        var request = new TemplateCreateRequest(
            "existing", "Subject", "<p>Body</p>", "Body", "TRANSACTIONAL", "{}");

        // Act & Assert
        var act = async () => await _sut.CreateAsync(request);
        await Should.ThrowAsync<DuplicateTemplateNameException>(act);
    }

    /// <summary>
    /// 削除操作時にテンプレートの IsActive フラグが false に設定される（論理削除）ことを検証する。
    /// </summary>
    [Fact]
    public async Task Should_DeactivateTemplate_When_DeleteCalled()
    {
        // Arrange
        var template = new MailTemplate { Id = "t-1", Name = "old-template", IsActive = true };
        _templateRepository.FindByIdAsync("t-1", Arg.Any<CancellationToken>())
            .Returns(template);

        // Act
        await _sut.DeleteAsync("t-1");

        // Assert
        template.IsActive.ShouldBeFalse();
        await _templateRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 存在しないテンプレート ID で削除を試行した場合に、<see cref="TemplateNotFoundException"/> がスローされることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_ThrowNotFound_When_DeleteNonExistentTemplate()
    {
        // Arrange
        _templateRepository.FindByIdAsync("bad-id", Arg.Any<CancellationToken>())
            .Returns((MailTemplate?)null);

        // Act & Assert
        var act = async () => await _sut.DeleteAsync("bad-id");
        await Should.ThrowAsync<TemplateNotFoundException>(act);
    }
}

using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Core.Behaviors;
using Xunit;

namespace RealmEngine.Core.Tests.Behaviors;

public record TestPipelineRequest : IRequest<string>;

[Trait("Category", "Behaviors")]
public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_NoValidators_CallsNextAndReturnsResult()
    {
        var behavior = new ValidationBehavior<TestPipelineRequest, string>(
            Enumerable.Empty<IValidator<TestPipelineRequest>>());

        var result = await behavior.Handle(
            new TestPipelineRequest(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_PassingValidator_CallsNextAndReturnsResult()
    {
        var validator = new InlineValidator<TestPipelineRequest>();
        var behavior = new ValidationBehavior<TestPipelineRequest, string>([validator]);

        var result = await behavior.Handle(
            new TestPipelineRequest(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_FailingValidator_ThrowsValidationException()
    {
        var validator = new InlineValidator<TestPipelineRequest>();
        validator.RuleFor(x => x).Must(_ => false).WithMessage("Always fails");
        var behavior = new ValidationBehavior<TestPipelineRequest, string>([validator]);

        var act = () => behavior.Handle(
            new TestPipelineRequest(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Always fails*");
    }

    [Fact]
    public async Task Handle_ValidatorWithMultipleRules_CollectsAllErrors()
    {
        // A single validator with two failing rules — verifies all failures are aggregated
        var validator = new InlineValidator<TestPipelineRequest>();
        validator.RuleFor(x => x).Must(_ => false).WithMessage("Error one");
        validator.RuleFor(x => x).Must(_ => false).WithMessage("Error two");
        var behavior = new ValidationBehavior<TestPipelineRequest, string>([validator]);

        var act = () => behavior.Handle(
            new TestPipelineRequest(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().HaveCount(2)
            .And.Satisfy(
                e => e.ErrorMessage == "Error one",
                e => e.ErrorMessage == "Error two");
    }

    [Fact]
    public async Task Handle_FailingValidator_DoesNotCallNext()
    {
        var validator = new InlineValidator<TestPipelineRequest>();
        validator.RuleFor(x => x).Must(_ => false).WithMessage("fails");
        var behavior = new ValidationBehavior<TestPipelineRequest, string>([validator]);
        var nextCalled = false;

        var act = () => behavior.Handle(
            new TestPipelineRequest(),
            () => { nextCalled = true; return Task.FromResult("ok"); },
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        nextCalled.Should().BeFalse();
    }
}

[Trait("Category", "Behaviors")]
public class LoggingBehaviorTests
{
    private static Mock<ILogger<LoggingBehavior<TestPipelineRequest, string>>> CreateLogger() =>
        new();

    private static void VerifyLog(
        Mock<ILogger<LoggingBehavior<TestPipelineRequest, string>>> mockLogger,
        LogLevel level,
        string containsText,
        Times times)
    {
        mockLogger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(containsText)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    [Fact]
    public async Task Handle_SuccessfulRequest_LogsExecutingAndCompleted()
    {
        var mockLogger = CreateLogger();
        var behavior = new LoggingBehavior<TestPipelineRequest, string>(mockLogger.Object);

        await behavior.Handle(
            new TestPipelineRequest(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        VerifyLog(mockLogger, LogLevel.Information, "Executing", Times.Once());
        VerifyLog(mockLogger, LogLevel.Information, "Completed", Times.Once());
    }

    [Fact]
    public async Task Handle_SuccessfulRequest_ReturnsResponse()
    {
        var behavior = new LoggingBehavior<TestPipelineRequest, string>(
            Mock.Of<ILogger<LoggingBehavior<TestPipelineRequest, string>>>());

        var result = await behavior.Handle(
            new TestPipelineRequest(),
            () => Task.FromResult("response"),
            CancellationToken.None);

        result.Should().Be("response");
    }

    [Fact]
    public async Task Handle_ThrowingNext_LogsFailedAndRethrows()
    {
        var mockLogger = CreateLogger();
        var behavior = new LoggingBehavior<TestPipelineRequest, string>(mockLogger.Object);
        var exception = new InvalidOperationException("boom");

        var act = () => behavior.Handle(
            new TestPipelineRequest(),
            () => throw exception,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        VerifyLog(mockLogger, LogLevel.Information, "Executing", Times.Once());
        VerifyLog(mockLogger, LogLevel.Error, "Failed", Times.Once());
    }

    [Fact]
    public async Task Handle_ThrowingNext_DoesNotLogCompleted()
    {
        var mockLogger = CreateLogger();
        var behavior = new LoggingBehavior<TestPipelineRequest, string>(mockLogger.Object);

        var act = () => behavior.Handle(
            new TestPipelineRequest(),
            () => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        VerifyLog(mockLogger, LogLevel.Information, "Completed", Times.Never());
    }
}

[Trait("Category", "Behaviors")]
public class PerformanceBehaviorTests
{
    private static Mock<ILogger<PerformanceBehavior<TestPipelineRequest, string>>> CreateLogger() =>
        new();

    private static void VerifyWarningLog(
        Mock<ILogger<PerformanceBehavior<TestPipelineRequest, string>>> mockLogger,
        Times times)
    {
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Slow request")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    [Fact]
    public async Task Handle_FastRequest_DoesNotLogWarning()
    {
        var mockLogger = CreateLogger();
        var behavior = new PerformanceBehavior<TestPipelineRequest, string>(mockLogger.Object);

        await behavior.Handle(
            new TestPipelineRequest(),
            () => Task.FromResult("fast"),
            CancellationToken.None);

        VerifyWarningLog(mockLogger, Times.Never());
    }

    [Fact]
    public async Task Handle_FastRequest_ReturnsResponse()
    {
        var behavior = new PerformanceBehavior<TestPipelineRequest, string>(
            Mock.Of<ILogger<PerformanceBehavior<TestPipelineRequest, string>>>());

        var result = await behavior.Handle(
            new TestPipelineRequest(),
            () => Task.FromResult("result"),
            CancellationToken.None);

        result.Should().Be("result");
    }

    [Fact]
    public async Task Handle_SlowRequest_LogsWarning()
    {
        var mockLogger = CreateLogger();
        var behavior = new PerformanceBehavior<TestPipelineRequest, string>(mockLogger.Object);

        await behavior.Handle(
            new TestPipelineRequest(),
            async () =>
            {
                await Task.Delay(510);
                return "slow";
            },
            CancellationToken.None);

        VerifyWarningLog(mockLogger, Times.Once());
    }
}

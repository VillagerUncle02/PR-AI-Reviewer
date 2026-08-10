using PrReviewSubmit.Domain;
using PrReviewSubmit.Validation;

namespace PrReviewSubmit.Tests.Unit;

public class ValidatorTests
{
    private static ReviewSubmitRequest CreateRequest(
        string? owner = "octo",
        string? repo = "hello-world",
        int pullNumber = 42,
        string? body = "整体审查结论",
        IReadOnlyList<ReviewComment>? comments = null)
        => new()
        {
            Owner = owner!,
            Repo = repo!,
            PullNumber = pullNumber,
            Body = body!,
            Comments = comments ?? [],
        };

    [Fact]
    public void ValidRequest_Passes()
    {
        var (ok, error) = ReviewPayloadValidator.Validate(CreateRequest());

        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void EmptyCommentsList_IsValid()
    {
        var (ok, _) = ReviewPayloadValidator.Validate(CreateRequest(comments: []));

        Assert.True(ok);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void OwnerWhitespaceOrMissing_IsRejected(string? owner)
    {
        var (ok, error) = ReviewPayloadValidator.Validate(CreateRequest(owner: owner));

        Assert.False(ok);
        Assert.Contains("owner", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \u3000")]
    public void RepoWhitespaceOrMissing_IsRejected(string? repo)
    {
        var (ok, error) = ReviewPayloadValidator.Validate(CreateRequest(repo: repo));

        Assert.False(ok);
        Assert.Contains("repo", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PullNumberLessThanOne_IsRejected(int pullNumber)
    {
        var (ok, error) = ReviewPayloadValidator.Validate(CreateRequest(pullNumber: pullNumber));

        Assert.False(ok);
        Assert.Contains("pullNumber", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BodyWhitespaceOrMissing_IsRejected(string? body)
    {
        var (ok, error) = ReviewPayloadValidator.Validate(CreateRequest(body: body));

        Assert.False(ok);
        Assert.Contains("body", error);
    }

    [Fact]
    public void NullComments_IsRejected()
    {
        var request = new ReviewSubmitRequest
        {
            Owner = "octo",
            Repo = "hello-world",
            PullNumber = 42,
            Body = "整体审查结论",
            Comments = null!,
        };

        var (ok, error) = ReviewPayloadValidator.Validate(request);

        Assert.False(ok);
        Assert.Contains("comments", error);
    }

    [Fact]
    public void NullRequest_IsRejected()
    {
        var (ok, error) = ReviewPayloadValidator.Validate(null);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void CommentMissingPath_IsRejected()
    {
        var request = CreateRequest(comments:
        [
            new ReviewComment { Path = "", Line = 1, Side = "RIGHT", Body = "评论" },
        ]);

        var (ok, error) = ReviewPayloadValidator.Validate(request);

        Assert.False(ok);
        Assert.Contains("path", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CommentInvalidLine_IsRejected(int line)
    {
        var request = CreateRequest(comments:
        [
            new ReviewComment { Path = "README.md", Line = line, Side = "RIGHT", Body = "评论" },
        ]);

        var (ok, error) = ReviewPayloadValidator.Validate(request);

        Assert.False(ok);
        Assert.Contains("line", error);
    }

    [Theory]
    [InlineData("right")]
    [InlineData("LEFT ")]
    [InlineData("")]
    public void CommentInvalidSide_IsRejected(string side)
    {
        var request = CreateRequest(comments:
        [
            new ReviewComment { Path = "README.md", Line = 1, Side = side, Body = "评论" },
        ]);

        var (ok, error) = ReviewPayloadValidator.Validate(request);

        Assert.False(ok);
        Assert.Contains("side", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void CommentWhitespaceBody_IsRejected(string? body)
    {
        var request = CreateRequest(comments:
        [
            new ReviewComment { Path = "README.md", Line = 1, Side = "RIGHT", Body = body! },
        ]);

        var (ok, error) = ReviewPayloadValidator.Validate(request);

        Assert.False(ok);
        Assert.Contains("body", error);
    }

    [Fact]
    public void ValidationDoesNotMutateOriginalContent()
    {
        var body = "  带首尾空格的整体结论  ";
        var commentBody = "  \u3000带首尾空格的评论  ";
        var request = CreateRequest(
            body: body,
            comments:
            [
                new ReviewComment { Path = "README.md", Line = 3, Side = "LEFT", Body = commentBody },
            ]);

        var (ok, _) = ReviewPayloadValidator.Validate(request);

        Assert.True(ok);
        Assert.Equal(body, request.Body);
        Assert.Equal(commentBody, request.Comments[0].Body);
    }
}

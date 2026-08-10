using System.Text.Json.Serialization;
using PrReviewSubmit.Domain;

namespace PrReviewSubmit.Json;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ReviewSubmitRequest))]
[JsonSerializable(typeof(ReviewComment))]
[JsonSerializable(typeof(IReadOnlyList<ReviewComment>))]
[JsonSerializable(typeof(ReviewSubmitResult))]
[JsonSerializable(typeof(GitHubErrorDetails))]
[JsonSerializable(typeof(GitHubApiError))]
[JsonSerializable(typeof(ReviewSubmitErrorCode))]
public partial class ToolJsonContext : JsonSerializerContext;

using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using PrReviewSubmit.Configuration;
using PrReviewSubmit.Domain;

namespace PrReviewSubmit.GitHub;

/// <summary>
/// GitHub App 认证客户端：PEM → RS256 JWT → POST /app/installations/{id}/access_tokens。
/// 私钥每次调用重新读取（FR-012/CHK137），令牌按需生成、不缓存。
/// </summary>
public sealed class GitHubAppAuthClient
{
    private readonly HttpClient _http;
    private readonly GitHubAppOptions _options;

    public GitHubAppAuthClient(HttpClient httpClient, GitHubAppOptions options)
    {
        _http = httpClient;
        _options = options;
    }

    public async Task<string> GetInstallationTokenAsync(string repo, CancellationToken cancellationToken)
    {
        var jwt = CreateJwt();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"app/installations/{_options.InstallationId}/access_tokens");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation(GitHubAppOptions.ApiVersionHeader, GitHubAppOptions.ApiVersion);
        request.Content = JsonContent.Create(new { repositories = new[] { repo } });

        using var response = await SendAsync(request, GitHubRequestStage.Auth, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw GitHubErrorMapper.ToException(response, body, GitHubRequestStage.Auth);

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new GitHubRequestException(ReviewSubmitErrorCode.UNEXPECTED_ERROR, "令牌交换响应不是 JSON 对象", 201);
            if (!root.TryGetProperty("token", out var tokenElement) ||
                tokenElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrEmpty(tokenElement.GetString()))
            {
                throw new GitHubRequestException(ReviewSubmitErrorCode.UNEXPECTED_ERROR, "令牌交换响应缺少 token 字段或类型异常", 201);
            }

            if (!root.TryGetProperty("expires_at", out var expiresElement) ||
                expiresElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrEmpty(expiresElement.GetString()))
            {
                throw new GitHubRequestException(ReviewSubmitErrorCode.UNEXPECTED_ERROR, "令牌交换响应缺少 expires_at 字段或类型异常", 201);
            }

            return tokenElement.GetString()!;
        }
        catch (GitHubRequestException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw new GitHubRequestException(ReviewSubmitErrorCode.UNEXPECTED_ERROR, "令牌交换响应不是有效 JSON", 201);
        }
    }

    /// <summary>生成 RS256 JWT：iss=App ID、iat=now-60s、exp≤now+10min。</summary>
    public string CreateJwt()
    {
        using var rsa = ReadPrivateKey();
        var credentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        var now = DateTimeOffset.UtcNow;
        return new JwtSecurityTokenHandler().CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Issuer = _options.AppId.ToString(),
            IssuedAt = now.AddSeconds(-60).UtcDateTime,
            Expires = now.AddMinutes(10).UtcDateTime,
            SigningCredentials = credentials,
        });
    }

    private RSA ReadPrivateKey()
    {
        try
        {
            var pem = File.ReadAllText(_options.PrivateKeyPath);
            var rsa = RSA.Create();
            try
            {
                rsa.ImportFromPem(pem);
                return rsa;
            }
            catch
            {
                rsa.Dispose();
                throw;
            }
        }
        catch (Exception ex)
        {
            throw new GitHubRequestException(
                ReviewSubmitErrorCode.CREDENTIALS_INVALID,
                "私钥文件读取或解析失败（可能被删除、替换或权限不足），请检查 private-key 目录与配置",
                innerException: ex);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        GitHubRequestStage stage,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw GitHubErrorMapper.ToNetworkException(stage);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw GitHubErrorMapper.ToNetworkException(stage);
        }
    }
}

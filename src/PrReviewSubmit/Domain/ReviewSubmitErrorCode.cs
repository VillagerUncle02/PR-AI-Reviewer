namespace PrReviewSubmit.Domain;

/// <summary>失败原因枚举（tool-contract.md 错误码表的机器可读形式）。</summary>
public enum ReviewSubmitErrorCode
{
    INVALID_PAYLOAD,
    CREDENTIALS_INVALID,
    APP_NOT_INSTALLED,
    TARGET_NOT_FOUND,
    PR_NOT_OPEN,
    REVIEW_UNPROCESSABLE,
    RATE_LIMITED,
    NETWORK_ERROR,
    UNEXPECTED_ERROR,
}

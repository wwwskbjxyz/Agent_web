namespace SProtectPlatform.Api.Options;

public sealed class ForwardingOptions
{
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Shared secret used to sign outbound requests to author endpoints.
    /// </summary>
    public string SharedSecret { get; set; } = string.Empty;

    /// <summary>
    /// Maximum allowed clock skew (in seconds) when validating request signatures.
    /// </summary>
    public int SignatureClockSkewSeconds { get; set; } = 300;
}

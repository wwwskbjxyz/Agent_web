using System.Collections.Generic;

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

    /// <summary>
    /// Paths that should be forwarded without applying request signatures.
    /// Supports exact matches and <c>/*</c> suffix for prefix-based matching.
    /// </summary>
    public List<string> UnsignedPaths { get; set; } = new();
}
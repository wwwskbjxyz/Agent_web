namespace SProtectPlatform.Api.Options;

public sealed class HttpsOptions
{
    public bool Enabled { get; set; }

    public int? HttpPort { get; set; }

    public int? HttpsPort { get; set; }

    public string? CertificatePath { get; set; }

    public string? CertificatePassword { get; set; }
}

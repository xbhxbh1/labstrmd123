namespace StudentCabinetMVVM.Services;

public static class ApiConfig
{
    /// <summary>Адреса тестового сервера (stub/StudentCabinet.StubApi), що виконує автентифікацію, лише HTTPS.</summary>
    public const string AuthBaseUrl = "https://localhost:5443/";

    /// <summary>
    /// SHA-256 відбиток сертифіката сервера (DER), закріплений у застосунку.
    /// Це відбиток файла stub/StudentCabinet.StubApi/certs/stub-localhost.cer.
    /// </summary>
    public const string PinnedCertificateSha256 =
        "103369995DF050FBE5C37D65716DC360F0D9925F7D8D5B790A81E98924E344D3";
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using StudentCabinetMVVM.Models;

namespace StudentCabinetMVVM.Services;

public class HttpAuthApi : IAuthApi
{
    private readonly HttpClient _http;

    public HttpAuthApi(ILogger<HttpAuthApi> logger)
    {
        // Усі запити автентифікації йдуть через обробник, що перевіряє закріплений сертифікат
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, certificate, chain, sslPolicyErrors) =>
            {
                // Довіряємо лише сертифікату із закріпленим відбитком; ланцюжок довіри ОС не враховуємо,
                // бо тестовий сертифікат самопідписаний.
                if (CertificatePinValidator.IsPinned(certificate, ApiConfig.PinnedCertificateSha256))
                    return true;

                logger.LogError(
                    "Сертифікат сервера не збігається із закріпленим: очікувано {Expected}, отримано {Actual}",
                    CertificatePinValidator.Normalize(ApiConfig.PinnedCertificateSha256),
                    certificate is null ? "(немає сертифіката)" : CertificatePinValidator.ComputePin(certificate));
                return false;
            }
        };

        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(ApiConfig.AuthBaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task<string?> AuthenticateAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("api/auth/login", new { login, password }, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return null;

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        return body?.AccessToken;
    }

    public async Task<IReadOnlyList<UserInfo>> GetUsersAsync(string token, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<UserInfo>>(cancellationToken) ?? new List<UserInfo>();
    }

    private record LoginResponse(string AccessToken, DateTime ExpiresAt);
}

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace StudentCabinetMVVM.Services;

/// <summary>
/// Сертифікат-пінінг: застосунок довіряє не будь-якому дійсному сертифікату, а лише тому,
/// SHA-256 відбиток якого збігається із закріпленим значенням.
/// </summary>
public static class CertificatePinValidator
{
    public static string ComputePin(X509Certificate certificate) =>
        Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData()));

    public static bool IsPinned(X509Certificate? certificate, string expectedPin)
    {
        if (certificate is null)
            return false;

        var actual = Encoding.ASCII.GetBytes(ComputePin(certificate));
        var expected = Encoding.ASCII.GetBytes(Normalize(expectedPin));

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Приводить відбиток до верхнього регістру без роздільників («AA:BB» і «aabb» - одне й те саме).</summary>
    public static string Normalize(string pin) =>
        pin.Replace(":", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
}

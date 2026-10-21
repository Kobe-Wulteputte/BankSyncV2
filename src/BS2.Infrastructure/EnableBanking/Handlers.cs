using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BS2.Infrastructure.EnableBanking;

/// <summary>Signs every request with a short-lived RS256 JWT, as Enable Banking requires.</summary>
public class TokenHandler(IOptions<EnableBankingOptions> options) : DelegatingHandler
{
    private const string JwtAudience = "api.enablebanking.com";
    private const string JwtIssuer = "enablebanking.com";

    private readonly EnableBankingOptions _options = options.Value;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetAccessToken());
        return await base.SendAsync(request, cancellationToken);
    }

    private string GetAccessToken()
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(_options.ReadKeyPem());

        var signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };

        var now = DateTime.UtcNow;
        var jwt = new JwtSecurityToken(
            audience: JwtAudience,
            issuer: JwtIssuer,
            claims: [new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)],
            expires: now.AddMinutes(30),
            signingCredentials: signingCredentials);
        jwt.Header.Add("kid", _options.AppKid);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}

/// <summary>
/// Adds <c>psu-ip-address</c>, which some ASPSPs list in <c>required_psu_headers</c> (Argenta does,
/// Revolut does not). Falls back to the local IPv4; sends nothing rather than a bogus address.
/// </summary>
public class PsuHeaderHandler(IOptions<EnableBankingOptions> options) : DelegatingHandler
{
    private static readonly Lazy<string?> LocalAddress = new(ResolveLocalAddress);

    private readonly EnableBankingOptions _options = options.Value;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var address = !string.IsNullOrWhiteSpace(_options.PsuIpAddress) ? _options.PsuIpAddress : LocalAddress.Value;

        if (!string.IsNullOrWhiteSpace(address))
        {
            request.Headers.Remove("psu-ip-address");
            request.Headers.Add("psu-ip-address", address);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private static string? ResolveLocalAddress()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Select(unicast => unicast.Address)
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                ?.ToString();
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }
}

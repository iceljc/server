using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace GraphQL.Server.Samples.Jwt;

/// <summary>
/// Provides a method to create a signed token, and provides token validation parameters to validate those tokens.
/// </summary>
public class JwtHelper
{
    /// <summary>
    /// The shared instance for the application.
    /// </summary>
    public static JwtHelper Instance { get; set; } = null!;

    private readonly SecurityKey _securityKey;
    private readonly string _securityAlgorithm;
    private readonly SigningCredentials _signingCredentials;
    private readonly string _issuer = "http://localhost/Samples.Jwt";   // may be any arbitrary string
    private readonly string _audience = "Samples.Jwt.Audience";         // may be any arbitrary string
    private readonly TimeSpan _expiresIn = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance with the specified key and security key type.
    /// </summary>
    /// <param name="key">A password for symmetric algorithms; the exported key for asymmetric algorithms.</param>
    /// <param name="keyType">The type of key; a symmetric key, a public asymmetric key, or a private asymmetric key.</param>
    /// <remarks>
    /// When using passwords with symmetric algoritms, be sure to use a password with a high entropy,
    /// such as a pair of random GUIDs.
    /// The provided <see cref="CreateNewSymmetricKey"/> method will provide a password with 256-bit entropy.
    /// </remarks>
    public JwtHelper(string key, SecurityKeyType keyType)
    {
        // load the key
        switch (keyType)
        {
            case SecurityKeyType.SymmetricSecurityKey:
                (_securityKey, _securityAlgorithm) = CreateSymmetricSecurityKey(key);
                break;
            case SecurityKeyType.PublicKey:
                (_securityKey, _securityAlgorithm) = CreateAsymmetricSecurityKey(key, false);
                break;
            case SecurityKeyType.PrivateKey:
                (_securityKey, _securityAlgorithm) = CreateAsymmetricSecurityKey(key, true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(keyType));
        }

        // prepare the signing credentials
        _signingCredentials = new(_securityKey, _securityAlgorithm);

        // set the token validation parameters
        TokenValidationParameters =
            new TokenValidationParameters
            {
                // validate the issuer name on the token
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                // validate the audience name on the token
                ValidateAudience = true,
                ValidAudience = _audience,
                // validate the 'not before' timestamp, if it exists
                ValidateLifetime = true,
                // ensure the token has not expired
                RequireExpirationTime = true,
                // allow up to 6 seconds of clock skew (aka add 6 seconds to the expiration time)
                //   (because Azure might let the VM clock get 5 seconds off before forcing a re-sync to the host)
                ClockSkew = TimeSpan.FromSeconds(6),
                // ensure the digital signature exists and validate it
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = new[] { _securityKey },
                ValidAlgorithms = new[] { _securityAlgorithm },
            };
    }

    /// <summary>
    /// Creates a symmetric security key based on a password
    /// </summary>
    private static (SecurityKey SecurityKey, string SecurityAlgorithm) CreateSymmetricSecurityKey(string password)
    {
        // hash the password and use that to create a symmetric key for signing the JWT tokens
        var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
        var keyBytes = SHA256.HashData(passwordBytes);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        // return the key
        return (securityKey, SecurityAlgorithms.HmacSha256);
    }

    /// <summary>
    /// Creates an asymmetric ECDsa security key based on a previously generated key pair.
    /// </summary>
    /// <remarks>
    /// If loading a public key, new JWT tokens cannot be signed; only verification of JWT tokens is possible.
    /// </remarks>
    private static (SecurityKey SecurityKey, string SecurityAlgorithm) CreateAsymmetricSecurityKey(string key, bool isPrivateKey)
    {
        // interpret the key as base64
        var keyBytes = Convert.FromBase64String(key);
        // create a ECDsa key pair and import the key
        using var ecdsa = ECDsa.Create();
        if (isPrivateKey)
            ecdsa.ImportECPrivateKey(keyBytes, out int _);
        else
            ecdsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
        var securityKey = new ECDsaSecurityKey(ecdsa);
        // return the key
        return (securityKey, SecurityAlgorithms.EcdsaSha256);
    }

    /// <summary>
    /// Creates an asymmetric ECDsa security key pair.
    /// </summary>
    public static (string PublicKey, string PrivateKey) CreateNewAsymmetricKeyPair()
    {
        using var ecdsa = ECDsa.Create();
        var privateKey = Convert.ToBase64String(ecdsa.ExportECPrivateKey());
        var publicKey = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());
        return (publicKey, privateKey);
    }

    /// <summary>
    /// Creates a password for a symmetric security algorithm with a 256-bit entropy.
    /// </summary>
    public static string CreateNewSymmetricKey()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Returns the <see cref="TokenValidationParameters"/> used to authenticate JWT bearer tokens.
    /// </summary>
    public TokenValidationParameters TokenValidationParameters { get; }

    /// <summary>
    /// Creates a signed JWT token containing the specified <see cref="Claim"/>s.
    /// </summary>
    public (TimeSpan ExpiresIn, string Token) CreateSignedToken(params Claim[] claims)
    {
        var now = DateTime.UtcNow;

        // create the security token as follows:
        var token = new JwtSecurityToken(
            // issuer is an arbitrary string, typically the name of the web server that issued the token
            issuer: _issuer,
            // audience is an arbitrary string, typically representing valid recipients - typically 'Refresh' or 'Access' or a url for a subdomain
            audience: _audience,
            // include a list of claims
            claims: claims,
            // set the time this token becomes valid
            notBefore: now,
            // for access tokens, set a short timeout like 5 minutes, after which the access token will need to be refreshed
            //   (the access token can be refreshed before or after the expiration, as refreshing it uses the refresh token)
            // for refresh tokens, set a long timeout like 6 months, after which the refresh token will expire
            expires: now.Add(_expiresIn),
            // set the digital signature algorithm and key
            signingCredentials: _signingCredentials
        );

        // return the token
        return (_expiresIn, new JwtSecurityTokenHandler().WriteToken(token));
    }
}

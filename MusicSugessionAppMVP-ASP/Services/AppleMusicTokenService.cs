using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace MusicSugessionAppMVP_ASP.Services
{
    public class AppleMusicTokenService
    {
        private readonly string _teamId;
        private readonly string _keyId;
        private readonly string _privateKeyPath;

        private readonly object _lock = new();
        private string? _cachedToken;
        private DateTime _cachedTokenExpiryUtc;

        public AppleMusicTokenService(string teamId, string keyId, string privateKeyPath)
        {
            _teamId = teamId;
            _keyId = keyId;
            _privateKeyPath = privateKeyPath;
        }

        public string GenerateDeveloperToken()
        {
            lock (_lock)
            {
                // Reuse token until close to expiry to avoid re-reading the key file on every request.
                if (!string.IsNullOrWhiteSpace(_cachedToken) &&
                    DateTime.UtcNow < _cachedTokenExpiryUtc)
                {
                    return _cachedToken;
                }
            }

            // Read the .p8 private key
            var privateKeyText = File.ReadAllText(_privateKeyPath);

            // Convert PEM to ECDsa key
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(privateKeyText);

            var securityKey = new ECDsaSecurityKey(ecdsa)
            {
                KeyId = _keyId
            };

            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(20); // Apple recommends short-lived tokens

            var tokenHandler = new JwtSecurityTokenHandler();

            var token = new JwtSecurityToken(
                issuer: _teamId,              // <-- REQUIRED
                audience: "https://appleid.apple.com",
                claims: null,
                notBefore: now,
                expires: expires,
                signingCredentials: credentials
            );

            var jwt = tokenHandler.WriteToken(token);

            lock (_lock)
            {
                _cachedToken = jwt;
                // Refresh 60s early.
                _cachedTokenExpiryUtc = expires.AddSeconds(-60);
            }

            return jwt;
        }
    }
}

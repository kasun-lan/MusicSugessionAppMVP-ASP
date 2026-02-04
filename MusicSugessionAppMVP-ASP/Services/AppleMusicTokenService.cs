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

        public AppleMusicTokenService(string teamId, string keyId, string privateKeyPath)
        {
            _teamId = teamId;
            _keyId = keyId;
            _privateKeyPath = privateKeyPath;
        }

        public string GenerateDeveloperToken()
        {
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

            var tokenHandler = new JwtSecurityTokenHandler();

            var token = new JwtSecurityToken(
                issuer: _teamId,              // <-- REQUIRED
                audience: "https://appleid.apple.com",
                claims: null,
                notBefore: now,
                expires: now.AddMinutes(20),  // Apple recommends short-lived tokens
                signingCredentials: credentials
            );

            return tokenHandler.WriteToken(token);
        }
    }
}

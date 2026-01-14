using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MusicSugessionAppMVP_ASP.Models;

namespace MusicSugessionAppMVP_ASP.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public SmtpEmailService(EmailSettings settings)
        {
            _settings = settings;
        }

        public async Task SendPlaylistAsync(
            string email,
            IEnumerable<TrackInfo> tracks)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _settings.FromName,
                _settings.FromEmail));

            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = "Your Music Crate 🎧";

            var body = new BodyBuilder
            {
                HtmlBody = BuildHtml(tracks)
            };

            message.Body = body.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(
                _settings.Host,
                _settings.Port,
                SecureSocketOptions.StartTls);

            try
            {
                await client.AuthenticateAsync(
                    _settings.Username,
                    _settings.Password);

                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
            }
        }

        private static string BuildHtml(IEnumerable<TrackInfo> tracks)
        {
            var items = string.Join("",
                tracks.Select(t => $"""
            <tr>
                <td style="padding: 10px 0; border-bottom: 1px solid #2a2a2a;">
                    <div style="color: #ffffff; font-size: 15px; font-weight: 600;">
                        {t.ArtistName}
                    </div>
                    <div style="color: #9ca3af; font-size: 14px;">
                        {t.Name}
                    </div>
                </td>
            </tr>
        """));

            return $"""
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8" />
    <title>Your Music Crate</title>
</head>
<body style="
    margin: 0;
    padding: 0;
    background-color: #000000;
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
">

<table width="100%" cellpadding="0" cellspacing="0" style="background-color:#000000; padding: 24px 0;">
    <tr>
        <td align="center">

            <!-- Container -->
            <table width="100%" cellpadding="0" cellspacing="0"
                   style="
                       max-width: 420px;
                       background-color: #0a0a0a;
                       border-radius: 24px;
                       overflow: hidden;
                   ">

                <!-- Header -->
                <tr>
                    <td style="padding: 32px 24px 24px; text-align: center;">
                        <img src="https://YOUR_DOMAIN/images/Speed%20Crating%20logo%20NEW.png"
                             alt="Speed Crating"
                             width="160"
                             style="display: block; margin: 0 auto 20px;" />

                        <h1 style="
                            color: #ffffff;
                            font-size: 24px;
                            font-weight: 700;
                            margin: 0 0 8px;
                        ">
                            Your Music Crate
                        </h1>

                        <p style="
                            color: #9ca3af;
                            font-size: 14px;
                            margin: 0;
                        ">
                            Hand-picked. Fast. No filler.
                        </p>
                    </td>
                </tr>

                <!-- Playlist -->
                <tr>
                    <td style="padding: 0 24px 24px;">
                        <table width="100%" cellpadding="0" cellspacing="0">
                            {items}
                        </table>
                    </td>
                </tr>

                <!-- CTA -->
                <tr>
                    <td style="padding: 0 24px 32px; text-align: center;">
                        <a href="#"
                           style="
                               display: inline-block;
                               background-color: #B4FF00;
                               color: #000000;
                               text-decoration: none;
                               font-weight: 700;
                               padding: 14px 24px;
                               border-radius: 8px;
                               font-size: 14px;
                           ">
                            OPEN & LISTEN
                        </a>
                    </td>
                </tr>

                <!-- Footer -->
                <tr>
                    <td style="
                        padding: 16px 24px 24px;
                        text-align: center;
                        color: #6b7280;
                        font-size: 12px;
                    ">
                        Speed Crating — find hidden gems, faster.
                    </td>
                </tr>

            </table>

        </td>
    </tr>
</table>

</body>
</html>
""";
        }

    }
}

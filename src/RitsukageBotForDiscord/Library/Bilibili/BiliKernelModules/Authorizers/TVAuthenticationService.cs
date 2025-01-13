using System.Reflection;
using QRCoder;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Authorizers.TV.Core;
using Richasy.BiliKernel.Bili;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Http;
using OriginalAuthenticationService = Richasy.BiliKernel.Authorizers.TV.TVAuthenticationService;

namespace RitsukageBot.Library.Bilibili.BiliKernelModules.Authorizers
{
    /// <summary>
    ///     TV authentication service.
    /// </summary>
    /// <param name="biliHttpClient"></param>
    /// <param name="qrCodeResolver"></param>
    /// <param name="localCookiesResolver"></param>
    /// <param name="localTokenResolver"></param>
    /// <param name="basicAuthenticator"></param>
    public sealed class TvAuthenticationService(
        BiliHttpClient biliHttpClient,
        IQRCodeResolver qrCodeResolver,
        IBiliCookiesResolver localCookiesResolver,
        IBiliTokenResolver localTokenResolver,
        BiliAuthenticator basicAuthenticator) : IAuthenticationService
    {
        private readonly OriginalAuthenticationService _originalAuthenticationService = new(biliHttpClient,
            qrCodeResolver, localCookiesResolver, localTokenResolver, basicAuthenticator);

        private TVQRCode? _qrCode;
        private byte[]? _qrCodeImage;

        /// <summary>
        ///     Sign in.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task SignInAsync(AuthorizeExecutionSettings? settings = null,
            CancellationToken cancellationToken = default)
        {
            var client = GetClient();
            _qrCode = await GetQrCode(client, cancellationToken);
            if (string.IsNullOrEmpty(_qrCode.Url)) throw new("Cannot get the QR code URL.");

            var qrCodeGenerator = new QRCodeGenerator();
            var data = qrCodeGenerator.CreateQrCode(_qrCode.Url, QRCodeGenerator.ECCLevel.Q);
            var code = new PngByteQRCode(data);
            _qrCodeImage = code.GetGraphic(20);
        }

        /// <summary>
        ///     Sign out.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task SignOutAsync(CancellationToken cancellationToken = default)
        {
            return _originalAuthenticationService.SignOutAsync(cancellationToken);
        }

        /// <summary>
        ///     Ensure the token is valid. If the token is invalid, an exception is thrown.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task EnsureTokenAsync(CancellationToken cancellationToken = default)
        {
            return _originalAuthenticationService.EnsureTokenAsync(cancellationToken);
        }

        /// <summary>
        ///     Get the QR code.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        public TVQRCode GetQrCode()
        {
            return _qrCode ?? throw new NullReferenceException("Cannot get the QR code.");
        }

        /// <summary>
        ///     Get the QR code image.
        /// </summary>
        /// <returns></returns>
        public byte[]? GetQrCodeImage()
        {
            return _qrCodeImage;
        }

        /// <summary>
        ///     Wait for the QR code to be scanned.
        /// </summary>
        /// <param name="qrCode"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task WaitQrCodeScanAsync(TVQRCode qrCode, CancellationToken cancellationToken = default)
        {
            var client = GetClient();
            return client.GetType().GetMethod("WaitQRCodeScanAsync")
                       ?.Invoke(client, [qrCode, cancellationToken]) as Task ??
                   throw new NullReferenceException("Cannot get the method.");
        }

        private object GetClient()
        {
            return _originalAuthenticationService.GetType()
                       .GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)
                       ?.GetValue(_originalAuthenticationService) ??
                   throw new NullReferenceException("Cannot get the client.");
        }

        private static Task<TVQRCode> GetQrCode(object client, CancellationToken cancellationToken)
        {
            var method = client.GetType().GetMethod("GetQRCodeAsync", BindingFlags.Public | BindingFlags.Instance) ??
                         throw new NullReferenceException($"Cannot get the method {nameof(GetQrCode)}.");
            return method.Invoke(client, [cancellationToken]) as Task<TVQRCode> ??
                   throw new NullReferenceException("Cannot get the result.");
        }
    }
}
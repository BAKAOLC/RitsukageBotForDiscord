using Richasy.BiliKernel.Bili.Authorization;

namespace RitsukageBot.Library.Bilibili.BiliKernelModules.Resolvers
{
    /// <summary>
    ///     Empty QR code resolver
    /// </summary>
    public sealed class EmptyQrCodeResolver : IQRCodeResolver
    {
        /// <summary>
        ///     Render QR code
        /// </summary>
        /// <param name="qrImageData"></param>
        /// <returns></returns>
        public Task RenderAsync(byte[] qrImageData)
        {
            return Task.CompletedTask;
        }
    }
}
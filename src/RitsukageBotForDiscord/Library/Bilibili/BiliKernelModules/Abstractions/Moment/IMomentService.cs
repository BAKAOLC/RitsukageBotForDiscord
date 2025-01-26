using Richasy.BiliKernel.Bili.Moment;
using Richasy.BiliKernel.Models.Moment;

namespace RitsukageBot.Library.Bilibili.BiliKernelModules.Abstractions.Moment
{
    /// <summary>
    ///     Moment service.
    /// </summary>
    public interface IMomentService : IMomentDiscoveryService, IMomentOperationService
    {
        /// <summary>
        ///     Get moment information.
        /// </summary>
        /// <param name="momentId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        // ReSharper disable once AsyncApostle.AsyncMethodNamingHighlighting
        Task<MomentInformation> GetMomentInformation(string momentId, CancellationToken cancellationToken = default);
    }
}
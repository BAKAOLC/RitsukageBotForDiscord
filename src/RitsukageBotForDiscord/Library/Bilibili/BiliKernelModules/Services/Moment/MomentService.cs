using Bilibili.App.Dynamic.V2;
using Richasy.BiliKernel;
using Richasy.BiliKernel.Authenticator;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Http;
using Richasy.BiliKernel.Models.Moment;
using Richasy.BiliKernel.Models.User;
using Richasy.BiliKernel.Services.Moment;
using Richasy.BiliKernel.Services.Moment.Core;
using RitsukageBot.Library.Bilibili.BiliKernelModules.Abstractions.Moment;

namespace RitsukageBot.Library.Bilibili.BiliKernelModules.Services.Moment
{
    /// <inheritdoc />
    public class MomentService(
        BiliHttpClient httpClient,
        BiliAuthenticator authenticator,
        IBiliTokenResolver tokenResolver) : IMomentService
    {
        private readonly MomentDiscoveryService _momentDiscoveryService = new(httpClient, authenticator, tokenResolver);
        private readonly MomentOperationService _momentOperationService = new(httpClient, authenticator, tokenResolver);

        #region IMomentService

        /// <inheritdoc />
        // ReSharper disable once AsyncApostle.AsyncMethodNamingHighlighting
        public async Task<MomentInformation> GetMomentInformation(string momentId,
            CancellationToken cancellationToken = default)
        {
            var req = new DynDetailReq
            {
                DynamicId = momentId,
            };

            var request = BiliHttpClient.CreateRequest(new(BiliApis.Community.DynamicDetail), req);
            authenticator.AuthorizeGrpcRequest(request, false);
            var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseObj = await BiliHttpClient.ParseAsync(response, DynDetailReply.Parser).ConfigureAwait(false);
            var detail = responseObj.Item;
            return detail is null
                ? throw new KernelException("没有获取到动态信息，请稍后重试")
                : detail.ToMomentInformation();
        }

        #endregion

        #region IMomentDiscoveryService

        /// <inheritdoc />
        public Task<(IReadOnlyList<MomentInformation> Moments, string Offset, bool HasMore)> GetUserMomentsAsync(
            UserProfile user, string? offset = null, CancellationToken cancellationToken = default)
        {
            return _momentDiscoveryService.GetUserComprehensiveMomentsAsync(user, offset, cancellationToken);
        }

        /// <inheritdoc />
        public Task<MomentView> GetComprehensiveMomentsAsync(string? offset = null, string? baseline = null,
            CancellationToken cancellationToken = default)
        {
            return _momentDiscoveryService.GetComprehensiveMomentsAsync(offset, baseline, cancellationToken);
        }

        /// <inheritdoc />
        public Task<MomentView> GetVideoMomentsAsync(string? offset = null, string? baseline = null,
            CancellationToken cancellationToken = default)
        {
            return _momentDiscoveryService.GetVideoMomentsAsync(offset, baseline, cancellationToken);
        }

        /// <inheritdoc />
        public Task<(IReadOnlyList<MomentInformation> Moments, string Offset, bool HasMore)> GetMyMomentsAsync(
            string? offset = null, CancellationToken cancellationToken = default)
        {
            return _momentDiscoveryService.GetMyMomentsAsync(offset, cancellationToken);
        }

        /// <inheritdoc />
        public Task<(IReadOnlyList<MomentInformation> Moments, string Offset, bool HasMore)>
            GetUserComprehensiveMomentsAsync(UserProfile user, string? offset = null,
                CancellationToken cancellationToken = default)
        {
            return _momentDiscoveryService.GetUserComprehensiveMomentsAsync(user, offset, cancellationToken);
        }

        /// <inheritdoc />
        public Task<(IReadOnlyList<MomentInformation> Moments, string Offset, bool HasMore)> GetUserVideoMomentsAsync(
            UserProfile user, string? offset = null, CancellationToken cancellationToken = default)
        {
            return _momentDiscoveryService.GetUserVideoMomentsAsync(user, offset, cancellationToken);
        }

        #endregion

        #region IMomentOperationService

        /// <inheritdoc />
        public Task DislikeMomentAsync(MomentInformation moment, CancellationToken cancellationToken = default)
        {
            return _momentOperationService.LikeMomentAsync(moment, cancellationToken);
        }

        /// <inheritdoc />
        public Task LikeMomentAsync(MomentInformation moment, CancellationToken cancellationToken = default)
        {
            return _momentOperationService.LikeMomentAsync(moment, cancellationToken);
        }

        /// <inheritdoc />
        public Task MarkUserMomentAsReadAsync(MomentProfile user, string? offset = null,
            CancellationToken cancellationToken = default)
        {
            return _momentOperationService.MarkUserMomentAsReadAsync(user, offset, cancellationToken);
        }

        #endregion
    }
}
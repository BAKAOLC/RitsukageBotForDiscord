using Microsoft.Extensions.DependencyInjection;
using Richasy.BiliKernel.Bili.User;
using RitsukageBot.Library.AI.Attributes;
using RitsukageBot.Library.Bilibili.Convertors;
using RitsukageBot.Services.Providers;

namespace RitsukageBot.Library.AI.Bundles.Bilibili
{
    /// <summary>
    ///     Chat client tools for Bilibili user.
    /// </summary>
    [ChatClientToolsBundle]
    public class UserTools(IServiceProvider serviceProvider)
    {
        private readonly BiliKernelProviderService _biliKernelProviderService =
            serviceProvider.GetRequiredService<BiliKernelProviderService>();

        private IUserService UserService => _biliKernelProviderService.GetRequiredService<IUserService>();

        /// <summary>
        ///     Get user info.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        [ChatClientTool]
        public async Task<string> GetBilibiliUserInfo(ulong userId)
        {
            var detail = await UserService.GetUserInformationAsync(userId.ToString()).ConfigureAwait(false);
            return InformationStringBuilder.BuildUserInfo(detail);
        }
    }
}
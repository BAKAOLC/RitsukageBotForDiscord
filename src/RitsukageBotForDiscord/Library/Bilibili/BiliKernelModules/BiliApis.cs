namespace RitsukageBot.Library.Bilibili.BiliKernelModules
{
    /// <summary>
    ///     B站API地址
    /// </summary>
    public static class BiliApis
    {
        private const string GrpcBase = "https://grpc.biliapi.net";

        /// <summary>
        ///     B站社区相关API
        /// </summary>
        public static class Community
        {
            /// <summary>
            ///     获取用户动态
            /// </summary>
            public const string DynamicDetail = GrpcBase + "/bilibili.app.dynamic.v2.Dynamic/DynDetail";
        }
    }
}
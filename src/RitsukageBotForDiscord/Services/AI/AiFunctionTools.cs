using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace RitsukageBot.Services.AI
{
    /// <summary>
    /// AI Function Tools for Function Calling
    /// </summary>
    public static class AiFunctionTools
    {
        /// <summary>
        /// Get all available function tools for AI
        /// </summary>
        /// <returns></returns>
        public static AIFunction[] GetAllFunctions()
        {
            return
            [
                // Information retrieval functions
                AIFunctionFactory.Create(WebSearchAsync, "Search the web for information"),
                AIFunctionFactory.Create(GetDateInfoAsync, "Get date and calendar information for a specific date"),
                AIFunctionFactory.Create(GetRangeDateInfoAsync, "Get date and calendar information for a date range"),
                
                // Bilibili integration functions
                AIFunctionFactory.Create(GetBilibiliVideoInfoAsync, "Get information about a Bilibili video"),
                AIFunctionFactory.Create(GetBilibiliUserInfoAsync, "Get information about a Bilibili user"),
                AIFunctionFactory.Create(GetBilibiliLiveInfoAsync, "Get information about a Bilibili live stream"),
                
                // Memory management functions
                AIFunctionFactory.Create(AddShortMemoryAsync, "Add information to short-term memory"),
                AIFunctionFactory.Create(AddLongMemoryAsync, "Add information to long-term memory"),
                AIFunctionFactory.Create(UpdateSelfStateAsync, "Update self state information"),
                AIFunctionFactory.Create(RemoveLongMemoryAsync, "Remove information from long-term memory"),
                AIFunctionFactory.Create(RemoveSelfStateAsync, "Remove information from self state"),
                
                // User interaction functions
                AIFunctionFactory.Create(ModifyUserGoodAsync, "Modify user's good points based on their behavior")
            ];
        }
        
        /// <summary>
        /// Search the web for information
        /// </summary>
        /// <param name="query">Search query</param>
        /// <returns>Search results</returns>
        [Description("Search the web for current information")]
        public static async Task<string> WebSearchAsync(
            [Description("The search query to look for")] string query)
        {
            // This will be implemented by the calling context
            throw new NotImplementedException("This function should be implemented by the AI interaction context");
        }

        /// <summary>
        /// Get date and calendar information for a specific date
        /// </summary>
        /// <param name="date">The date to get information for</param>
        /// <returns>Date and calendar information</returns>
        [Description("Get detailed date and calendar information including holidays and lunar calendar")]
        public static async Task<string> GetDateInfoAsync(
            [Description("The date in YYYY-MM-DD format")] string date)
        {
            throw new NotImplementedException("This function should be implemented by the AI interaction context");
        }

        /// <summary>
        /// Get date and calendar information for a date range
        /// </summary>
        /// <param name="fromDate">Start date</param>
        /// <param name="toDate">End date</param>
        /// <returns>Date range information</returns>
        [Description("Get date and calendar information for a range of dates")]
        public static async Task<string> GetRangeDateInfoAsync(
            [Description("Start date in YYYY-MM-DD format")] string fromDate,
            [Description("End date in YYYY-MM-DD format")] string toDate)
        {
            throw new NotImplementedException("This function should be implemented by the AI interaction context");
        }

        /// <summary>
        /// Get information about a Bilibili video
        /// </summary>
        /// <param name="videoId">Bilibili video ID</param>
        /// <returns>Video information</returns>
        [Description("Get detailed information about a Bilibili video")]
        public static async Task<string> GetBilibiliVideoInfoAsync(
            [Description("The Bilibili video ID (BV or AV number)")] string videoId)
        {
            throw new NotImplementedException("This function should be implemented by the AI interaction context");
        }

        /// <summary>
        /// Get information about a Bilibili user
        /// </summary>
        /// <param name="userId">Bilibili user ID</param>
        /// <returns>User information</returns>
        [Description("Get detailed information about a Bilibili user")]
        public static async Task<string> GetBilibiliUserInfoAsync(
            [Description("The Bilibili user ID (UID number)")] string userId)
        {
            throw new NotImplementedException("This function should be implemented by the AI interaction context");
        }

        /// <summary>
        /// Get information about a Bilibili live stream
        /// </summary>
        /// <param name="liveId">Live stream ID</param>
        /// <returns>Live stream information</returns>
        [Description("Get information about a Bilibili live stream")]
        public static async Task<string> GetBilibiliLiveInfoAsync(
            [Description("The Bilibili live stream ID")] string liveId)
        {
            throw new NotImplementedException("This function should be implemented by the AI interaction context");
        }

        /// <summary>
        /// Add information to short-term memory
        /// </summary>
        /// <param name="key">Memory key</param>
        /// <param name="value">Memory value</param>
        /// <returns>Result message</returns>
        [Description("Store information in short-term memory for current conversation")]
        public static async Task<string> AddShortMemoryAsync(
            [Description("Key for the memory item")] string key,
            [Description("Value to store in memory")] string value)
        {
            throw new NotImplementedException("This function should be implemented by the AI interaction context");
        }

        /// <summary>
        /// Add information to long-term memory
        /// </summary>
        /// <param name="key">Memory key</param>
        /// <param name="value">Memory value</param>
        /// <returns>Result message</returns>
        [Description("Store information in long-term memory for future conversations")]
        public static async Task<string> AddLongMemoryAsync(
            [Description("Key for the memory item")] string key,
            [Description("Value to store in memory")] string value)
        {
            throw new NotImplementedException("This function should be implemented by the AI interaction context");
        }

        /// <summary>
        /// Update self state information
        /// </summary>
        /// <param name="key">State key</param>
        /// <param name="value">State value</param>
        /// <returns>Result message</returns>
        [Description("Update internal self state information")]
        public static async Task<string> UpdateSelfStateAsync(
            [Description("Key for the state item")] string key,
            [Description("New state value")] string value)
        {
            throw new NotImplementedException("This function should be implemented by the AI interaction context");
        }

        /// <summary>
        /// Remove information from long-term memory
        /// </summary>
        /// <param name="key">Memory key to remove</param>
        /// <returns>Result message</returns>
        [Description("Remove information from long-term memory")]
        public static async Task<string> RemoveLongMemoryAsync(
            [Description("Key of the memory item to remove")] string key)
        {
            throw new NotImplementedException("This function should be implemented by the AI interaction context");
        }

        /// <summary>
        /// Remove information from self state
        /// </summary>
        /// <param name="key">State key to remove</param>
        /// <returns>Result message</returns>
        [Description("Remove information from self state")]
        public static async Task<string> RemoveSelfStateAsync(
            [Description("Key of the state item to remove")] string key)
        {
            throw new NotImplementedException("This function should be implemented by the AI interaction context");
        }

        /// <summary>
        /// Modify user's good points
        /// </summary>
        /// <param name="value">Points to add or subtract</param>
        /// <param name="reason">Reason for the change</param>
        /// <returns>Result message</returns>
        [Description("Modify user's good points based on their behavior or interactions")]
        public static async Task<string> ModifyUserGoodAsync(
            [Description("Points to add (positive) or subtract (negative)")] int value,
            [Description("Reason for the point change")] string reason = "")
        {
            throw new NotImplementedException("This function should be implemented by the AI interaction context");
        }
    }
}
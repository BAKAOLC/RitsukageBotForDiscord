namespace RitsukageBot.Library.AI.Attributes
{
    /// <summary>
    ///     Attribute to mark a class as a chat client tool
    ///     Must be instantiable, must have a parameterless constructor or a constructor with an IServiceProvider parameter
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ChatClientToolsBundleAttribute : Attribute
    {
    }
}
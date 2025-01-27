using System.ComponentModel;

namespace RitsukageBot.Library.AI.Attributes
{
    /// <summary>
    ///     Attribute to mark a method as a chat client tool
    ///     Method should have a <see cref="DescriptionAttribute" /> to provide a description
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ChatClientToolAttribute : Attribute
    {
    }
}
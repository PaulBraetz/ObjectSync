using System;

namespace ObjectSync.Attributes
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public sealed class GenerateEventsAttribute : Attribute
    {
    }
}

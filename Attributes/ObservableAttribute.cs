using System;

namespace ObjectSync.Attributes
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public sealed class ObservableAttribute : Attribute
	{
		public ObservableAttribute() : this(null)
		{

		}
		public ObservableAttribute(string propertyName)
		{
			PropertyName = propertyName;
		}
		public string PropertyName { get; }
	}
}

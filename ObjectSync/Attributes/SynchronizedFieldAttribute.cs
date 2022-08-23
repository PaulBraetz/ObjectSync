using System;

namespace ObjectSync.Attributes
{
	[AttributeUsage(AttributeTargets.Field, Inherited = false)]
	public sealed class SynchronizedFieldAttribute : Attribute
	{
		public SynchronizedFieldAttribute() : this(null)
		{

		}
		public SynchronizedFieldAttribute(String propertyName)
		{
			PropertyName = propertyName;
		}
		public String PropertyName { get; }
	}
}

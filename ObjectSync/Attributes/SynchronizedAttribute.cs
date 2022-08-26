using System;

namespace ObjectSync.Attributes
{
	[AttributeUsage(AttributeTargets.Field, Inherited = false)]
	public sealed class SynchronizedAttribute : Attribute
	{
		public SynchronizedAttribute() : this(null)
		{

		}
		public SynchronizedAttribute(String propertyName)
		{
			PropertyName = propertyName;
		}
		public String PropertyName { get; }
	}
}

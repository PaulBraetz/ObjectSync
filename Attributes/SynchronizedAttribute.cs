using System;

namespace ObjectSync.Attributes
{
	[Obsolete]
	[AttributeUsage(AttributeTargets.Field, Inherited = false)]
	public sealed class SynchronizedAttribute : Attribute
	{
		public SynchronizedAttribute() : this(null)
		{

		}
		public SynchronizedAttribute(string propertyName)
		{
			PropertyName = propertyName;
		}
		public string PropertyName { get; }
	}
}

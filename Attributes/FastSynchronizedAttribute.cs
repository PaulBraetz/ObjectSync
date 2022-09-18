using System;

namespace ObjectSync.Attributes
{
	[Obsolete]
	[AttributeUsage(AttributeTargets.Field, Inherited = false)]
	public sealed class FastSynchronizedAttribute : Attribute
	{
		public FastSynchronizedAttribute() : this(null)
		{

		}
		public FastSynchronizedAttribute(string propertyName)
		{
			PropertyName = propertyName;
		}
		public string PropertyName { get; }
	}
}

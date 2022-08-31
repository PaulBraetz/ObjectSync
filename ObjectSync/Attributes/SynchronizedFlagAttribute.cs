using System;

namespace ObjectSync.Attributes
{
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
	public sealed class SynchronizedFlagAttribute : Attribute
	{
		public SynchronizedFlagAttribute()
		{

		}
	}
}

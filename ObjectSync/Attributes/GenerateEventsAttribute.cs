using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectSync.Attributes
{
	[AttributeUsage(AttributeTargets.Field, Inherited = false)]
	public sealed class GenerateEventsAttribute : Attribute
	{
	}
}

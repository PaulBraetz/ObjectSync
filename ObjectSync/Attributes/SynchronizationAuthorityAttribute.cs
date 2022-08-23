using System;

namespace ObjectSync.Attributes
{
	/// <summary>
	/// Denotes the synchronization authority for synchronized fields of this type. 
	/// The type returned must provide a method with the signature <c>void Synchronize&lt;TProperty&gt;(String, String, Action&lt;TProperty&gt;);</c>
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
	public sealed class SynchronizationAuthorityAttribute : Attribute
	{

	}
}

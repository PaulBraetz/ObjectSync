using System;

namespace ObjectSync.Attributes
{
	/// <summary>
	/// <para>
	/// Denotes the synchronization authority for synchronized fields of this type. 
	/// The type returned must provide the following methods:
	/// </para>
	/// <para>
	/// <c>TProperty Pull&lt;TProperty&gt;(String typeId, String propertyName, String sourceInstanceId, String instanceId)</c>
	/// </para>
	/// <para>
	/// <c>void Push&lt;TProperty&gt;(String typeId, String propertyName, String sourceInstanceId, String instanceId, TProperty value)</c>
	/// </para>
	/// <para>
	/// <c>void Subscribe&lt;TProperty&gt;(String typeId, String propertyName, String sourceInstanceId, String instanceId, Action&lt;TProperty&gt; callback)</c>
	/// </para>
	/// <para>
	/// <c>void Unsubscribe(String typeId, String propertyName, String sourceInstanceId, String instanceId)</c>
	/// </para>
	/// </summary>
	[Obsolete]
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
	public sealed class SynchronizationAuthorityAttribute : Attribute
	{

	}
}

using System;

namespace ObjectSync.Synchronization
{
	public abstract class SynchronizationAuthorityBase : ISynchronizationAuthority
	{
		public abstract TProperty Pull<TProperty>(String synchronizationId, String propertyName, String instanceId);
		public abstract void Push<TProperty>(String synchronizationId, String propertyName, String instanceId, TProperty value);
		public abstract void Subscribe<TProperty>(String synchronizationId, String propertyName, String instanceId, Action<TProperty> callback);
		public abstract void Unsubscribe(String synchronizationId, String propertyName, String instanceId);
	}
}

using System;

namespace ObjectSync.Synchronization
{
	public interface ISynchronizationAuthority
	{
		void Synchronize<TProperty>(String objectId, String propertyId, Action<TProperty> callback);
	}
	public abstract class SynchronizationAuthorityBase : ISynchronizationAuthority
	{
		public abstract void Synchronize<TProperty>(String objectId, String propertyId, Action<TProperty> callback);
	}
}

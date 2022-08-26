using System;

namespace ObjectSync.Synchronization
{
	public interface ISynchronizationAuthority
    {
        void Push<TProperty>(String objectId, String propertyId, TProperty value);
        void Subscribe<TProperty>(String objectId, String propertyId, Action<TProperty> callback);
		void Unsubscribe(String objectId, String propertyId);
    }
	public abstract class SynchronizationAuthorityBase : ISynchronizationAuthority
	{
		public abstract void Push<TProperty>(String objectId, String propertyId, TProperty value);
		public abstract void Subscribe<TProperty>(String objectId, String propertyId, Action<TProperty> callback);
		public abstract void Unsubscribe(String objectId, String propertyId);
	}
}

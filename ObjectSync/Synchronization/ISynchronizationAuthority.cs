using System;

namespace ObjectSync.Synchronization
{
	public interface ISynchronizationAuthority
    {
        void Push<TProperty>(String objectId, String propertyName, TProperty value);
        void Subscribe<TProperty>(String objectId, String propertyName, Action<TProperty> callback);
		void Unsubscribe(String objectId, String propertyName);
    }
	public abstract class SynchronizationAuthorityBase : ISynchronizationAuthority
	{
		public abstract void Push<TProperty>(String objectId, String propertyName, TProperty value);
		public abstract void Subscribe<TProperty>(String objectId, String propertyName, Action<TProperty> callback);
		public abstract void Unsubscribe(String objectId, String propertyName);
	}
}

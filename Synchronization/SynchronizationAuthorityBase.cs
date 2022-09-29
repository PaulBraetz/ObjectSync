namespace ObjectSync.Synchronization
{
	[Obsolete]
	public abstract class SynchronizationAuthorityBase : ISynchronizationAuthority
	{
		protected abstract TProperty Pull<TProperty>(SyncInfo syncInfo);
		public TProperty Pull<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId)
		{
			return Pull<TProperty>(new SyncInfo(typeId, propertyName, sourceInstanceId, instanceId));
		}

		protected abstract void Push<TProperty>(SyncInfo syncInfo, TProperty value);
		public void Push<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId, TProperty value)
		{
			Push<TProperty>(new SyncInfo(typeId, propertyName, sourceInstanceId, instanceId), value);
		}

		protected abstract void Subscribe<TProperty>(SyncInfo syncInfo, Action<TProperty> callback);
		public void Subscribe<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId, Action<TProperty> callback)
		{
			Subscribe<TProperty>(new SyncInfo(typeId, propertyName, sourceInstanceId, instanceId), callback);
		}

		protected abstract void Unsubscribe(SyncInfo syncInfo);
		public void Unsubscribe(String typeId, String propertyName, String sourceInstanceId, String instanceId)
		{
			Unsubscribe(new SyncInfo(typeId, propertyName, sourceInstanceId, instanceId));
		}
	}
}

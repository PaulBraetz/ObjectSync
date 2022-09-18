namespace ObjectSync.Synchronization
{
	public interface ISynchronizationAuthority
	{
		TProperty Pull<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId);
		void Push<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId, TProperty value);
		void Subscribe<TProperty>(String typeId, String propertyName, String sourceInstanceId, String instanceId, Action<TProperty> callback);
		void Unsubscribe(String typeId, String propertyName, String sourceInstanceId, String instanceId);
	}
}

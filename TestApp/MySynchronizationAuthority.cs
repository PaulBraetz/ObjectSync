using ObjectSync.Synchronization;

namespace TestApp
{
	public sealed class MySynchronizationAuthority: StaticSynchronizationAuthority
	{
		public override void Push<TProperty>(String synchronizationId, String propertyName, String instanceId, TProperty value)
		{
			Console.WriteLine($"Pushing:\t{instanceId}\tValue: {value}");
			base.Push(synchronizationId, propertyName, instanceId, value);
        }

		public override void Subscribe<T>(String synchronizationId, String propertyName, String instanceId, Action<T> callback)
		{
			Console.WriteLine($"Subscribing:\t{instanceId}");
			base.Subscribe(synchronizationId, propertyName, instanceId, callback);
        }

		public override void Unsubscribe(String synchronizationId, String propertyName, String instanceId)
		{
			Console.WriteLine($"Unsubscribing:\t{instanceId}");
			base.Unsubscribe(synchronizationId, propertyName, instanceId);
        }

		public override TProperty Pull<TProperty>(String synchronizationId, String propertyName, String instanceId)
		{
			var value = base.Pull<TProperty>(synchronizationId, propertyName, instanceId);
			Console.WriteLine($"Pulled:\t\t{instanceId}\tValue: {value}");
			return value;
		}
	}
}
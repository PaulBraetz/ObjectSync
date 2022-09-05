using ObjectSync.Synchronization;

namespace TestApp
{
	public sealed class MySynchronizationAuthority : StaticSynchronizationAuthority
	{
		protected override void Push<TProperty>(SyncInfo syncInfo, TProperty value)
		{
			Console.WriteLine($"Pushing:\t{syncInfo.PropertyStateId} = {value}");
			base.Push(syncInfo, value);
		}

		protected override void Subscribe<T>(SyncInfo syncInfo, Action<T> callback)
		{
			Console.WriteLine($"Subscribing:\t{syncInfo.InstanceId} to {syncInfo.PropertyStateId}");
			base.Subscribe(syncInfo, callback);
		}

		protected override void Unsubscribe(SyncInfo syncInfo)
		{
			Console.WriteLine($"Unsubscribing:\t{syncInfo.InstanceId} from {syncInfo.PropertyStateId}");
			base.Unsubscribe(syncInfo);
		}

		protected override TProperty Pull<TProperty>(SyncInfo syncInfo)
		{
			var value = base.Pull<TProperty>(syncInfo);
			Console.WriteLine($"Pulled:\t\t{syncInfo.PropertyStateId} = {value}");
			return value;
		}
	}
}
using SomeExternalAssembly.ObjectSync.Synchronization;

namespace TestApp
{
	internal sealed class MySynchronizationAuthority : StaticSynchronizationAuthority
	{
		protected override void Push<TProperty>(SyncInfo syncInfo, TProperty value)
		{
			Console.WriteLine($"Pushing:\t{syncInfo.FieldName} = {value}");
			base.Push(syncInfo, value);
		}

		protected override void Subscribe<T>(SyncInfo syncInfo, Action<T> callback)
		{
			Console.WriteLine($"Subscribing:\t{syncInfo.InstanceId} to {syncInfo.FieldStateId}");
			base.Subscribe(syncInfo, callback);
		}

		protected override void Unsubscribe(SyncInfo syncInfo)
		{
			Console.WriteLine($"Unsubscribing:\t{syncInfo.InstanceId} from {syncInfo.FieldStateId}");
			base.Unsubscribe(syncInfo);
		}

		protected override TProperty Pull<TProperty>(SyncInfo syncInfo)
		{
			var value = base.Pull<TProperty>(syncInfo);
			Console.WriteLine($"Pulled:\t\t{syncInfo.FieldStateId} = {value}");
			return value;
		}
	}
}
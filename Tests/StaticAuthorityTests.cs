using ObjectSync.Attributes;
using ObjectSync.Synchronization;
using System.Linq;

namespace Tests
{
	internal sealed class SampleAuthority : StaticSynchronizationAuthority
	{
		public SampleAuthority() { }
	}
	[SynchronizationTarget]
	internal sealed partial class SampleEntity<T1, T2, T3>
	{
		[SynchronizationAuthority]
		private ISynchronizationAuthority Authority { get; } = new SampleAuthority();

		[Synchronized]
		private T1? _value1;
		[Synchronized]
		private T2? _value2;
		[Synchronized]
		private T3? _value3;
		public void Synchronize(SampleEntity<T1, T2, T3> other)
		{

		}
	}
	[TestClass]
	public class StaticAuthorityTests
	{
		private static SampleAuthority Instance = new SampleAuthority();
		private static readonly Int32 _degreeOfParallelism = Environment.ProcessorCount;

		private static void PushPull<T>(Func<Int32, T> rng, Int32 count = 1000)
		{
			var testGroups = Enumerable.Range(0, count)
				.Select(rng)
				.Select<T, Action>(v => () => pushPull(v))
				.Select((e, i) => (Group: i % _degreeOfParallelism, Test: e))
				.GroupBy(e => e.Group, e => e.Test);

			_ = Parallel.ForEach(testGroups, g =>
			{
				foreach (var test in g)
				{
					test.Invoke();
				}
			});

			static void pushPull(T pushedValue)
			{
				String typeId = Guid.NewGuid().ToString();
				String sourceInstanceId = Guid.NewGuid().ToString();
				String instanceId = Guid.NewGuid().ToString();
				String fieldName = Guid.NewGuid().ToString();

				Instance.Push<T>(typeId, fieldName, sourceInstanceId, instanceId, pushedValue);
				var actual = Instance.Pull<T>(typeId, fieldName, sourceInstanceId, instanceId);

				Assert.AreEqual(pushedValue, actual);
			}
		}

		[TestMethod]
		public void PushPullNumerics()
		{
			PushPull(i => (SByte)Random.Shared.Next(SByte.MinValue - 1, SByte.MaxValue + 1));
			PushPull(i => (Int16)Random.Shared.Next(Int16.MinValue - 1, Int16.MaxValue + 1));
			PushPull(i => (Int32)Random.Shared.Next());
			PushPull(i => (Int64)Random.Shared.NextInt64());
			PushPull(i => (Byte)Random.Shared.Next(Byte.MinValue - 1, Byte.MaxValue + 1));
			PushPull(i => (UInt16)Random.Shared.Next(UInt16.MinValue - 1, UInt16.MaxValue + 1));
			PushPull(i => (UInt32)Random.Shared.Next());
			PushPull(i => (UInt64)Random.Shared.NextInt64());
			PushPull(i => Random.Shared.NextSingle());
			PushPull(i => Random.Shared.NextDouble());
		}

		[TestMethod]
		public void PushPullString()
		{
			PushPull(i => Guid.NewGuid().ToString(), 10000);
		}

		[TestMethod]
		public void PushPullObject()
		{
			PushPull(i => new Object(), 10000);
		}

		//TODO: continue here
		private void SubscribePush<T>(Func<Int32, T> rng, Int32 count = 10000)
		{
			var entities = Enumerable.Range(0, 100)
				.Select(i => new SampleEntity<T, T, T>());



			var testGroups = Enumerable.Range(0, count)
				.Select(rng)
				.Select<T, Action>(v => () => pushPull(v))
				.Select((e, i) => (Group: i % _degreeOfParallelism, Test: e))
				.GroupBy(e => e.Group, e => e.Test);

			_ = Parallel.ForEach(testGroups, g =>
			{
				foreach (var test in g)
				{
					test.Invoke();
				}
			});

			static void pushPull(T pushedValue)
			{
				String typeId = Guid.NewGuid().ToString();
				String sourceInstanceId = Guid.NewGuid().ToString();
				String instanceId = Guid.NewGuid().ToString();
				String fieldName = Guid.NewGuid().ToString();

				Instance.Push<T>(typeId, fieldName, sourceInstanceId, instanceId, pushedValue);
				var actual = Instance.Pull<T>(typeId, fieldName, sourceInstanceId, instanceId);

				Assert.AreEqual(pushedValue, actual);
			}
		}
	}
}
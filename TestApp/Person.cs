using ObjectSync.Attributes;
using ObjectSync.Synchronization;
using System.ComponentModel;

namespace TestApp
{
	public partial class Person
	{
		public Person(String name, Byte age) : this(Guid.NewGuid())
		{
			GetSynchronizationState().Synchronize();
			Name = name;
			Age = age;
		}
		public Person(Guid id)
		{
			Id = id;
			SyncId = id.ToString();

			GetSynchronizationState().Synchronize();
		}

		[Synchronized]
		private String? _name;

		[Synchronized]
		private Byte _age;

		public Guid Id { get; }

		[SynchronizationId]
		private String SyncId { get; }

		[SynchronizationAuthority]
		private ISynchronizationAuthority _authority { get; } = new StaticSynchronizationAuthority();

		public override String ToString()
		{
			return $"Name: {_name}, Age: {_age}";
		}
	}
}
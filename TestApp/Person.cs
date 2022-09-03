using ObjectSync.Attributes;
using ObjectSync.Synchronization;
using System.ComponentModel;

namespace TestApp
{
	public partial class Person
	{
		public Person()
		{
			GetSynchronizationState().Synchronize();
			Name = "Mike";
			Age = 5;
		}
		public Person(String synchronizationId)
		{
			SynchronizationId = synchronizationId;
			GetSynchronizationState().Synchronize();
		}

		[Synchronized("Name")]
		private String _name;

		[Synchronized("Age")]
		private Byte _age;

		[SynchronizationAuthority]
		private ISynchronizationAuthority _authority { get; } = new StaticSynchronizationAuthority();

		public Person Clone()
		{
			return new Person(SynchronizationId);
		}

		public override String ToString()
		{
			return $"Name: {_name}, Age: {_age}";
		}
	}
}
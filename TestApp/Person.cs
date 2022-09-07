using ObjectSync.Attributes;
using ObjectSync.Synchronization;
using System;
using System.ComponentModel;

namespace TestApp
{
	public partial class Person
	{
		public Person(String name)
		{
			Synchronize(Guid.NewGuid().ToString());
			Name = name;
		}
		public Person(Person person)
		{
			SynchronizeTo(person);
		}

		[Synchronized]
		private String? _name;

		[SynchronizationAuthority]
		private ISynchronizationAuthority Authority { get; } = StaticSynchronizationAuthority.Instance;

		public override String ToString()
		{
			return $"{SourceInstanceId[..2]}-{Name}";
		}

		public void Desynchronize()
		{
			SynchronizationContext.Desynchronize();
			SourceInstanceId = Guid.NewGuid().ToString();
		}
		public void SynchronizeTo(Person person)
		{
			Synchronize(person.SourceInstanceId);
		}
		private void Synchronize(string id)
		{
			SourceInstanceId = id;
			SynchronizationContext.Synchronize();
		}
	}
}
﻿using ObjectSync.Synchronization;
using ObjectSync.Attributes;

namespace TestApp.Data.AnotherNamespace
{
	public partial class Person
	{
		public Person(String name)
		{
			SynchronizeTo(Guid.NewGuid().ToString());
			Name = name;
		}
		public Person(Person person)
		{
			SynchronizeTo(person);
		}

		[Synchronized]
		private String? _name;
		[Synchronized]
		private Byte _age;

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
			SynchronizeTo(person.SourceInstanceId);
		}
		/// <summary>
		/// For <paramref name="id"/>
		/// </summary>
		/// <param name="id"></param>
		private void SynchronizeTo(string id)
		{
			SynchronizationContext.Desynchronize();
			SourceInstanceId = id;
			SynchronizationContext.Synchronize();
		}
	}
}
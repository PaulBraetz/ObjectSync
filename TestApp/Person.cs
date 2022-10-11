﻿using ObjectSync.Synchronization;
using ObjectSync.Attributes;

namespace TestApp.Data.AnotherNamespace
{
	[SynchronizationTarget(ContextTypeAccessibility = ObjectSync.Attributes.Attributes.Accessibility.Protected,
						  ContextTypeIsSealed = false)]
	public abstract partial class PersonBase
	{
		public PersonBase()
		{
			_instanceCount++;
			InstanceId = _instanceCount.ToString();
		}

		private static Int32 _instanceCount;

		[SynchronizationAuthority]
		protected ISynchronizationAuthority Authority { get; } = new MySynchronizationAuthority();

		[InstanceId]
		public String InstanceId { get; }

		[TypeId]
		private static String TypeId { get; } = "PersonType";

		[SourceInstanceId]
		protected String SourceInstanceId { get; set; } = Guid.NewGuid().ToString();
	}

	[SynchronizationTarget(BaseContextTypeName = nameof(PersonBase.PersonBaseSynchronizationContext),
						   ContextTypeIsSealed = true)]
	internal sealed partial class Person : PersonBase
	{
		public Person(String name)
		{
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

		public override String ToString()
		{
			return $"{InstanceId}->{String.Concat(SourceInstanceId.Take(2))}-{Name}";
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
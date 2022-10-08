using ObjectSync.Synchronization;
using ObjectSync.Attributes;

namespace TestApp.Data.AnotherNamespace
{
	[SynchronizationTarget(ContextTypeAccessibility = ObjectSync.Attributes.Attributes.Accessibility.Protected,
						   ContextTypeIsSealed = false)]
	public abstract partial class PersonBase
	{
		[SynchronizationAuthority]
		protected ISynchronizationAuthority Authority { get; } = new MySynchronizationAuthority();

		[Synchronized(PropertyAccessibility = Attributes.Accessibility.Private)]
		private Byte _age;
	}

	[SynchronizationTarget]
	internal sealed partial class Person// : PersonBase
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

		[SynchronizationAuthority]
		protected ISynchronizationAuthority Authority { get; } = new MySynchronizationAuthority();

		[Synchronized]
		private String? _name;

		[SourceInstanceId]
		private String SourceInstanceId { get; set; } = Guid.NewGuid().ToString();

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
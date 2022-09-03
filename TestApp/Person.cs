using ObjectSync.Attributes;
using ObjectSync.Synchronization;
using System.ComponentModel;

namespace TestApp
{
	public partial class Person
	{
		public Person()
		{
			Synchronize();
		}
		public Person(String synchronizationId)
		{
			SynchronizationId = synchronizationId;
			Synchronize();
		}

		[Synchronized("Name")]
		[EventIntegration]
		private String _name = String.Empty;

		[SynchronizationAuthority]
		private ISynchronizationAuthority SynchronizationAuthority { get; } = new StaticSynchronizationAuthority();

		public event EventHandler<PropertyChangedEventArgs>? PropertyChanged;

		partial void OnPropertyChanged(PropertyChangedEventArgs args)
		{
			PropertyChanged?.Invoke(this, args);
		}

		public event EventHandler<PropertyChangingEventArgs>? PropertyChanging;
		partial void OnPropertyChanging(PropertyChangingEventArgs args)
		{
			PropertyChanging?.Invoke(this, args);
		}

		public Person Clone()
		{
			return new Person(SynchronizationId);
		}

		public override String ToString()
		{
			return Name;
		}
	}
}
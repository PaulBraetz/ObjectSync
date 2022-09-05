using ObjectSync.Attributes;
using ObjectSync.Synchronization;
using System.ComponentModel;

namespace TestApp
{
	public partial class Person : INotifyPropertyChanging, INotifyPropertyChanged
	{
		public Person(String name) : this(Guid.NewGuid())
		{
			Name = name;
		}
		public Person(Guid id)
		{
			Synchronize(id);
		}

		[Synchronized]
		[GenerateEvents]
		private String? _name;

		public event PropertyChangedEventHandler? PropertyChanged;
		partial void OnPropertyChanged(String propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public event PropertyChangingEventHandler? PropertyChanging;
		partial void OnPropertyChanging(String propertyName)
		{
			PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
		}

		private Guid _id;
		public Guid Id
		{
			get => _id;
			private set
			{
				_id = value;
				SourceInstanceId = _id.ToString();
			}
		}

		[SourceInstanceId]
		private String SourceInstanceId { get; set; }

		[SynchronizationAuthority]
		private ISynchronizationAuthority Authority { get; } = new StaticSynchronizationAuthority();

		public override String ToString()
		{
			return $"Name: {_name},\tSource: {SourceInstanceId.Substring(0, 3)},\tSynchronized: {GetSynchronizationContext().IsSynchronized}";
		}

		public void Desynchronize()
		{
			GetSynchronizationContext().Desynchronize();
		}
		public void Synchronize(Guid id)
		{
			Id = id;

			GetSynchronizationContext().Synchronize();
		}
	}
}
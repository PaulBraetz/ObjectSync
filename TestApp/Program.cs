using ObjectSync.Attributes;
using ObjectSync.Synchronization;
using System.ComponentModel;

namespace TestApp
{
	public class Program:INotifyPropertyChanged, INotifyPropertyChanging
	{
		public event PropertyChangingEventHandler? PropertyChanging;
		public event PropertyChangedEventHandler? PropertyChanged;

		static void Main(string[] args)
		{
			using(var instance = T.MySynchronizedObject.CreateSynchronized())
			{
				instance.Synchronized_synchronizedField = "Some Value";
			}
		}
	}

	internal sealed partial class T
	{
		public partial class MySynchronizedObject : IDisposable
		{
			private const string name = "SynchronizedProperty";

			[Synchronized(name)]
			private String _synchronizedField = String.Empty;
			private Boolean disposedValue;

			[SynchronizationAuthority]
			private MySynchronizationAuthority SynchronizationAuthority { get; } = new();

			public static MySynchronizedObject CreateSynchronized()
			{
				var instance = new MySynchronizedObject();
				instance.Synchronize();
				return instance;
			}
		}
	}

	public sealed class MySynchronizationAuthority : SynchronizationAuthorityBase
	{
		public override void Push<TProperty>(String objectId, String propertyName, TProperty value)
        {
            Console.WriteLine($"Pushing:\n\tObject: {objectId}\n\tProperty: {propertyName}\n\t");
        }

		public override void Subscribe<TProperty>(String objectId, String propertyName, Action<TProperty> callback)
        {
            Console.WriteLine($"Subscribing:\n\tObject: {objectId}\n\tProperty: {propertyName}");
        }

		public override void Unsubscribe(String objectId, String propertyName)
        {
            Console.WriteLine($"Unsubscribing:\n\tObject: {objectId}\n\tProperty: {propertyName}");
        }
	}
}
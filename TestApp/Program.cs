using ObjectSync.Attributes;
using ObjectSync.Synchronization;
using System.ComponentModel;

namespace TestApp
{
	public partial class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Hello, World!");
		}
	}

	public partial class MySynchronizedObject:IDisposable
	{
		private const string name = "SynchronizedProperty";

		[Synchronized(name)]
		private String _synchronizedField = String.Empty;
		private Boolean disposedValue;

		[SynchronizationAuthority]
		private MySynchronizationAuthority SynchronizationAuthority { get; } = new();

	}

	public sealed class MySynchronizationAuthority : SynchronizationAuthorityBase
	{
		public override void Push<TProperty>(String objectId, String propertyId, TProperty value)
        {
            Console.WriteLine($"Pushing:\n\tObject: {objectId}\n\tProperty: {propertyId}\n\t");
        }

		public override void Subscribe<TProperty>(String objectId, String propertyId, Action<TProperty> callback)
        {
            Console.WriteLine($"Subscribing:\n\tObject: {objectId}\n\tProperty: {propertyId}");
        }

		public override void Unsubscribe(String objectId, String propertyId)
        {
            Console.WriteLine($"Unsubscribing:\n\tObject: {objectId}\n\tProperty: {propertyId}");
        }
	}
}
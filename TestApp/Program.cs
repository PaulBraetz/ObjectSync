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

	public partial class MySynchronizedObject
	{
		[SynchronizedField]
		private String _synchronizedField = String.Empty;
		[SynchronizationAuthority]
		private MySynchronizationAuthority _synchronizationAuthority = new();
	}

	public sealed class MySynchronizationAuthority : SynchronizationAuthorityBase
	{
		public override void Synchronize<TProperty>(String objectKey, String propertyKey, Action<TProperty> callback)
		{
			Console.WriteLine($"Synchronizing:\n\tObject: {objectKey}\n\tProperty: {propertyKey}");
		}
	}
}
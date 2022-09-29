using Fort;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestApp.Data.AnotherNamespace;

namespace TestApp.Commands
{
	internal sealed class SyncPerson : PersonCommandBase
	{
		public SyncPerson(ICollection<Person> people, String navigationKey) : base("Sync Person", navigationKey, people)
		{
		}

		public override void Run()
		{
			var name = Read("Enter person to sync: ");
			var sourceName = Read("Enter source person to sync to: ");

			var p = People.SingleOrDefault(p => p.Name == name);
			p.ThrowIfDefault("person");

			var source = People.SingleOrDefault(p => p.Name == sourceName);
			source.ThrowIfDefault("source");


			p!.SynchronizeTo(source!);
			Console.WriteLine($"Synchronized {name} to {sourceName}");
		}
	}
}

using Scli.Command;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestApp.Data.AnotherNamespace;

namespace TestApp.Commands
{
	internal sealed class CreatePerson : PersonCommandBase
	{
		public CreatePerson(ICollection<Person> people, String navigationKey) : base("Create Person", navigationKey, people)
		{
		}

		public override void Run()
		{
			var name = Read("Enter name: ");
			var newPerson = new Person(name);
			People.Add(newPerson);
			Console.WriteLine($"Created Person: {newPerson}");
		}
	}
}

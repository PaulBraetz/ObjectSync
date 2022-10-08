using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestApp.Data.AnotherNamespace;

namespace TestApp.Commands
{
	internal sealed class DesyncPerson : PersonCommandBase
	{
		public DesyncPerson(ICollection<Person> people, String navigationKey) : base("Desync Person", navigationKey, people)
		{
		}

		public override void Run()
		{
			var id = Read("Enter id:");
			var person = People.Single(p => p.InstanceId.StartsWith(id));

			person.Desynchronize();
		}
	}
}

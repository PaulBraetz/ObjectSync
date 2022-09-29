using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestApp.Data.AnotherNamespace;

namespace TestApp.Commands
{
	internal sealed class ListPeople : PersonCommandBase
	{
		public ListPeople(ICollection<Person> people, String navigationKey) : base("List People", navigationKey, people)
		{
		}

		public override void Run()
		{
			Console.WriteLine($"[{String.Join(", ", People.Select(p => $"({p})"))}]");
		}
	}
}

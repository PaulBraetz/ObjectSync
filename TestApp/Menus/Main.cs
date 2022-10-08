using Scli.Command;
using Scli.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestApp.Commands;
using TestApp.Data.AnotherNamespace;

namespace TestApp.Menus
{
	internal sealed class Main : ExitableMenuBase
	{
		public Main() : base("ObjectSync Demonstrator", "Exit")
		{
			var people = new List<Person>()
			{
				new Person("Jake"),
				new Person("Mary"),
				new Person("Walther"),
				new Person("Jane")
			};

			new CommandCollectionBuilder<IMenu>()
			   .Append(Children)
			   .Build(out var children)
			   .Next<ICommand>()
			   .Append(Actions)
			   .Append(k => new ListPeople(people, k))
			   .Append(k => new CreatePerson(people, k))
			   .Append(k => new SyncPerson(people, k))
			   .Append(k => new SetName(people, k))
			   .Append(k => new DesyncPerson(people, k))
			   .Build(out var actions);

			Children = children;
			Actions = actions;
		}
	}
}

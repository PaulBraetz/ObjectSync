using Scli.Command;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestApp.Data.AnotherNamespace;

namespace TestApp.Commands
{
	internal abstract class PersonCommandBase : CommandBase
	{
		protected PersonCommandBase(String name, String navigationKey, ICollection<Person> people) : base(name)
		{
			People = people;
			NavigationKey = navigationKey;
		}

		protected ICollection<Person> People { get; }
	}
}

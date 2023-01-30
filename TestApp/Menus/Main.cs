using Scli.Command;
using Scli.Menu;

using TestApp.Commands;

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

            _ = new CommandCollectionBuilder<IMenu>()
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
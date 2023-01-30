using Scli.Command;

namespace TestApp.Commands
{
    internal abstract class PersonCommandBase : CommandBase
    {
        protected PersonCommandBase(String name, String navigationKey, ICollection<Person> people) : base(name)
        {
            People = people;
            NavigationKey = navigationKey;
        }

        protected ICollection<Person> People
        {
            get;
        }
    }
}
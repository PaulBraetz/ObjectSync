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
        }
    }
}
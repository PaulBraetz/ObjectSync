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
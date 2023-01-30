namespace TestApp.Commands
{
    internal sealed class SyncPerson : PersonCommandBase
    {
        public SyncPerson(ICollection<Person> people, String navigationKey) : base("Sync Person", navigationKey, people)
        {
        }

        public override void Run()
        {
            var id = Read("Enter id: ");
            var p = People.Single(p => p.InstanceId.StartsWith(id));

            var sourceId = Read("Enter source id: ");
            var source = People.Single(p => p.InstanceId.StartsWith(sourceId));

            p!.SynchronizeTo(source!);
        }
    }
}
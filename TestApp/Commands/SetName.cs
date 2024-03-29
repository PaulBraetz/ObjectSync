﻿namespace TestApp.Commands
{
    internal sealed class SetName : PersonCommandBase
    {
        public SetName(ICollection<Person> people, String navigationKey) : base("Set Name", navigationKey, people)
        {
        }

        public override void Run()
        {
            var id = Read("Enter id:");
            var person = base.People.Single(p => p.InstanceId.StartsWith(id));

            var name = Read("Enter new name:");

            person.Name = name;
        }
    }
}
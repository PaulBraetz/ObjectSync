using RhoMicro.ObjectSync.Attributes;

namespace TestApp
{
    [SynchronizationTarget(
        ContextTypeAccessibility = Accessibility.Protected,
        BaseContextTypeName = nameof(PersonBase.PersonBaseSynchronizationContext),
        ContextTypeIsSealed = true,
        ContextPropertyAccessibility = Accessibility.Protected,
        ContextPropertyModifier = Modifier.Override)]
    internal sealed partial class Person : PersonBase
    {
        public Person(String name)
        {
            Name = name;
        }

        public Person(Person person)
        {
            SynchronizeTo(person);
        }

        [Synchronized(Observable = true)]
        private String? _name;
        [Synchronized]
        private Byte _age;

        public override String ToString() => $"{InstanceId}->{String.Concat(SourceInstanceId.Take(2))}-{Name}";

        public void Desynchronize()
        {
            SynchronizationContext.Desynchronize();
            SourceInstanceId = Guid.NewGuid().ToString();
        }
        public void SynchronizeTo(Person person) => SynchronizeTo(person.SourceInstanceId);
        /// <summary>
        /// For <paramref name="id"/>
        /// </summary>
        /// <param name="id"></param>
        private void SynchronizeTo(String id)
        {
            SynchronizationContext.Desynchronize();
            SourceInstanceId = id;
            SynchronizationContext.Synchronize();
        }
    }
}
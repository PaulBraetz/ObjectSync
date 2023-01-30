using RhoMicro.ObjectSync.Attributes;

using SomeExternalAssembly.ObjectSync.Synchronization;

namespace TestApp
{
    [SynchronizationTarget(
        ContextTypeAccessibility = Accessibility.Protected,
        ContextTypeIsSealed = false,
        ContextPropertyAccessibility = Accessibility.Protected,
        ContextPropertyModifier = Modifier.Virtual)]
    internal abstract partial class PersonBase
    {
        public PersonBase()
        {
            _instanceCount++;
            InstanceId = _instanceCount.ToString();
            SynchronizationContext.Synchronize();
        }

        private static Int32 _instanceCount;

        [SynchronizationAuthority]
        protected ISynchronizationAuthority Authority { get; } = new MySynchronizationAuthority();

        [InstanceId]
        public String InstanceId
        {
            get;
        }

        [TypeId]
        private static String TypeId { get; } = "PersonType";

        [SourceInstanceId]
        protected String SourceInstanceId { get; set; } = Guid.NewGuid().ToString();
    }
}
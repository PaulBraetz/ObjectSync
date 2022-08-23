using System;
using System.Collections.Generic;

namespace ObjectSync.Synchronization
{
    public delegate void PropertySynchronizationHandler<TProperty>(PropertySynchronizationArg<TProperty> arg);
    public readonly struct PropertySynchronizationArg<TProperty>
    {
        public readonly DateTimeOffset Timestamp;
        public readonly TProperty Value;
    }
}

namespace TestApp
{
    public partial class MySynchronizedObject : IDisposable
    {
        private String __SynchronizationId { get; } = Guid.NewGuid().ToString();

        public System.String Synchronized__synchronizedField
        {
            get
            {
                return _synchronizedField;
            }
            set
            {
                _synchronizedField = value;
                SynchronizationAuthority.Push<System.String>(__SynchronizationId, "Synchronized__synchronizedField", value);
            }
        }


        partial void DisposeManagedResources();
        partial void DisposeUnmanagedResources();

        private void Dispose(Boolean disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    DisposeManagedResources();
                }

                DisposeUnmanagedResources();
                disposedValue = true;
            }
        }

        ~MySynchronizedObject()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
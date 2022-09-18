namespace ObjectSync.Synchronization
{
	public readonly struct SyncInfo : IEquatable<SyncInfo>
	{
		public readonly String TypeId;
		public readonly String PropertyName;
		public readonly String SourceInstanceId;
		public readonly String InstanceId;

		public readonly String PropertyStateId;

		public SyncInfo(String typeId, String propertyName, String sourceInstanceId, String instanceId) : this()
		{
			TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
			PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
			SourceInstanceId = sourceInstanceId ?? throw new ArgumentNullException(nameof(sourceInstanceId));
			InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));

			PropertyStateId = $"{TypeId}.{PropertyName}[{SourceInstanceId}]";
		}

		public override Boolean Equals(Object? obj)
		{
			return obj is SyncInfo info && Equals(info);
		}

		public Boolean Equals(SyncInfo other)
		{
			return InstanceId == other.InstanceId &&
				   PropertyStateId == other.PropertyStateId;
		}

		public override Int32 GetHashCode()
		{
			return HashCode.Combine(InstanceId, PropertyStateId);
		}

		public static Boolean operator ==(SyncInfo left, SyncInfo right)
		{
			return left.Equals(right);
		}

		public static Boolean operator !=(SyncInfo left, SyncInfo right)
		{
			return !(left == right);
		}
	}
}

using System;
using System.Collections;
using System.Collections.Generic;

namespace ObjectSync.Synchronization
{
	public interface ISynchronizationAuthority
	{
		TProperty Pull<TProperty>(String synchronizationId, String propertyName, String instanceId);
		void Push<TProperty>(String synchronizationId, String propertyName, String instanceId, TProperty value);
		void Subscribe<TProperty>(String synchronizationId, String propertyName, String instanceId, Action<TProperty> callback);
		void Unsubscribe(String synchronizationId, String propertyName, String instanceId);
	}
}

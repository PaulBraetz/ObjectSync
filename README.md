# ObjectSync #

ObjectSync is a C# analyzer that generates code for synchronizing properties.

## Features ##

* Generation of Synchronized Properties from annotated fields
* Threadsafe Synchronization of Properties
* Pre-Built Static Synchronization Authority for synchronizing inside a single Appdomain

## Versioning ##

The most recent version of ObjectSync is 3.0.0.
ObjectSync uses [Semantic Versioning 2.0.0](https://semver.org/).
## Installation ##

Nuget Gallery: https://www.nuget.org/packages/RhoMicro.ObjectSync

Package Manager: `Install-Package RhoMicro.ObjectSync -Version 3.0.0`

.Net CLI: `dotnet add package RhoMicro.ObjectSync --version 3.0.0`
## Quick Start ##

ObjectSync works by generating a partial class for types annotated with the `SynchronizationTarget` attribute. It will generate a nested context type that manages synchronization state and logic for instances of your type. 

```cs
using ObjectSync.Attributes;
using ObjectSync.Synchronization;

[SynchronizationTarget]
internal partial class MyType
{

}
```

Your type must provide a synchronization authority property for use by this context. Synchronization authorities are responsible for communication between contexts. As such, they could enable synchronization across the internet, among select clients, or inside a single Appdomain. A static synchronization authority has been provided for synchronizing properties inside a single app domain. Access it using `StaticSynchronizationAuthority.Instance`. 

```cs
using ObjectSync.Attributes;
using ObjectSync.Synchronization;

[SynchronizationTarget]
internal partial class MyType
{
  [SynchronizationAuthorityAttribute]
  private ISynchronizationAuthority Authority { get; } = StaticSynchronizationAuthority.Instance;
}
```

Now to actually define the data to be synchronized, annotate fields with the `Synchronized` attribute. This will instruct ObjectSync to generate a property based on this backing field that synchronizes its state using the types synchronization context.

```cs
using ObjectSync.Attributes;
using ObjectSync.Synchronization;

[SynchronizationTarget]
internal partial class MyType
{
  [SynchronizationAuthorityAttribute]
  private ISynchronizationAuthority Authority { get; } = StaticSynchronizationAuthority.Instance;
  
  [Synchronized]
  private String _synchronizedValue;
}
```

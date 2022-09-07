# ObjectSync.Synchronization #

This project contains Synchronization Authority Types for ObjectSync.
These types are intended to provide semantic compatibility with the ObjectSync.Generator's requirements.

## Features ##

* Synchronization authority interface and base type
* Static authority for synchronization inside an AppDomain

## Versioning ##

ObjectSync.Synchronization uses [Semantic Versioning 2.0.0](https://semver.org/).

## Installation ##

Nuget Gallery: https://www.nuget.org/packages/RhoMicro.ObjectSync.Synchronization

Package Manager: `Install-Package RhoMicro.ObjectSync.Synchronization -Version 1.0.0`

.Net CLI: `dotnet add package RhoMicro.ObjectSync.Synchronization --version 1.0.0`

## How To Use ##

Simply inherit the `ISynchronizationAuthority` and implement your synchronization scenario.
Inherit from `SynchronizationAuthorityBase` for simplified method signatures using the `SyncInfo` type.

### Using StaticSynchronizationAuthority ###

`StaticSynchronizationAuthority` provides facilities for synchronizing instances within an AppDomain.
Access it's instances by accessing `StaticSynchronizationAuthority.Instance`:

'''cs
[SynchronizationAuthority]
private ISynchronizationAuthority Authority { get; } = StaticSynchronizationAuthority.Instance;
'''

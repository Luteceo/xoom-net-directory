// Copyright © 2012-2022 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL
// was not distributed with this file, You can obtain
// one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vlingo.Xoom.Actors.TestKit;
using Vlingo.Xoom.Common;
using Vlingo.Xoom.Directory.Client;
using Vlingo.Xoom.Directory.Model;
using Vlingo.Xoom.Directory.Tests.Client;
using Vlingo.Xoom.Wire.Multicast;
using Vlingo.Xoom.Wire.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Vlingo.Xoom.Directory.Tests.Model;

public class DirectoryServiceTest : IDisposable
{
    private static readonly Random Random = new Random();
    private static readonly AtomicInteger PortToUse = new AtomicInteger(Random.Next(32_768, 60_999));
        
    private readonly TestActor<IDirectoryClient> _client1;
    private readonly TestActor<IDirectoryClient> _client2;
    private readonly TestActor<IDirectoryClient> _client3;
    private readonly TestActor<IDirectoryService> _directory;
    private readonly MockServiceDiscoveryInterest _interest1;
    private readonly MockServiceDiscoveryInterest _interest2;
    private readonly MockServiceDiscoveryInterest _interest3;
    private readonly List<MockServiceDiscoveryInterest> _interests;
    private readonly TestWorld _testWorld;
    private readonly ITestOutputHelper _output;

    [Fact]
    public void TestShouldInformInterest()
    {
        _directory.Actor.Use(new TestAttributesClient());

        // directory assigned leadership
        _directory.Actor.AssignLeadership();

        var location = new Location("test-host", PortToUse.GetAndIncrement());
        var info = new ServiceRegistrationInfo("test-service", new List<Location> { location });

        var accessSafely = _interest1.AfterCompleting(2);
        _client1.Actor.Register(info);
            
        accessSafely.ReadFromExpecting("interestedIn", 1);

        Assert.NotEmpty(_interest1.ServicesSeen);
        Assert.Contains("test-service", _interest1.ServicesSeen);
        Assert.NotEmpty(_interest1.DiscoveredServices);
        Assert.Contains(info, _interest1.DiscoveredServices);
    }

    [Fact]
    public void TestShouldUnregister()
    {
        _directory.Actor.Use(new TestAttributesClient());
        
        // directory assigned leadership
        _directory.Actor.AssignLeadership();
        
        var accessSafely1 = _interest1.AfterCompleting(3);
        var accessSafely2 = _interest2.AfterCompleting(3);
        var accessSafely3 = _interest3.AfterCompleting(3);
        
        var location1 = new Location("test-host1", PortToUse.GetAndIncrement());
        var info1 = new ServiceRegistrationInfo("test-service1", new List<Location> { location1 });
        _client1.Actor.Register(info1);
        
        var location2 = new Location("test-host2", PortToUse.GetAndIncrement());
        var info2 = new ServiceRegistrationInfo("test-service2", new List<Location> { location2 });
        _client2.Actor.Register(info2);
        
        var location3 = new Location("test-host3", PortToUse.GetAndIncrement());
        var info3 = new ServiceRegistrationInfo("test-service3", new List<Location> { location3 });
        _client3.Actor.Register(info3);
        
        accessSafely1.ReadFromExpecting("interestedIn", 3);
        accessSafely2.ReadFromExpecting("interestedIn", 3);
        accessSafely3.ReadFromExpecting("interestedIn", 3);
            
        _client1.Actor.Unregister(info1.Name);
            
        accessSafely1.ReadFromExpecting("informUnregistered", 1);
        accessSafely2.ReadFromExpecting("informUnregistered", 1);
        accessSafely3.ReadFromExpecting("informUnregistered", 1);
        
        foreach (var interest in new List<MockServiceDiscoveryInterest> { _interest2, _interest3 })
        {
            _output.WriteLine($"COUNT: {interest.ServicesSeen.Count + interest.DiscoveredServices.Count + interest.UnregisteredServices.Count}");
            var discoveredServices = interest.DiscoveredServices.ToList();
            Assert.NotEmpty(interest.ServicesSeen);
            Assert.Contains(info1.Name, interest.ServicesSeen);
            Assert.NotEmpty(discoveredServices);
            Assert.Contains(info1, discoveredServices);
            Assert.NotEmpty(interest.UnregisteredServices);
            foreach (var unregisteredService in interest.UnregisteredServices)
            {
                _output.WriteLine(unregisteredService);
            }
            Assert.Contains(info1.Name, interest.UnregisteredServices);
        }
    }
        
    [Fact]
    public void TestShouldNotInformInterest()
    {
        _directory.Actor.Use(new TestAttributesClient());
        
        // directory NOT assigned leadership
        _directory.Actor.RelinquishLeadership(); // actually never had leadership, but be explicit and prove no harm
            
        var location1 = new Location("test-host1", PortToUse.GetAndIncrement());
        var info1 = new ServiceRegistrationInfo("test-service1", new List<Location> { location1 });
        _client1.Actor.Register(info1);
        
        Assert.Empty(_interest1.ServicesSeen);
        Assert.DoesNotContain("test-service", _interest1.ServicesSeen);
        Assert.Empty(_interest1.DiscoveredServices);
        Assert.DoesNotContain(info1, _interest1.DiscoveredServices);
    }
        
    [Fact]
    public void TestAlteredLeadership()
    {
        _directory.Actor.Use(new TestAttributesClient());
            
        // START directory assigned leadership
        _directory.Actor.AssignLeadership();
        
        var accessSafely1 = _interest1.AfterCompleting(6);
        var accessSafely2 = _interest2.AfterCompleting(6);
        var accessSafely3 = _interest3.AfterCompleting(6);
            
        var location1 = new Location("test-host1", PortToUse.GetAndIncrement());
        var info1 = new ServiceRegistrationInfo("test-service1", new List<Location> { location1 });
        _client1.Actor.Register(info1);
        
        var location2 = new Location("test-host2", PortToUse.GetAndIncrement());
        var info2 = new ServiceRegistrationInfo("test-service2", new List<Location> { location2 });
        _client2.Actor.Register(info2);
        
        var location3 = new Location("test-host3", PortToUse.GetAndIncrement());
        var info3 = new ServiceRegistrationInfo("test-service3", new List<Location> { location3 });
        _client3.Actor.Register(info3);
        
        accessSafely1.ReadFromExpecting("interestedIn", 3, 50_000);
        accessSafely2.ReadFromExpecting("interestedIn", 3, 50_000);
        accessSafely3.ReadFromExpecting("interestedIn", 3, 50_000);
            
        accessSafely1.ReadFromExpecting("informDiscovered", 3, 50_000);
        accessSafely2.ReadFromExpecting("informDiscovered", 3, 50_000);
        accessSafely3.ReadFromExpecting("informDiscovered", 3, 50_000);
            
        foreach (var interest in _interests)
        {
            var discoveredServices = interest.DiscoveredServices.ToList();
            Assert.NotEmpty(interest.ServicesSeen);
            Assert.Contains("test-service1", interest.ServicesSeen);
            Assert.Contains("test-service2", interest.ServicesSeen);
            Assert.Contains("test-service3", interest.ServicesSeen);
            Assert.NotEmpty(discoveredServices);
            Assert.Contains(info1, discoveredServices);
            Assert.Contains(info2, discoveredServices);
            Assert.Contains(info3, discoveredServices);
        }
        
        // ALTER directory relinquished leadership
        _directory.Actor.RelinquishLeadership();
        
        foreach (var interest in _interests)
        {
            interest.ServicesSeen.Clear();
            interest.DiscoveredServices.Clear();
        }
        
        foreach (var interest in _interests)
        {
            Assert.Empty(interest.ServicesSeen);
            Assert.DoesNotContain("test-service1", interest.ServicesSeen);
            Assert.DoesNotContain("test-service2", interest.ServicesSeen);
            Assert.DoesNotContain("test-service3", interest.ServicesSeen);
            Assert.Empty(interest.DiscoveredServices);
            Assert.DoesNotContain(info1, interest.DiscoveredServices);
            Assert.DoesNotContain(info2, interest.DiscoveredServices);
            Assert.DoesNotContain(info3, interest.DiscoveredServices);
        }
        
        // ALTER directory assigned leadership
        _directory.Actor.AssignLeadership();
        
        foreach (var interest in _interests)
        {
            interest.ServicesSeen.Clear();
            interest.DiscoveredServices.Clear();
        }
            
        accessSafely1 = _interest1.AfterCompleting(3);
        accessSafely2 = _interest2.AfterCompleting(3);
        accessSafely3 = _interest3.AfterCompleting(3);
            
        accessSafely1.ReadFromExpecting("interestedIn", 3, 50_000);
        accessSafely2.ReadFromExpecting("interestedIn", 3, 50_000);
        accessSafely3.ReadFromExpecting("interestedIn", 3, 50_000);
        
        accessSafely1.ReadFromExpecting("informDiscovered", 3, 50_000);
        accessSafely2.ReadFromExpecting("informDiscovered", 3, 50_000);
        accessSafely3.ReadFromExpecting("informDiscovered", 3, 50_000);
        
        foreach (var interest in _interests)
        {
            Assert.NotEmpty(interest.ServicesSeen);
            Assert.Contains("test-service1", interest.ServicesSeen);
            Assert.Contains("test-service2", interest.ServicesSeen);
            Assert.Contains("test-service3", interest.ServicesSeen);
            Assert.NotEmpty(interest.DiscoveredServices);
            Assert.Contains(info1, interest.DiscoveredServices);
            Assert.Contains(info2, interest.DiscoveredServices);
            Assert.Contains(info3, interest.DiscoveredServices);
        }
    }

    [Fact]
    public void TestRegisterDiscoverMultiple()
    {
        _directory.Actor.Use(new TestAttributesClient());
        _directory.Actor.AssignLeadership();
            
        var accessSafely1 = _interest1.AfterCompleting(6);
        var accessSafely2 = _interest2.AfterCompleting(6);
        var accessSafely3 = _interest3.AfterCompleting(6);
            
        var location1 = new Location("test-host1", PortToUse.GetAndIncrement());
        var info1 = new ServiceRegistrationInfo("test-service1", new List<Location> {location1});
        _client1.Actor.Register(info1);
            
        var location2 = new Location("test-host2", PortToUse.GetAndIncrement());
        var info2 = new ServiceRegistrationInfo("test-service2", new List<Location> {location2});
        _client2.Actor.Register(info2);
            
        var location3 = new Location("test-host3", PortToUse.GetAndIncrement());
        var info3 = new ServiceRegistrationInfo("test-service3", new List<Location> {location3});
        _client3.Actor.Register(info3);

        accessSafely1.ReadFromExpecting("interestedIn", 3, 10_000);
        accessSafely2.ReadFromExpecting("interestedIn", 3, 10_000);
        accessSafely3.ReadFromExpecting("interestedIn", 3, 10_000);
            
        accessSafely1.ReadFromExpecting("informDiscovered", 3, 10_000);
        accessSafely2.ReadFromExpecting("informDiscovered", 3, 10_000);
        accessSafely3.ReadFromExpecting("informDiscovered", 3, 10_000);
            
        foreach (var interest in _interests)
        {
            Assert.NotNull(interest.ServicesSeen);
            Assert.Contains("test-service1", interest.ServicesSeen);
            Assert.Contains("test-service2", interest.ServicesSeen);
            Assert.Contains("test-service3", interest.ServicesSeen);
            Assert.NotEmpty(interest.DiscoveredServices);
            Assert.Contains(info1, interest.DiscoveredServices);
            Assert.Contains(info2, interest.DiscoveredServices);
            Assert.Contains(info3, interest.DiscoveredServices);
        }
    }

    public DirectoryServiceTest(ITestOutputHelper output)
    {
        _output = output;
        var converter = new Converter(output);
        if (!Debugger.IsAttached)
        {
            Console.SetOut(converter);
        }

        _testWorld = TestWorld.Start("test");

        var operationalPort = PortToUse.GetAndIncrement();
        var applicationPort = PortToUse.GetAndIncrement();
        var node = Node.With(Id.Of(1), Name.Of("node1"), Host.Of("localhost"), operationalPort, applicationPort);

        var group = new Group("237.37.37.1", operationalPort);

        var incomingPort = PortToUse.GetAndIncrement();

        _directory = _testWorld.ActorFor<IDirectoryService>(
            () => new DirectoryServiceActor(node, new Network(group, incomingPort), 1024, new Timing(10, 100), 10));

        _interest1 = new MockServiceDiscoveryInterest("interest1", _testWorld.DefaultLogger);
        _client1 = _testWorld.ActorFor<IDirectoryClient>(
            () => new DirectoryClientActor(_interest1, group, 1024, 10, 10));

        _interest2 = new MockServiceDiscoveryInterest("interest2", _testWorld.DefaultLogger);
        _client2 = _testWorld.ActorFor<IDirectoryClient>(
            () => new DirectoryClientActor(_interest2, group, 1024, 10, 10));
            
        _interest3 = new MockServiceDiscoveryInterest("interest3", _testWorld.DefaultLogger);
        _client3 = _testWorld.ActorFor<IDirectoryClient>(
            () => new DirectoryClientActor(_interest3, group, 1024, 10, 10));

        var testAddress = Address.From(Host.Of("localhost"), incomingPort, AddressType.Main);
        ((DirectoryClientActor)_client1.ActorInside).TestSetDirectoryAddress(testAddress);
        ((DirectoryClientActor)_client2.ActorInside).TestSetDirectoryAddress(testAddress);
        ((DirectoryClientActor)_client3.ActorInside).TestSetDirectoryAddress(testAddress);

        _interests = new List<MockServiceDiscoveryInterest> { _interest1, _interest2, _interest3 };
    }

    public void Dispose()
    {
        _directory.Actor.Stop();
        _client1.Actor.Stop();
        _client2.Actor.Stop();
        _client3.Actor.Stop();
        _testWorld.Terminate();
    }
}
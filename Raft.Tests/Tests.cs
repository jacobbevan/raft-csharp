using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Raft.Audit;
using Raft.Proxies;
using Raft.Server;
using Serilog;
using static Raft.Proxies.ProxyWithPartitions;

namespace Tests
{
    [TestFixture]
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
           Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File("testLog.log")
                .CreateLogger();            
        }

        [Test]
        public void become_follower_updates_term()
        {
            var audit = new Mock<IAuditLog>();
            var planner = new Mock<IPlanner>();
            using(var server = new RaftServer("a", audit.Object, planner.Object))
            {
                server.BecomeFollower(2);
                Assert.AreEqual(2, server.CurrentTerm);
            }
        }

        [Test]
        public void become_follower_with_term_reduction_is_fatal()
        {

            
            var audit = new Mock<IAuditLog>();
            var planner = new Mock<IPlanner>();
            using(var server = new RaftServer("a", audit.Object, planner.Object))
            {
                server.BecomeFollower(2);
                Assert.That(() => server.BecomeFollower(1), Throws.Exception);
                
            }
        }

        [Test]
        public void become_follower_leads_to_become_candidate()
        {

            
            var audit = new Mock<IAuditLog>();
            var planner = new Mock<IPlanner>();
            using(var server = new RaftServer("a", audit.Object, planner.Object))
            {
                var t = server.BecomeFollower(2);
                t.Wait(2000);
                Assert.AreEqual(1,1);                
            }
        }


        [Test]
        public void partition_integration_test()
        {
                        var auditLog = new SimpleAuditLog();
            var planner = new Planner(1000, 900, 200, 200);

            var partitionConfig = new PartitionConfiguration(
                new Dictionary<string,int> 
                {
                    {"a",1},
                    {"b",1},
                    {"c",1},
                    {"d",1},
                    {"e",1},
                    {"f",2},
                    {"g",2},
                },
                false
            );

            var servers = new List<IServer>
            {
                new RaftServer("a", auditLog, planner),
                new RaftServer("b", auditLog, planner),
                new RaftServer("c", auditLog, planner),
                new RaftServer("e", auditLog, planner),
                new RaftServer("f", auditLog, planner),
                new RaftServer("g", auditLog, planner),
            };


            

            foreach(var server in servers)
            {
                server.Initialise(servers.Where(s=>s.Id != server.Id).Select(s=> new ProxyWithPartitions(new ReliableProxy(s,100), partitionConfig)));
            }

            System.Threading.Thread.Sleep(5000);
            partitionConfig.Partition();
            System.Threading.Thread.Sleep(5000);
            partitionConfig.Heal();
            System.Threading.Thread.Sleep(5000);
        }
    }
}
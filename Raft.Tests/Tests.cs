using System.Collections.Generic;
using System.Linq;
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
        public void Test1()
        {

            var audit = new Mock<IAuditLog>();
            var planner = new Mock<IPlanner>();
            var server = new RaftServer("a", audit.Object, planner.Object);
            Log.Logger.Information("hi mum");
            System.Console.WriteLine("hi hi");


        }


        [Test]
        public void temporary_integration_test()
        {
                        var auditLog = new SimpleAuditLog();
            var planner = new Planner(1000, 900, 200);

            var partitionConfig = new PartitionConfiguration(
                new Dictionary<string,int> 
                {
                    {"a",1},
                    {"b",1},
                    {"c",1},
                    {"d",1},
                    {"e",2},
                    {"f",2},
                    {"g",2},
                    {"h",2},
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
                new RaftServer("h", auditLog, planner),

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
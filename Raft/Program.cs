﻿using System.Collections.Generic;
using System.Linq;
using Raft.Audit;
using Raft.Proxies;
using Raft.Server;
using static Raft.Proxies.ProxyWithPartitions;

namespace Raft
{
    class Program
    {
        static void Main(string[] args)
        {
            var auditLog = new SimpleAuditLog();
            var planner = new Planner(1000, 900, 200, 50);

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

            System.Threading.Thread.Sleep(10000);
            partitionConfig.Partition();
            System.Threading.Thread.Sleep(10000);
            partitionConfig.Heal();
            System.Threading.Thread.Sleep(10000);
        }
    }






}
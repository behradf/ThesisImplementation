﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using Implementation.Data_Structures;
using MathNet.Numerics.Distributions;

namespace Implementation.Dataset_Reader
{
    public class DistDataFeed : IDataFeed
    {
        private readonly DistDataParams _distDataParams;
        private readonly Normal _maxGenerator;
        private readonly Normal _normalRandomGenerator;

        //public DistDataFeed()
        //{
        //    _rand = new Random();
        //    _capmean = 20;
        //    _capstddev = 10;
        //}

        public DistDataFeed(DistDataParams distDataParams)
        {
            _distDataParams = distDataParams;
            var rand = new Random();
            _maxGenerator = Normal.WithMeanVariance(_distDataParams.CapacityMean, _distDataParams.CapacityVariance, rand);
            _normalRandomGenerator = Normal.WithMeanVariance(1.5, 3, rand);
        }

        private Graph GenerateEventGraph(int userNumber, int eventNumber)
        {
            var graph = new Graph(userNumber);
            var rand = new Random();
            for (int i = 0; i < userNumber; i++)
            {
                graph.Edges.Add(i, new List<int>(eventNumber));
                for (int j = 0; j < eventNumber; j++)
                {
                    if (rand.Next(1, 101) <= _distDataParams.EventInterestPerct)
                    {
                        graph.Edges[i].Add(j);
                    }
                }
            }
            return graph;
        }

        private Graph GenerateSocialGraph(int userCount)
        {
            switch (_distDataParams.SocialNetworkModel)
            {
                case SocialNetworkModel.PowerLawModel:
                    return PowerLawModel(userCount);
                case SocialNetworkModel.ErdosModel:
                    return ErdosModel(userCount);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private Graph ErdosModel(int userCount)
        {
            Graph graph = new Graph();
            var rand = new Random();
            for (int nodeA = 0; nodeA < userCount; nodeA++)
            {
                for (int nodeB = 0; nodeB < userCount; nodeB++)
                {
                    if (nodeA != nodeB)
                    {
                        if (rand.NextDouble() <= _distDataParams.SocialNetworkDensity)
                        {
                            if (!graph.Edges.ContainsKey(nodeA))
                            {
                                graph.Edges.Add(nodeA, new List<int>());
                            }
                            if (!graph.Edges.ContainsKey(nodeB))
                            {
                                graph.Edges.Add(nodeB, new List<int>());
                            }
                            graph.Edges[nodeA].Add(nodeB);
                            graph.Edges[nodeB].Add(nodeA);
                        }
                    }
                }
            }

            return graph;
        }

        private static Graph PowerLawModel(int userCount)
        {
            List<string> lines;
            using (WebClient client = new WebClient())
            {
                var ip = ConfigurationManager.AppSettings["IP"];
                var csv = client.DownloadString(ip + $"/getgraph/{userCount}");
                lines = csv.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            //var lines = File.ReadAllLines("graph.csv");

            Graph graph = new Graph(lines.Count);

            foreach (var line in lines)
            {
                var edge = line.Split(new[] { ',' });
                int nodeA = Convert.ToInt32(edge[0]) - 1;
                int nodeB = Convert.ToInt32(edge[1]) - 1;
                if (!graph.Edges.ContainsKey(nodeA))
                {
                    graph.Edges.Add(nodeA, new List<int>());
                }
                //if (!graph.Edges.ContainsKey(nodeB))
                //{
                //    graph.Edges.Add(nodeB, new List<int>());
                //}
                graph.Edges[nodeA].Add(nodeB);
                //graph.Edges[nodeB].Add(nodeA);
            }
            return graph;
        }

        public List<Cardinality> GenerateCapacity(List<int> users, List<int> events)
        {
            var result = events.Select(x =>
            {
                var end = GenerateMaxCapacity(1);
                var start = GenerateMinCapacity(1, end);
                var c = new Cardinality
                {
                    Min = start,
                    Max = end
                };
                return c;
            }).ToList();

            return result;
        }

        private int GenerateMinCapacity(int min, int max)
        {
            var ground = min;
            var rand = new Random();
            switch (_distDataParams.MinCardinalityOption)
            {
                case MinCardinalityOptions.Half:
                    ground = Convert.ToInt32(Math.Floor((double)max / 2));
                    break;
                case MinCardinalityOptions.Fourth:
                    ground = max - Convert.ToInt32(Math.Floor((double)max / 4));
                    break;
                case MinCardinalityOptions.Eighth:
                    ground = max - Convert.ToInt32(Math.Floor((double)max / 8));
                    break;
                case MinCardinalityOptions.Random:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return rand.Next(ground, max);
        }

        public List<List<double>> GenerateInnateAffinities(List<int> users, List<int> events)
        {
            var usersInterests = new List<List<double>>();
            var eventGraph = GenerateEventGraph(users.Count, events.Count);
            foreach (var user in users)
            {
                var userInterests = new List<double>();
                foreach (var @event in events)
                {
                    if (eventGraph.Edges[user].Contains(@event))
                    {
                        //double r = 1.0 / Math.Pow(1 - _rand.NextDouble(), 1.5);
                        //r = Math.Round(r, 2);
                        userInterests.Add(GenerateNormalRandom(0));
                    }
                    else
                    {
                        userInterests.Add(0);
                    }
                }
                usersInterests.Add(userInterests);
            }
            return usersInterests;
        }

        public double[,] GenerateSocialAffinities(List<int> users)
        {
            var socialNetworkGraph = GenerateSocialGraph(users.Count);
            var usersInterests = new double[users.Count, users.Count];

            foreach (var edge in socialNetworkGraph.Edges)
            {
                var nodeA = edge.Key;
                foreach (var nodeB in edge.Value)
                {
                    var r = GenerateNormalRandom(0);
                    r = Math.Round(r, 2);
                    usersInterests[nodeA, nodeB] = r;
                }
            }

            return usersInterests;
        }

        void IDataFeed.GetNumberOfUsersAndEvents(out int usersCount, out int eventsCount)
        {
            throw new NotImplementedException();
        }

        private double GenerateNormalRandom(double minimum)
        {
            var normalDist = _normalRandomGenerator.Sample();
            return normalDist < Math.Pow(10, -5) ? minimum : normalDist;
        }


        private int GenerateMaxCapacity(int minimum)
        {
            var normalDist = _maxGenerator.Sample();
            var notmalDistInt = Convert.ToInt32(Math.Floor(normalDist));
            return notmalDistInt < minimum ? minimum : notmalDistInt;
        }
    }
}

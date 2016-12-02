﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Implementation.Data_Structures;
using LouvainCommunityPL;
using OfficeOpenXml;

namespace Implementation.Algorithms
{
    public abstract class Algorithm<T>
    {
        public abstract void Run(FileInfo output);
        public abstract void Initialize();
        public Welfare Welfare { get; set; }
        public List<int> AllEvents;
        public List<int> AllUsers;
        public List<Cardinality> EventCapacity;
        public List<List<int>> Assignments;
        public List<int?> UserAssignments;
        public List<List<double>> InAffinities;
        public double[,] SocAffinities;
        public SGConf Conf;
        public Stopwatch _watch;
        protected readonly int _index;
        private readonly ReassignmentStrategy<T> _reassignmentStrategy;
        private readonly PrintOutput<T> _printOutput;
        protected Dictionary<string, UserEvent> DisposeUserEvents;

        protected Algorithm(int index)
        {
            _index = index;
            _watch = new Stopwatch();
            _reassignmentStrategy = new ReassignmentStrategy<T>(this);
            _printOutput = new PrintOutput<T>(this);
        }

        public Stopwatch Execute(FileInfo output)
        {
            _watch.Reset();
            _watch.Start();
            Run(output);
            _watch.Stop();
            return _watch;
        }

        protected void PrintAssignments(bool assignmentMade)
        {
            if (!Conf.PrintOutEachStep)
            {
                return;
            }

            if (!assignmentMade)
            {
                Console.WriteLine("No assignment made.");
            }
            for (int i = 0; i < Assignments.Count; i++)
            {
                Console.WriteLine();
                Console.Write("Event {0}", (char) (i + 88));
                var assignment = Assignments[i];
                if (assignment.Count == 0)
                {
                    Console.Write(" is empty.");
                    continue;
                }
                Console.Write(" contains ");
                foreach (var user in assignment)
                {
                    Console.Write("{0}  ", (char) (user + 97));
                }
            }
            PrintQueue();
            Console.WriteLine("{0}{0}*****************************", Environment.NewLine);
            Console.ReadLine();
        }

        protected abstract void PrintQueue();

        public List<UserEvent> CreateOutput(FileInfo file)
        {
            SetInputFile(file);
            var result = new List<UserEvent>();
            for (int i = 0; i < UserAssignments.Count; i++)
            {
                var userAssignment = UserAssignments[i];
                result.Add(new UserEvent
                {
                    Event = userAssignment ?? -1,
                    User = i
                });
            }
            Welfare = CalculateSocialWelfare(Assignments);
            Print(result, Welfare, file);
            return result;
        }

        private void Print(List<UserEvent> result, Welfare welfare, FileInfo file)
        {
            switch (Conf.OutputType)
            {
                case OutputTypeEnum.Excel:
                    _printOutput.PrintToExcel(result, Welfare, file);
                    break;
                case OutputTypeEnum.Text:
                    _printOutput.PrintToText(result, Welfare, file);
                    break;
                case OutputTypeEnum.None:
                    break;
            }
        }

        protected void DefaultReassign()
        {
            if (Conf.Reassignment != AlgorithmSpec.ReassignmentEnum.Default)
                return;

            for (int i = 0; i < UserAssignments.Count; i++)
            {
                if (UserAssignments[i] != null && !EventIsReal(UserAssignments[i].Value, Assignments[UserAssignments[i].Value]))
                {
                    UserAssignments[i] = null;
                }
            }

            if (UserAssignments.Any(x => !x.HasValue))
            {
                List<int> availableUsers;
                List<int> realOpenEvents;
                PrepareReassignment(out availableUsers, out realOpenEvents);
                RefillQueue(realOpenEvents, availableUsers);
            }
        }

        protected void Reassign()
        {
            if (Conf.Reassignment == AlgorithmSpec.ReassignmentEnum.None
                || Conf.Reassignment == AlgorithmSpec.ReassignmentEnum.Default
                || Conf.Reassignment == AlgorithmSpec.ReassignmentEnum.Greedy)
                return;

            for (int i = 0; i < UserAssignments.Count; i++)
            {
                if (UserAssignments[i] != null && !EventIsReal(UserAssignments[i].Value, Assignments[UserAssignments[i].Value]))
                {
                    UserAssignments[i] = null;
                }
            }

            if (UserAssignments.All(x => x.HasValue))
            {
                return;
            }

            List<int> realOpenEvents;
            List<int> availableUsers;
            PrepareReassignment(out availableUsers, out realOpenEvents);
            KeepPhantomEvents(availableUsers, realOpenEvents, Conf.Reassignment);
            RefillQueue(realOpenEvents, availableUsers);
        }

        protected Welfare GetUserWelfare(UserEvent userEvent, List<int> assignment)
        {
            var welfare = new Welfare();
            welfare.InnateWelfare += InAffinities[userEvent.User][userEvent.Event];
            foreach (var user2 in assignment)
            {
                if (userEvent.User != user2)
                {
                    welfare.SocialWelfare += SocAffinities[userEvent.User, user2];
                }
            }
            welfare.InnateWelfare = (1 - Conf.Alpha)*welfare.InnateWelfare;
            welfare.SocialWelfare = Conf.Alpha*welfare.SocialWelfare;
            welfare.TotalWelfare += welfare.InnateWelfare + welfare.SocialWelfare;
            return welfare;
        }

        protected abstract void RefillQueue(List<int> realOpenEvents, List<int> availableUsers);


        protected void PrepareReassignment(out List<int> availableUsers, out List<int> realOpenEvents)
        {
            var phantomEvents = GetPhantomEvents();
            realOpenEvents =
                AllEvents.Where(
                    x => EventCapacity[x].Min <= Assignments[x].Count && Assignments[x].Count < EventCapacity[x].Max)
                    .ToList();
            availableUsers = GetAvailableUsers();

            foreach (var phantomEvent in phantomEvents)
            {
                if (Assignments[phantomEvent].Count > 0)
                {
                    availableUsers.AddRange(Assignments[phantomEvent]);
                    Assignments[phantomEvent].RemoveAll(x => true);
                }
            }
            availableUsers = availableUsers.Distinct().OrderBy(x => x).ToList();


            PhantomAware(availableUsers, phantomEvents);
        }

        protected List<int> GetPhantomEvents()
        {
            var phantomEvents = AllEvents.Where(x => Assignments[x].Count < EventCapacity[x].Min).ToList();
            return phantomEvents;
        }

        protected List<int> GetRealEvents()
        {
            var realEvents = AllEvents.Where(x => Assignments[x].Count >= EventCapacity[x].Min).ToList();
            return realEvents;
        }

        protected List<int> GetAvailableUsers()
        {
            var availableUsers = new List<int>();
            for (int i = 0; i < UserAssignments.Count; i++)
            {
                if (UserAssignments[i] == null)
                {
                    availableUsers.Add(i);
                }
            }

            return availableUsers;
        }

        protected abstract void PhantomAware(List<int> availableUsers, List<int> phantomEvents);

        protected List<List<int>> Swap(List<List<int>> assignments)
        {
            if (!Conf.Swap)
            {
                return assignments;
            }

            var users = new List<int>();
            for (int i = 0; i < UserAssignments.Count; i++)
            {
                var userAssignment = UserAssignments[i];
                if (userAssignment.HasValue)
                {
                    users.Add(i);
                }
            }

            var oldSocialWelfare = new Welfare();
            var newSocialWelfare = new Welfare();
            do
            {
                oldSocialWelfare = CalculateSocialWelfare(assignments);
                for (int i = 0; i < users.Count; i++)
                {
                    var user1 = users[i];

                    for (int j = i + 1; j < users.Count; j++)
                    {
                        var user2 = users[j];
                        if (user1 != user2 && UserAssignments[user1] != null && UserAssignments[user2] != null)
                        {
                            var e1 = UserAssignments[user1].Value;
                            var e2 = UserAssignments[user2].Value;
                            var oldWelfare = new Welfare {InnateWelfare = 0, SocialWelfare = 0, TotalWelfare = 0};
                            CalculateEventWelfare(assignments, e1, oldWelfare);
                            CalculateEventWelfare(assignments, e2, oldWelfare);

                            assignments[e1].Remove(user1);
                            assignments[e1].Add(user2);

                            assignments[e2].Remove(user2);
                            assignments[e2].Add(user1);
                            UserAssignments[user1] = e2;
                            UserAssignments[user2] = e1;

                            var newWelfare = new Welfare {InnateWelfare = 0, SocialWelfare = 0, TotalWelfare = 0};
                            CalculateEventWelfare(assignments, e1, newWelfare);
                            CalculateEventWelfare(assignments, e2, newWelfare);

                            if (newWelfare.TotalWelfare <= oldWelfare.TotalWelfare)
                            {
                                //undo
                                assignments[e1].Remove(user2);
                                assignments[e1].Add(user1);

                                assignments[e2].Remove(user1);
                                assignments[e2].Add(user2);

                                UserAssignments[user1] = e1;
                                UserAssignments[user2] = e2;
                            }
                        }
                    }
                }
                newSocialWelfare = CalculateSocialWelfare(assignments);

            } while (1 - oldSocialWelfare.TotalWelfare/newSocialWelfare.TotalWelfare > Conf.SwapThreshold);

            return assignments;
        }

        protected List<List<int>> ReuseDisposedPairs(List<List<int>> assignments)
        {
            if (!Conf.ReuseDisposedPairs)
            {
                return assignments;
            }

            foreach (var disposeUserEvent in DisposeUserEvents)
            {
                var @event = disposeUserEvent.Value.Event;
                var leftoutUser = disposeUserEvent.Value.User;
                var assignment = assignments[@event];
                if (UserAssignments[leftoutUser] != null || !EventIsReal(@event, assignments[@event]))
                {
                    continue;
                }
                List<UserEvent> gains = new List<UserEvent>();
                var oldWelfare = new Welfare {InnateWelfare = 0, SocialWelfare = 0, TotalWelfare = 0};
                CalculateEventWelfare(assignments, @event, oldWelfare);

                for (int i = 0; i < assignment.Count; i++)
                {
                    var user = assignment[i];
                    ReplaceUser(assignments, @event, leftoutUser, user);
                    var newWelfare = new Welfare {InnateWelfare = 0, SocialWelfare = 0, TotalWelfare = 0};
                    CalculateEventWelfare(assignments, @event, newWelfare);
                    gains.Add(new UserEvent(user, @event, newWelfare.TotalWelfare));
                    //undo
                    ReplaceUser(assignments, @event, user, leftoutUser);
                }
                var bestChoice = gains.Aggregate((current, next) => next.Utility > current.Utility ? next : current);
                if (bestChoice.Utility > oldWelfare.TotalWelfare)
                {
                    ReplaceUser(assignments, @event, leftoutUser, bestChoice.User);
                }
            }
            DisposeUserEvents.Clear();

            return Swap(assignments);
        }

        private void ReplaceUser(List<List<int>> assignments, int @event, int newUser, int oldUser)
        {
            assignments[@event].Remove(oldUser);
            assignments[@event].Add(newUser);

            UserAssignments[newUser] = @event;
            UserAssignments[oldUser] = null;
        }

        protected void KeepPhantomEvents(List<int> availableUsers, List<int> realOpenEvents,
            AlgorithmSpec.ReassignmentEnum reassignment)
        {
            _reassignmentStrategy.KeepPhantomEvents(availableUsers, realOpenEvents, reassignment,
                Conf.PreservePercentage);
        }

        protected void KeepPotentialPhantomEvents(List<int> availableUsers, List<int> realOpenEvents)
        {
            var phantomEvents =
                AllEvents.Where(x => !EventIsReal(x, Assignments[x])).Select(x => new UserEvent {Event = x, Utility = 0d}).ToList();
            foreach (var @event in phantomEvents)
            {
                @event.Utility = availableUsers.Sum(user => InAffinities[user][@event.Event]);
            }
            phantomEvents = phantomEvents.OrderByDescending(x => x.Utility).ToList();
            var numberOfAvailableUsers = availableUsers.Count;
            foreach (var phantomEvent in phantomEvents)
            {
                if (EventCapacity[phantomEvent.Event].Min <= numberOfAvailableUsers)
                {
                    realOpenEvents.Add(phantomEvent.Event);
                    numberOfAvailableUsers -= EventCapacity[phantomEvent.Event].Min;
                    if (numberOfAvailableUsers == 0)
                    {
                        break;
                    }
                }
            }
        }

        protected void SetInputFile(FileInfo file)
        {
            if (file != null)
            {
                Conf.InputFilePath = file.FullName;
            }
        }

        public string GetInputFile()
        {
            return Conf.InputFilePath;
        }

        public FeedTypeEnum GetFeedType()
        {
            return Conf.FeedType;
        }

        public Welfare CalculateSocialWelfare(List<List<int>> assignments)
        {
            var welfare = new Welfare
            {
                TotalWelfare = 0d,
                InnateWelfare = 0d,
                SocialWelfare = 0d
            };

            for (int @event = 0; @event < assignments.Count; @event++)
            {
                CalculateEventWelfare(assignments, @event, welfare);
            }

            return welfare;
        }

        protected void CalculateEventWelfare(List<List<int>> assignments, int @event, Welfare welfare , bool checkEventReality = true)
        {
            if (checkEventReality && !EventIsReal(@event, assignments[@event]))
            {
                return;
            }

            var w = CalculateEventWelfare(assignments, @event);
            welfare.InnateWelfare += w.InnateWelfare;
            welfare.SocialWelfare += w.SocialWelfare;
            welfare.TotalWelfare += w.InnateWelfare + w.SocialWelfare;
        }

        protected Welfare CalculateEventWelfare(List<List<int>> assignments, int @event)
        {
            var result = new Welfare
            {
                InnateWelfare = 0d,
                SocialWelfare = 0d,
                TotalWelfare = 0d
            };

            var assignment = assignments[@event];

            foreach (var user1 in assignment)
            {
                result.InnateWelfare += InAffinities[user1][@event];
                foreach (var user2 in assignment)
                {
                    if (user1 != user2)
                    {
                        result.SocialWelfare += SocAffinities[user1, user2];
                    }
                }
            }
            result.InnateWelfare = (1 - Conf.Alpha)*result.InnateWelfare;
            result.SocialWelfare = Conf.Alpha*result.SocialWelfare;
            result.TotalWelfare += result.InnateWelfare + result.SocialWelfare;
            return result;
        }

        public Welfare CalculateSocialWelfare(List<List<int>> assignments, int user, bool onlyRealEvents = true)
        {
            var welfare = new Welfare
            {
                TotalWelfare = 0d,
                InnateWelfare = 0d,
                SocialWelfare = 0d
            };

            var userAssignments = assignments.Where(x => x.Contains(user)).ToList();
            if (userAssignments.Count > 1)
            {
                throw new Exception("User assigned to more than one event");
            }

            if (userAssignments.Count == 0)
            {
                return welfare;
            }
            var assignment = userAssignments.First();
            var @event = assignments.IndexOf(assignment);
            if (onlyRealEvents && !EventIsReal(@event, assignments[@event]))
            {
                return welfare;
            }

            double s1 = InAffinities[user][@event];
            double s2 = 0;

            foreach (var user2 in assignment)
            {
                if (user != user2)
                {
                    s2 += SocAffinities[user, user2];
                }
            }
            s1 = (1 - Conf.Alpha)*s1;
            s2 = Conf.Alpha*s2;
            welfare.InnateWelfare += s1;
            welfare.SocialWelfare += s2;
            welfare.TotalWelfare += s1 + s2;

            return welfare;
        }

        public Dictionary<int, double> CalcRegRatios(List<int> allUsers)
        {
            var ratios = new Dictionary<int, double>();
            foreach (var user in allUsers)
            {
                var ratio = CalculateRegRatio(user);
                ratios.Add(user, ratio);
            }
            return ratios;
        }

        public double CalculateRegRatio(int user)
        {
            if (!UserAssignments[user].HasValue || !EventIsReal(UserAssignments[user].Value, Assignments[UserAssignments[user].Value]))
            {
                return 1;
            }

            var finalDenom = double.MinValue;
            foreach (var @event in AllEvents)
            {
                var friendAffinities = new List<double>();
                for (int i = 0; i < Conf.NumberOfUsers; i++)
                {
                    if (SocAffinities[user, i] > 0)
                    {
                        friendAffinities.Add(SocAffinities[user, i]);
                    }

                    if (Conf.Asymmetric && SocAffinities[i, user] > 0)
                    {
                        friendAffinities.Add(SocAffinities[i, user]);
                    }
                }
                friendAffinities = friendAffinities.OrderByDescending(x => x).ToList();
                var k = Math.Min(EventCapacity[@event].Max - 1, friendAffinities.Count);
                var localSocialAffinity = friendAffinities.Take(k).Sum(x => x);
                var denom = (1 - Conf.Alpha)*InAffinities[user][@event] + Conf.Alpha*localSocialAffinity;
                finalDenom = Math.Max(finalDenom, denom);
            }

            var assignedEvent = UserAssignments[user].Value;
            var users = Assignments[assignedEvent];
            var socialAffinity = users.Sum(x => SocAffinities[user, x] + (Conf.Asymmetric ? SocAffinities[x, user] : 0d));
            var numerator = (1 - Conf.Alpha)*InAffinities[user][assignedEvent] + Conf.Alpha*socialAffinity;

            if (finalDenom == 0)
            {
                return 1;
            }
            var phi = 1 - (numerator/finalDenom);
            return phi;
        }

        public bool EventIsReal(int @event, List<int> assignment)
        {
            var usersCount = Assignments[@event].Count;
            var min = EventCapacity[@event].Min;
            var max = EventCapacity[@event].Max;
            return usersCount >= min && usersCount <= max;
        }

        protected Dictionary<int, List<int>> DetectCommunities()
        {
            var graph = new Graph();
            int edgecounter = 0;
            for (int i = 0; i < SocAffinities.GetLength(0); i++)
            {
                for (int j = 0; j < SocAffinities.GetLength(1); j++)
                {
                    if (SocAffinities[i, j] != 0)
                    {
                        graph.AddEdge(i, j, SocAffinities[i, j]);
                        edgecounter++;
                    }
                }
            }
            Console.WriteLine("{0} edges added", edgecounter);

            Dictionary<int, int> partition = Community.BestPartition(graph);
            var communities = new Dictionary<int, List<int>>();
            foreach (var kvp in partition)
            {
                List<int> nodeset;
                if (!communities.TryGetValue(kvp.Value, out nodeset))
                {
                    nodeset = communities[kvp.Value] = new List<int>();
                }
                nodeset.Add(kvp.Key);
            }
            Console.WriteLine("{0} communities found", communities.Count);
            return communities;
        }

        protected UserEvent Util(int @event, int user, bool communityAware, CommunityFixEnum communityFix,
            List<int> users)
        {
            var userevent = new UserEvent
            {
                Event = @event,
                User = user
            };
            var g = (1 - Conf.Alpha)*InAffinities[user][@event];

            var s = Conf.Alpha*
                    Assignments[@event].Sum(
                        u => SocAffinities[user, u] + (Conf.Asymmetric ? SocAffinities[u, user] : 0d));

            g = g + s;

            if (communityAware)
            {
                //var assignedUsers = Assignments.SelectMany(x => x).ToList();
                //var users = AllUsers.Where(x => !UserAssignments[x].HasValue && !assignedUsers.Contains(x)).ToList();

                var denumDeduction = 1;
                if (communityFix.HasFlag(CommunityFixEnum.DenomFix))
                {
                    denumDeduction = 0;
                }

                if (communityFix.HasFlag(CommunityFixEnum.None))
                {
                    s = Conf.Alpha*(EventCapacity[@event].Max - Assignments[@event].Count)*
                        users.Sum(u => SocAffinities[user, u] + (Conf.Asymmetric ? SocAffinities[u, user] : 0d))/
                        (double) Math.Max(users.Count - denumDeduction, 1);
                }
                else if (communityFix.HasFlag(CommunityFixEnum.Version1))
                {
                    var lowInterestedUsers =
                        users.OrderBy(x => SocAffinities[user, x]).Take(EventCapacity[@event].Max).ToList();
                    s = Conf.Alpha*(EventCapacity[@event].Max - Assignments[@event].Count)*
                        (lowInterestedUsers.Sum(
                            u => SocAffinities[user, u] + (Conf.Asymmetric ? SocAffinities[u, user] : 0d))/
                         (double) Math.Max(lowInterestedUsers.Count - denumDeduction, 1));

                    //s += Conf.Alpha * (EventCapacity[@event].Max - Assignments[@event].Count) *
                    //(users.Sum(u => InAffinities[u][@event]) / (double)Math.Max(users.Count - 1, 1));
                }
                else if (communityFix.HasFlag(CommunityFixEnum.Version2))
                {
                    var lowInterestedUsers =
                        users.OrderBy(x => SocAffinities[user, x])
                            .Take(EventCapacity[@event].Max - Assignments[@event].Count)
                            .ToList();
                    s = Conf.Alpha*(EventCapacity[@event].Max - Assignments[@event].Count)*
                        (lowInterestedUsers.Sum(
                            u => SocAffinities[user, u] + (Conf.Asymmetric ? SocAffinities[u, user] : 0d))/
                         (double) Math.Max(lowInterestedUsers.Count - denumDeduction, 1));
                }
                else if (communityFix.HasFlag(CommunityFixEnum.Version3))
                {
                    var lowInterestedUsers =
                        users.OrderBy(x => SocAffinities[user, x] + InAffinities[x][@event])
                            .Take(EventCapacity[@event].Max - Assignments[@event].Count)
                            .ToList();
                    s = Conf.Alpha*(EventCapacity[@event].Max - Assignments[@event].Count)*
                        (lowInterestedUsers.Sum(
                            u => SocAffinities[user, u] + (Conf.Asymmetric ? SocAffinities[u, user] : 0d))/
                         (double) Math.Max(lowInterestedUsers.Count - denumDeduction, 1));
                }
                else if (communityFix.HasFlag(CommunityFixEnum.Version4))
                {
                    var lowInterestedUsers = users.Take(EventCapacity[@event].Max - Assignments[@event].Count).ToList();
                    s = Conf.Alpha*(EventCapacity[@event].Max - Assignments[@event].Count)*
                        (lowInterestedUsers.Sum(
                            u => SocAffinities[user, u] + (Conf.Asymmetric ? SocAffinities[u, user] : 0d))/
                         (double) Math.Max(lowInterestedUsers.Count - denumDeduction, 1));
                }

                g = s + g;
                /*var firstNotSecond = usersints.Except(users).ToList();
                var secondNotFirst = users.Except(usersints).ToList();
                if (firstNotSecond.Count != 0 || secondNotFirst.Count != 0)
                {
                    Console.WriteLine("|_user| bigger than |users| is {0}.", firstNotSecond.Count > secondNotFirst.Count);
                }*/
            }
            userevent.Utility = g;

            return userevent; //Math.Round(g, Conf.Percision);
        }

        protected double Util(int @event, int mainUser)
        {
            return (1 - Conf.Alpha) * InAffinities[mainUser][@event];
        }

        protected double Util(int @event, int mainUser, int friendUser, UserFriends friends)
        {
            var g = (1 - Conf.Alpha) * InAffinities[friendUser][@event];

            var s = Conf.Alpha * friends.Sum(u => SocAffinities[friendUser, u.User] + (Conf.Asymmetric ? SocAffinities[u.User, friendUser] : 0d));

            return g + s;
        }
    }
}

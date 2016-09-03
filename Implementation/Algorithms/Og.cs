﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Implementation.Dataset_Reader;
using Implementation.Data_Structures;

namespace Implementation.Algorithms
{
    public class Og : Algorithm<List<UserEvent>>
    {
        private List<int> _events;
        private List<int> _users;
        private List<int> _numberOfUserAssignments;
        private List<int> _eventDeficitContribution;
        private int _deficit = 0;
        private Queue<UserEvents> _queue;
        private bool _init;
        private readonly IDataFeed _dataFeeder;
        private OgConf _conf => (OgConf)Conf;

        public Og(SgConf conf, IDataFeed dataFeed)
        {
            _dataFeeder = dataFeed;
            Conf = conf;
        }

        public override void Run()
        {
            if (!_init)
                throw new Exception("Not Initialized");
            int hitcount = 0;

            while (_queue.Count > 0)
            {
                hitcount++;
                PrintQueue();
                var userEvents = _queue.Dequeue();
                var user = userEvents.User;
                var eventInterest = userEvents.GetBestEvent();
                var @event = eventInterest.Event;
                var minCapacity = EventCapacity[@event].Min;
                var maxCapacity = EventCapacity[@event].Max;
                bool assignmentMade = false;

                if (UserAssignments[user] == null && Assignments[@event].Count < maxCapacity)
                {
                    Assignments[@event].Add(user);
                    _numberOfUserAssignments[user]++;
                    assignmentMade = true;
                    UserAssignments[user] = @event;
                }

                AdjustList(user, @event, assignmentMade);

                //if (_queue.Count == 0)
                //{
                //    DynamicReassign();
                //}
            }

            //GreedyAssign();
        }

        private double Util(int @event, int user)
        {
            var g = (1 - _conf.Alpha) * InAffinities[user][@event];

            var s = _conf.Alpha * Assignments[@event].Sum(u => SocAffinities[user, u]);

            g = g + s;

            if (_conf.CommunityAware)
            {
                var assignedUsers = Assignments.SelectMany(x => x).ToList();
                var users = AllUsers.Where(x => !UserAssignments[x].HasValue && !assignedUsers.Contains(x));
                s = _conf.Alpha * (EventCapacity[@event].Max - Assignments[@event].Count) * users.Sum(u => SocAffinities[user, u] + SocAffinities[u, user]) / (double)Math.Max(_users.Count - 1, 1);

                g = s + g;
            }

            return Math.Round(g, _conf.Percision);
        }

        private void AdjustList(int user, int @event, bool assignmentMade)
        {
            foreach (var u in AllUsers)
            {
                Update(user, u, @event);
            }

            PrintAssignments(assignmentMade);
            CheckValidity();
        }

        private void CheckValidity()
        {
            foreach (var assignment in Assignments)
            {
                if (assignment.Count != assignment.Distinct().Count())
                {
                    Console.WriteLine("Elements are not unique !");
                    break;
                }
            }
        }

        private void Update(int user1, int user2, int @event)
        {
            if (SocAffinities[user2, user1] > 0 && UserAssignments[user2] == null) /* or a in affected_evts)*/
            {
                //What if this friend is already in that event, should it be aware that his friend is now assigned to this event?
                var newPriority = Util(@event, user2);
                if (!Assignments[@event].Contains(user2) && Assignments[@event].Count < EventCapacity[@event].Max)
                {
                    foreach (var userInterest in _queue)
                    {
                        if (userInterest.User == user1)
                        {
                            userInterest.UpdateUserInterest(@event, newPriority);
                        }
                    }
                }
            }
        }

        public override void Initialize()
        {
            SetNullMembers();

            AllUsers = new List<int>();
            AllEvents = new List<int>();
            _init = false;

            if (_conf.FeedType == FeedTypeEnum.Example1 || _conf.FeedType == FeedTypeEnum.XlsxFile)
            {
                int numberOfUsers;
                int numberOfEvents;
                _dataFeeder.GetNumberOfUsersAndEvents(out numberOfUsers, out numberOfEvents);
                _conf.NumberOfUsers = numberOfUsers;
                _conf.NumberOfEvents = numberOfEvents;
            }

            for (var i = 0; i < _conf.NumberOfUsers; i++)
            {
                AllUsers.Add(i);
            }

            for (var i = 0; i < _conf.NumberOfEvents; i++)
            {
                AllEvents.Add(i);
            }

            _users = new List<int>();
            _events = new List<int>();
            Assignments = new List<List<int>>();
            UserAssignments = new List<int?>();
            _numberOfUserAssignments = new List<int>();
            _eventDeficitContribution = new List<int>();
            SocialWelfare = 0;
            _queue = new Queue<UserEvents>();
            //_deficit = 0;
            _init = true;
            //_affectedUserEvents = new List<UserEvent>();

            for (var i = 0; i < _conf.NumberOfUsers; i++)
            {
                _users.Add(i);
                UserAssignments.Add(null);
                _numberOfUserAssignments.Add(0);
            }

            for (var i = 0; i < _conf.NumberOfEvents; i++)
            {
                _events.Add(i);
                _eventDeficitContribution.Add(0);
                Assignments.Add(new List<int>());
            }

            EventCapacity = _dataFeeder.GenerateCapacity(_users, _events);
            InAffinities = _dataFeeder.GenerateInnateAffinities(_users, _events);
            SocAffinities = _dataFeeder.GenerateSocialAffinities(_users);

            var rnd = new System.Random();
            _users = _users.OrderBy(item => rnd.Next()).ToList();

            foreach (var u in _users)
            {
                var ue = new UserEvents(u, _conf.NumberOfEvents);
                foreach (var e in _events)
                {
                    var gain = 0d;
                    if (InAffinities[u][e] != 0)
                    {
                        gain = (1 - _conf.Alpha) * InAffinities[u][e];
                        gain = Math.Round(gain, _conf.Percision);
                    }
                    ue.AddEvent(e, gain);
                }
                _queue.Enqueue(ue);
            }
        }

        protected override void PrintQueue()
        {
            if (!_conf.PrintOutEachStep)
            {
                return;
            }

            var userEvents = _queue.Peek();
            var bestEvent = userEvents.GetBestEvent();
            Console.WriteLine("User {0}, Event {1}, Value {2}", (char)(userEvents.User + 97),
                (char)(bestEvent.Event + 88), bestEvent.Utility);
        }

        private void SetNullMembers()
        {
            InAffinities = null;
            SocAffinities = null;
            _events = null;
            _users = null;
            AllEvents = null;
            AllUsers = null;
            _numberOfUserAssignments = null;
            _eventDeficitContribution = null;
            Assignments = null;
            UserAssignments = null;
            _deficit = 0;
            EventCapacity = null;
            _queue = null;
            _init = false;
        }
    }
}
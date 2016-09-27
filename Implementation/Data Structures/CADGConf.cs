﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Implementation.Experiment;
using OfficeOpenXml;

namespace Implementation.Data_Structures
{
    public class CadgConf : SgConf
    {
        public bool ImmediateReaction { get; set; }
        public AlgorithmSpec.ReassignmentEnum Reassignment { get; set; }
        public bool PhantomAware { get; set; }
        public bool DeficitFix { get; set; }
        public bool PostInitializationInsert { get; set; }
        public bool LazyAdjustment { get; set; }
        public int NumberOfPhantomEvents { get; set; }
        public bool CommunityAware { get; set; }
        public bool CommunityFix { get; set; }
        public bool DoublePriority { get; set; }

        public CadgConf()
        {
            NumberOfUsers = 10;
            NumberOfEvents = 4;
            ImmediateReaction = false;
            Reassignment = AlgorithmSpec.ReassignmentEnum.None;
            PrintOutEachStep = false;
            InputFilePath = null;
            PhantomAware = false;
            DeficitFix = false;
            PostInitializationInsert = false;
            Alpha = 0.5;
            Percision = 7;
            NumberOfPhantomEvents = 0;
            LazyAdjustment = false;
            CommunityAware = false;
            AlgorithmName = null;
            CommunityFix = false;
            DoublePriority = false;
        }

        protected override void PrintConfigs(ExcelPackage excel, Stopwatch stopwatch)
        {
            var ws = excel.Workbook.Worksheets.Add("Configs");
            int i = 1;
            ws.Cells[i, 1].Value = "Feed Type";
            ws.Cells[i, 2].Value = FeedType;
            i++;
            ws.Cells[i, 1].Value = "Number Of Users";
            ws.Cells[i, 2].Value = NumberOfUsers;
            i++;

            ws.Cells[i, 1].Value = "Number Of Events";
            ws.Cells[i, 2].Value = NumberOfEvents;
            i++;

            ws.Cells[i, 1].Value = "Immediate Reaction";
            ws.Cells[i, 2].Value = ImmediateReaction;
            i++;

            ws.Cells[i, 1].Value = "Reassignment Type";
            ws.Cells[i, 2].Value = Reassignment;
            i++;

            ws.Cells[i, 1].Value = "Print Each Step";
            ws.Cells[i, 2].Value = PrintOutEachStep;
            i++;

            ws.Cells[i, 1].Value = "Input File Path";
            ws.Cells[i, 2].Value = InputFilePath;
            i++;

            ws.Cells[i, 1].Value = "Phantom Aware";
            ws.Cells[i, 2].Value = PhantomAware;
            i++;

            ws.Cells[i, 1].Value = "Deficit Fix";
            ws.Cells[i, 2].Value = DeficitFix;
            i++;

            ws.Cells[i, 1].Value = "Post Initialization Insert";
            ws.Cells[i, 2].Value = PostInitializationInsert;
            i++;


            ws.Cells[i, 1].Value = "Alpha";
            ws.Cells[i, 2].Value = Alpha;
            i++;

            ws.Cells[i, 1].Value = "Percision";
            ws.Cells[i, 2].Value = Percision;
            i++;

            ws.Cells[i, 1].Value = "Lazy Adjustment";
            ws.Cells[i, 2].Value = LazyAdjustment;
            i++;

            ws.Cells[i, 1].Value = "Community Aware";
            ws.Cells[i, 2].Value = CommunityAware;
            i++;

            ws.Cells[i, 1].Value = "Number Of Phantom Events";
            ws.Cells[i, 2].Value = NumberOfPhantomEvents;
            i++;

            ws.Cells[i, 1].Value = "Algorithm Name";
            ws.Cells[i, 2].Value = AlgorithmName;
            i++;

            ws.Cells[i, 1].Value = "Execution Time";
            ws.Cells[i, 2].Value = stopwatch.ElapsedMilliseconds;
            i++;

            ws.Cells[i, 1].Value = "Community Fix";
            ws.Cells[i, 2].Value = CommunityFix;
            i++;

            ws.Cells[i, 1].Value = "Double Priority";
            ws.Cells[i, 2].Value = DoublePriority;

            ws.Cells[ws.Dimension.Address].AutoFitColumns();

        }
    }
}

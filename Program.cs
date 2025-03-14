﻿using RobotOM;
using Newtonsoft.Json;


namespace RobotImportTimberModel
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                throw new Exception("Invalid number of arguments passed.");
            }

            var userUlsCases = args[0];
            var userSlsCases = args[1];

            RobotApplication robotApp = new();
            if (robotApp.Project.FileName == null)
            {
                throw new Exception("Robot model not open.");
            }

            IRobotCollection bars = robotApp.Project.Structure.Bars.GetAll();
            IRobotBarForceServer forceServer = robotApp.Project.Structure.Results.Bars.Forces;
            RobotBarDeflectionServer deflectionServer = robotApp.Project.Structure.Results.Bars.Deflections;
            IRobotNodeServer nodeServer = robotApp.Project.Structure.Nodes;

            RobotSelection ulsCaseSelection = robotApp.Project.Structure.Selections.Create(IRobotObjectType.I_OT_CASE);
            ulsCaseSelection.FromText(userUlsCases);
            IRobotCollection ulsCases = robotApp.Project.Structure.Cases.GetMany(ulsCaseSelection);

            RobotSelection slsCaseSelection = robotApp.Project.Structure.Selections.Create(IRobotObjectType.I_OT_CASE);
            slsCaseSelection.FromText(userSlsCases);
            IRobotCollection slsCases = robotApp.Project.Structure.Cases.GetMany(slsCaseSelection);

            var robotData = new List<BarData>();

            for (int i = 1; i <= bars.Count; i++)
            {
                IRobotBar bar = (IRobotBar)bars.Get(i);
                int barId = bar.Number;
                double barLength = bar.Length;

                IRobotMaterialData barMaterialData = (IRobotMaterialData)bar.GetLabel(IRobotLabelType.I_LT_MATERIAL).Data;
                if (barMaterialData.Type != IRobotMaterialType.I_MT_TIMBER)
                {
                    continue;
                }

                IRobotBarSectionData sectionData = (IRobotBarSectionData)bar.GetLabel(IRobotLabelType.I_LT_BAR_SECTION).Data;
                double sectionArea = sectionData.GetValue(IRobotBarSectionDataValue.I_BSDV_AX);
                double sectionI = sectionData.GetValue(IRobotBarSectionDataValue.I_BSDV_IY);

                double momentMajor, momentMinor, shearMajor, shearMinor, axial;
                momentMajor = momentMinor = shearMajor = shearMinor = axial = 0;

                var pointsAlong = new List<double>() { 0, 0.25, 0.5, 0.75, 1 };
                foreach (var point in pointsAlong)
                {
                    for (int j = 1; j <= ulsCases.Count; j++)
                    {
                        int caseNum = ((IRobotCase)ulsCases.Get(j)).Number;
                        RobotBarForceData currentForces = forceServer.Value(barId, caseNum, point);

                        double currentMomentMajor = Math.Round(currentForces.MY / 1000, 2);
                        double currentMomentMinor = Math.Round(currentForces.MZ / 1000, 2);
                        double currentShearMajor = Math.Round(currentForces.FZ / 1000, 2);
                        double currentShearMinor = Math.Round(currentForces.FY / 1000, 2);
                        double currentAxial = Math.Round(currentForces.FX / 1000, 2);

                        momentMajor = Math.Abs(currentMomentMajor) > Math.Abs(momentMajor) ? currentMomentMajor : momentMajor;
                        momentMinor = Math.Abs(currentMomentMinor) > Math.Abs(momentMinor) ? currentMomentMinor : momentMinor;
                        shearMajor = Math.Abs(currentShearMajor) > Math.Abs(shearMajor) ? Math.Abs(currentShearMajor) : shearMajor;
                        shearMinor = Math.Abs(currentShearMinor) > Math.Abs(shearMinor) ? Math.Abs(currentShearMinor) : shearMinor;
                        axial = Math.Abs(currentAxial) > Math.Abs(axial) ? currentAxial : axial;
                    }
                }

                double barDeflection = double.NegativeInfinity;
                for (int j = 1; j <= slsCases.Count; j++)
                {
                    int caseNum = ((IRobotCase)slsCases.Get(j)).Number;
                    double currentBarDeflection = Math.Abs(deflectionServer.MaxValue(barId, caseNum).UZ);

                    if (currentBarDeflection > barDeflection) { barDeflection = currentBarDeflection; }
                }

                var barStartNodeId = bar.StartNode;
                var barEndNodeId = bar.EndNode;

                var barStartNode = (IRobotNode)nodeServer.Get(barStartNodeId);
                var barEndNode = (IRobotNode)nodeServer.Get(barEndNodeId);

                // Assumes all columns are vertical
                bool isAxialMember = false;
                if (Math.Abs(barStartNode.X - barEndNode.X) < 0.1 && Math.Abs(barStartNode.Y - barEndNode.Y) < 0.1) { isAxialMember = true; }

                BarData barData = new() { Id = barId, MomentMajor = momentMajor, MomentMinor = momentMinor, ShearMajor = shearMajor, ShearMinor = shearMinor, Axial = axial, IsAxialMember = isAxialMember, Deflection = barDeflection, Area = sectionArea, SecondMomentOfArea = sectionI, Length = barLength, MaterialE=barMaterialData.E, MaterialG=barMaterialData.GMean };
                robotData.Add(barData);
            }

            var jsonResults = JsonConvert.SerializeObject(robotData);
            Console.WriteLine(jsonResults);
        }
    }

    public class BarData
    {
        public int Id { get; set; }
        public double MomentMajor { get; set; }
        public double MomentMinor { get; set; }
        public double ShearMajor { get; set; }
        public double ShearMinor { get; set; }
        public double Axial { get; set; }
        public bool IsAxialMember { get; set; }
        public double Deflection { get; set; }
        public double Area { get; set; }
        public double SecondMomentOfArea { get; set; }
        public double Length { get; set; }
        public double MaterialE { get; set; }
        public double MaterialG { get; set; }

    }
}
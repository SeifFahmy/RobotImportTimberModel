using RobotOM;
using Newtonsoft.Json;


namespace RobotImportTimberModel
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                throw new Exception("Invalid number of arguments passed.");
            }

            var userCases = args[0];

            RobotApplication robotApp = new();

            IRobotCollection bars = robotApp.Project.Structure.Bars.GetAll();
            IRobotBarForceServer forceServer = robotApp.Project.Structure.Results.Bars.Forces;

            RobotSelection caseSelection = robotApp.Project.Structure.Selections.Create(IRobotObjectType.I_OT_CASE);
            caseSelection.FromText(userCases);
            IRobotCollection cases = robotApp.Project.Structure.Cases.GetMany(caseSelection);

            var robotData = new List<BarData>();

            for (int i = 1; i <= bars.Count; i++)
            {
                IRobotBar bar = (IRobotBar)bars.Get(i);
                int barId = bar.Number;

                IRobotMaterialData barMaterialData = (IRobotMaterialData)bar.GetLabel(IRobotLabelType.I_LT_MATERIAL).Data;
                if (barMaterialData.Type != IRobotMaterialType.I_MT_TIMBER)
                {
                    continue;
                }

                double momentMajor, momentMinor, shearMajor, shearMinor, axial;
                momentMajor = momentMinor = shearMajor = shearMinor = axial = 0;
                bool isAxialMember = false;

                var pointsAlong = new List<double>() { 0, 0.25, 0.5, 0.75, 1 };
                foreach (var point in pointsAlong)
                {
                    for (int j = 1; j <= cases.Count; j++)
                    {
                        int caseNum = ((IRobotCase)cases.Get(j)).Number;
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

                        // TODO: track whether a member is a bending or axial member to determine which section sizes to assign them when designing (i.e. square or rectangular)
                    }
                }

                BarData barData = new() { Id = barId, MomentMajor = momentMajor, MomentMinor = momentMinor, ShearMajor = shearMajor, ShearMinor = shearMinor, Axial = axial, IsAxialMember = isAxialMember };
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
    }
}
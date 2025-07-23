const string LCD_NAME = "DeltaV LCD";
const double HYDROGEN_DENSITY_KG_PER_L = 0.01;
const string HUDLCD_FORMAT = "hudlcd:0.65:0.95:0.9.2:255,255,255:";

bool includeRCS = true;
bool displayBurnTimes = true;
bool showAllDirections = true;

readonly HashSet<string> rcsSubtypes = new HashSet<string>(new[] {
    "LynxRcsThruster1", "AryxRCSRamp", "AryxRCSHalfRamp", "AryxRCSSlant", "AryxRCS",
    "RCS2Bare", "RCS2Cube", "RCS2Half", "RCS2Slope", "RCS2SlopeTip1", "RCS2SlopeTip2",
    "RCS21x2Slope1", "RCS21x2Slope2"
});

// dictionary for fuel usage per thruster
readonly Dictionary<string, double> fuelUsageLpsBySubtypeId = new Dictionary<string, double> {
    { "ARYLYNX_SILVERSMITH_DRIVE", 1560 },
    { "ARYLNX_QUADRA_Epstein_Drive", 521.74 },
    { "LargeBlockLargeHydrogenThrust", 2000 },
    { "LargeBlockSmallHydrogenThrust", 388.89 },
    { "ARYLNX_RAIDER_Epstein_Drive", 1120 },
    { "ARYLNX_DRUMMER_Epstein_Drive", 1750 },
    { "ARYLNX_PNDR_Epstein_Drive", 645.16 },
    { "ARYLNX_Mega_Epstein_Drive", 9090 },
    { "ARYLNX_Epstein_Drive", 1030 },
    { "ARYLNX_ROCI_Epstein_Drive", 2110 },
    { "ARYLNX_SCIRCOCCO_Epstein_Drive", 3110 },
    { "ARYLNX_MUNR_Epstein_Drive", 833.33 },
    { "2x2ThrusterZAC", 933.33 },
    { "Large5x5RocketThruster", 3000 },
    { "LargeTripleThrusterHeat", 1500 },
    { "LargeTripleThrusterHeatNoShroud", 1500 },
    { "LynxRcsThruster1", 272.73 },
    { "AryxRCSRamp", 272.73 },
    { "AryxRCSHalfRamp", 272.73 },
    { "AryxRCSSlant", 272.73 },
    { "AryxRCS", 272.73 },
    { "RCS2Bare", 227.27 },
    { "RCS2Cube", 227.27 },
    { "RCS2Half", 227.27 },
    { "RCS2Slope", 227.27 },
    { "RCS2SlopeTip1", 227.27 },
    { "RCS2SlopeTip2", 227.27 },
    { "RCS21x2Slope1", 227.27 },
    { "RCS21x2Slope2", 227.27 }
};

IMyTextPanel lcd;
IMyShipController controller;
List<IMyThrust> allThrusters = new List<IMyThrust>();
List<IMyGasTank> tanks = new List<IMyGasTank>();
List<IMyBlockGroup> allGroups = new List<IMyBlockGroup>();

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;

    lcd = GridTerminalSystem.GetBlockWithName(LCD_NAME) as IMyTextPanel;
    if (lcd == null) throw new Exception($"LCD '{LCD_NAME}' not found.");

    lcd.ContentType = ContentType.TEXT_AND_IMAGE;
    lcd.Font = "Monospace";
    lcd.FontSize = 1.2f;
    if (!lcd.CustomData.Trim().ToLower().StartsWith("hudlcd:"))
        lcd.CustomData = HUDLCD_FORMAT;

    var cockpits = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType(cockpits, c => c.CubeGrid == Me.CubeGrid);

    controller =
        cockpits.FirstOrDefault(c => c.CustomName.ToLower().Contains("deltavcockpit")) ??
        cockpits.FirstOrDefault(c => c.IsUnderControl) ??
        cockpits.FirstOrDefault(c => c.IsMainCockpit || c is IMyCockpit);

    if (controller == null) throw new Exception("No valid ship controller found.");

    Echo("Using cockpit: \"" + controller.CustomName + "\"");

    GridTerminalSystem.GetBlocksOfType(allThrusters, t => t.IsFunctional && t.CubeGrid == Me.CubeGrid);
    GridTerminalSystem.GetBlocksOfType(tanks, t => t.IsFunctional && t.CubeGrid == Me.CubeGrid &&
        t.DefinitionDisplayNameText.ToLower().Contains("hydrogen"));

    var tempGroups = new List<IMyBlockGroup>();
    GridTerminalSystem.GetBlockGroups(tempGroups);
    allGroups.Clear();
    allGroups.AddRange(tempGroups);
}

public void Main(string arg, UpdateType updateSource)
{
    string normalizedArg = (arg ?? "").Trim().ToLower();

    if (normalizedArg == "rcstoggle")
        includeRCS = !includeRCS;
    else if (normalizedArg == "burntimetoggle")
        displayBurnTimes = !displayBurnTimes;
    else if (normalizedArg == "alldirectionstoggle")
        showAllDirections = !showAllDirections;
    else if (normalizedArg == "reset")
    {
        lcd.CustomData = HUDLCD_FORMAT +
            "\ngroups=All Drives,Boost,Efficient,Braking\n" +
            "# Define terminal groups matching these names to calculate delta-V\n" +
            "# Add more names, comma separated\n" +
            "# Recompile script after changing groups in terminal";
        Echo("LCD CustomData reset.");
        return;
    }

    var massInfo = controller.CalculateShipMass();
    double m0 = massInfo.TotalMass;
    double fuelMass = tanks.Sum(t => t.Capacity * t.FilledRatio * HYDROGEN_DENSITY_KG_PER_L);
    double mf = m0 - fuelMass;

    if (mf <= 0 || m0 <= 0 || mf >= m0)
    {
        lcd.WriteText("Δv unavailable\nMass/fuel invalid.");
        return;
    }

    var sb = new StringBuilder();
    sb.AppendLine("Delta-V by Group:");

    var customLines = lcd.CustomData.Split('\n');
    string groupLine = customLines.FirstOrDefault(l => l.Trim().ToLower().StartsWith("groups="));
    var groupNames = new List<string>();

    if (groupLine != null)
    {
        var raw = groupLine.Substring(7).Split(',');
        foreach (var g in raw) groupNames.Add(g.Trim());
    }

    var thrustByDir = new Dictionary<Base6Directions.Direction, double>();
    var fuelByDir = new Dictionary<Base6Directions.Direction, double>();
    foreach (Base6Directions.Direction dir in Enum.GetValues(typeof(Base6Directions.Direction)))
    {
        thrustByDir[dir] = 0;
        fuelByDir[dir] = 0;
    }

    if (groupNames.Count > 0)
    {
        foreach (var name in groupNames)
        {
            var group = allGroups.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (group == null) continue;

            var blocks = new List<IMyTerminalBlock>();
            group.GetBlocks(blocks);

            double thrust = 0, fuelLps = 0;

            foreach (var block in blocks)
            {
                var thruster = block as IMyThrust;
                if (thruster == null) continue;

                string subtype = thruster.BlockDefinition.SubtypeName;
                if (!fuelUsageLpsBySubtypeId.TryGetValue(subtype, out var lps)) continue;
                if (rcsSubtypes.Contains(subtype) && !includeRCS) continue;

                thrust += thruster.MaxEffectiveThrust;
                fuelLps += lps;
            }

            if (thrust <= 0 || fuelLps <= 0) continue;

            double ve = thrust / (fuelLps * HYDROGEN_DENSITY_KG_PER_L);
            double dv = ve * Math.Log(m0 / mf);
            sb.AppendFormat("{0,-12}: {1,6:N0} m/s\n", name, dv);
        }
    }
    else
    {
        double thrust = 0, fuelLps = 0;

        foreach (var thruster in allThrusters)
        {
            string subtype = thruster.BlockDefinition.SubtypeName;
            if (!fuelUsageLpsBySubtypeId.TryGetValue(subtype, out var lps)) continue;
            if (rcsSubtypes.Contains(subtype)) continue;

            var thrustDir = Base6Directions.GetFlippedDirection(thruster.Orientation.Forward);
            if (thrustDir != controller.Orientation.Forward) continue;

            thrust += thruster.MaxEffectiveThrust;
            fuelLps += lps;
        }

        if (thrust > 0 && fuelLps > 0)
        {
            double ve = thrust / (fuelLps * HYDROGEN_DENSITY_KG_PER_L);
            double dv = ve * Math.Log(m0 / mf);
            sb.AppendFormat("{0,-12}: {1,6:N0} m/s\n", "Default", dv);
        }
        else
        {
            sb.AppendLine("Default     : Δv unavailable");
        }
    }

    if (displayBurnTimes)
    {
        sb.AppendLine();

        var displayOrder = new Base6Directions.Direction[] {
            Base6Directions.Direction.Forward, Base6Directions.Direction.Backward,
            Base6Directions.Direction.Left, Base6Directions.Direction.Right,
            Base6Directions.Direction.Up, Base6Directions.Direction.Down
        };

        foreach (var thruster in allThrusters)
        {
            string subtype = thruster.BlockDefinition.SubtypeName;
            if (!fuelUsageLpsBySubtypeId.TryGetValue(subtype, out var lps)) continue;
            if (rcsSubtypes.Contains(subtype) && !includeRCS) continue;

            var dir = Base6Directions.GetFlippedDirection(thruster.Orientation.Forward);
            if (!showAllDirections && dir != controller.Orientation.Forward) continue;

            thrustByDir[dir] += thruster.MaxEffectiveThrust;
            fuelByDir[dir] += lps;
        }

        foreach (var dir in displayOrder)
        {
            double fuelRate = fuelByDir[dir];
            if (fuelRate <= 0) continue;

            double fuelRateKg = fuelRate * HYDROGEN_DENSITY_KG_PER_L;
            double burnTime = fuelMass / fuelRateKg;
            sb.AppendFormat("{0,-12}: {1,0:N0} sec\n", dir, burnTime);
        }
    }

    lcd.WriteText(sb.ToString());
}

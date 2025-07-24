/*
=========================================
Delta-V HUD Script for Hydrogen Ships
=========================================
Displays delta-V (\u0394v) and hydrogen thruster burn times by group or direction
on a cockpit HUD-compatible LCD, with toggle status and runtime logging.

-- HOW TO SET UP IN-GAME --
1. Place a Text Panel and add this to name: "DeltaV"
2. Use a cockpit named with "deltavcockpit" or main cockpit
3. Group hydrogen thrusters using Terminal Groups:
   Example: All Drives, Boost, Efficient, Braking
4. Load this script into a Programmable Block
5. After running the script, configure groups, HUD position, and header display in the Programmable Block's CustomData:
6. Use arguments (rcs, burntime, etc.) in toolbar/timer blocks

-- ARGUMENTS --
rcs                     - Toggle inclusion of RCS thrusters
burntime                - Toggle display of burn times
all, alldirections      - Toggle all-direction vs forward-only
*/

const double HYDROGEN_DENSITY_KG_PER_L = 0.01;

// --Defaults--
// Delta-V
List<string> groupNames = new List<string> { "All Drives", "Boost", "Efficient", "Braking" };
// LCD
string HUDLCD_TOPRIGHT_TEMPLATE = "hudlcd:0.60:0.98:{0}";
string HUDLCD_TOPLEFT_TEMPLATE  = "hudlcd:-0.95:0.95:{0}";
string lcdColor = "225,225,225"; // Default white
string lcdFont = "Monospace";    // Default font
string lcdName = "DeltaV"; // Name to include on the LCD panel
bool showBorders = true; // Add this global variable
bool showHeaderAndSettings = true;
string hudPosition = "topright"; // topright or topleft
string lcdHudSize = "0.8"; // Default HUD size for hudlcd line

// Runtime settings
bool includeRCS = true;
bool displayBurnTimes = true;
bool showAllDirections = true;

IMyTextPanel lcd;
IMyShipController controller;
List<IMyThrust> allThrusters = new List<IMyThrust>();
List<IMyGasTank> tanks = new List<IMyGasTank>();
List<IMyBlockGroup> allGroups = new List<IMyBlockGroup>();
StringBuilder statusLog = new StringBuilder();

HashSet<string> rcsSubtypes = new HashSet<string>(new[]
    { "LynxRcsThruster1", "AryxRCSRamp", "AryxRCSHalfRamp", "AryxRCSSlant", "AryxRCS", "RCS2Bare", "RCS2Cube",
    "RCS2Half", "RCS2Slope", "RCS2SlopeTip1", "RCS2SlopeTip2", "RCS21x2Slope1", "RCS21x2Slope2" });

Dictionary<string, double> fuelUsageLpsBySubtypeId = new Dictionary<string, double> {
    {"ARYLYNX_SILVERSMITH_DRIVE", 1560}, {"ARYLNX_QUADRA_Epstein_Drive", 521.74},
    {"LargeBlockLargeHydrogenThrust", 2000}, {"LargeBlockSmallHydrogenThrust", 388.89},
    {"ARYLNX_RAIDER_Epstein_Drive", 1120}, {"ARYLNX_DRUMMER_Epstein_Drive", 1750},
    {"ARYLNX_PNDR_Epstein_Drive", 645.16}, {"ARYLNX_Mega_Epstein_Drive", 9090},
    {"ARYLNX_Epstein_Drive", 1030}, {"ARYLNX_ROCI_Epstein_Drive", 2110},
    {"ARYLNX_SCIRCOCCO_Epstein_Drive", 3110}, {"ARYLNX_MUNR_Epstein_Drive", 833.33},
    {"2x2ThrusterZAC", 933.33}, {"Large5x5RocketThruster", 3000},
    {"LargeTripleThrusterHeat", 1500}, {"LargeTripleThrusterHeatNoShroud", 1500},
    {"LynxRcsThruster1", 272.73}, {"AryxRCSRamp", 272.73}, {"AryxRCSHalfRamp", 272.73},
    {"AryxRCSSlant", 272.73}, {"AryxRCS", 272.73}, {"RCS2Bare", 227.27},
    {"RCS2Cube", 227.27}, {"RCS2Half", 227.27}, {"RCS2Slope", 227.27},
    {"RCS2SlopeTip1", 227.27}, {"RCS2SlopeTip2", 227.27}, {"RCS21x2Slope1", 227.27},
    {"RCS21x2Slope2", 227.27}
};

public Program() {
    Runtime.UpdateFrequency = UpdateFrequency.Update100;

    // If PB CustomData is empty, set the template
    if (string.IsNullOrWhiteSpace(Me.CustomData)) {
        Me.CustomData =
@"DeltaV Settings
groups=All Drives,Boost,Efficient,Braking
------- AnyGroupName seperated by Comma --------

Hud Settings
hudPosition=topright
----- topright, topleft ------

showHeaderAndSettings=true
----- true, false -------------

lcdColor=225,225,225
----- R,G,B ---------------------

lcdFont=Monospace
------ Monospace, Debug ----

showBorders=true
------ false, true --------

lcdHudSize=0.8
------ LCD Size (0.5 - 2.0) ------
";
    }

    // Parse config from programmable block CustomData
    ParseConfig(Me.CustomData);

    if (!string.IsNullOrWhiteSpace(Storage)) {
        var parts = Storage.Split(';');
        if (parts.Length == 3) {
            bool.TryParse(parts[0], out includeRCS);
            bool.TryParse(parts[1], out displayBurnTimes);
            bool.TryParse(parts[2], out showAllDirections);
        }
    }

    // Find LCD panel by name containing ".DeltaV" or " DeltaV"
    var lcds = new List<IMyTextPanel>();
    GridTerminalSystem.GetBlocksOfType(lcds, p =>
        p.IsFunctional && p.CubeGrid == Me.CubeGrid &&
        (p.CustomName.IndexOf("." + lcdName, StringComparison.OrdinalIgnoreCase) >= 0 ||
         p.CustomName.IndexOf(lcdName, StringComparison.OrdinalIgnoreCase) >= 0 ||
         p.CustomName.IndexOf(" " + lcdName, StringComparison.OrdinalIgnoreCase) >= 0)
    );
    lcd = lcds.FirstOrDefault();
    if (lcd == null)
        throw new Exception("No LCD with '.DeltaV' or ' DeltaV' in its name found or not functional.");

    lcd.ContentType = ContentType.TEXT_AND_IMAGE;
    lcd.Font = lcdFont;
    lcd.FontSize = 1.2f;

    // Set LCD color if possible
    var colorParts = lcdColor.Split(',');
    if (colorParts.Length == 3) {
        float r, g, b;
        if (float.TryParse(colorParts[0], out r) && float.TryParse(colorParts[1], out g) && float.TryParse(colorParts[2], out b)) {
            lcd.FontColor = new Color(r / 255f, g / 255f, b / 255f);
        }
    }

    // Ensure hudlcd: line is present and matches script's hudPosition
    string desiredHudLine = (hudPosition == "topleft" ? HUDLCD_TOPLEFT_TEMPLATE : HUDLCD_TOPRIGHT_TEMPLATE);
    var customLines = lcd.CustomData.Split('\n').ToList();
    int hudLineIndex = customLines.FindIndex(l => l.Trim().ToLower().StartsWith("hudlcd:"));

    if (hudLineIndex >= 0) {
        if (customLines[hudLineIndex].Trim() != desiredHudLine.Trim()) {
            customLines[hudLineIndex] = desiredHudLine;
        }
    } else {
        customLines.Insert(0, desiredHudLine);
    }

    lcd.CustomData = string.Join("\n", customLines);

    var cockpits = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType(cockpits, c => c.CubeGrid == Me.CubeGrid);
    controller = cockpits.FirstOrDefault(c => c.CustomName.ToLower().Contains("deltavcockpit"))
              ?? cockpits.FirstOrDefault(c => c.IsUnderControl)
              ?? cockpits.FirstOrDefault(c => c.IsMainCockpit);

    if (controller == null) throw new Exception("No valid cockpit found.");

    GridTerminalSystem.GetBlocksOfType(allThrusters, t => t.IsFunctional && t.CubeGrid == Me.CubeGrid);
    GridTerminalSystem.GetBlocksOfType(tanks, t => t.IsFunctional && t.CubeGrid == Me.CubeGrid && t.DefinitionDisplayNameText.ToLower().Contains("hydrogen"));
    GridTerminalSystem.GetBlockGroups(allGroups);

    Echo("ΔV HUD Loaded");
    Echo($"RCS: {(includeRCS ? "ON" : "OFF")}");
    Echo($"Burn Times: {(displayBurnTimes ? "ON" : "OFF")}");
    Echo($"All Directions: {(showAllDirections ? "ON" : "OFF")}");
    Echo($"LCD Found: {(lcd != null ? lcd.CustomName : "NOT FOUND")}");
    Echo($"Controller: {(controller != null ? controller.CustomName : "NOT FOUND")}");
}

void Save() {
    Storage = $"{includeRCS};{displayBurnTimes};{showAllDirections}";
}

void ParseConfig(string customData) {
    var lines = customData.Split('\n');
    foreach (var line in lines) {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("groups=", StringComparison.OrdinalIgnoreCase)) {
            var val = trimmed.Substring("groups=".Length);
            groupNames = val.Split(',').Select(g => g.Trim()).Where(g => !string.IsNullOrEmpty(g)).ToList();
        } else if (trimmed.StartsWith("hudPosition=", StringComparison.OrdinalIgnoreCase)) {
            var val = trimmed.Substring("hudPosition=".Length).Trim().ToLower();
            if (val == "topleft" || val == "topright") hudPosition = val;
        } else if (trimmed.StartsWith("showHeaderAndSettings=", StringComparison.OrdinalIgnoreCase)) {
            var val = trimmed.Substring("showHeaderAndSettings=".Length).Trim().ToLower();
            showHeaderAndSettings = (val == "true" || val == "1" || val == "yes");
        } else if (trimmed.StartsWith("lcdColor=", StringComparison.OrdinalIgnoreCase)) {
            lcdColor = trimmed.Substring("lcdColor=".Length).Trim();
        } else if (trimmed.StartsWith("lcdFont=", StringComparison.OrdinalIgnoreCase)) {
            lcdFont = trimmed.Substring("lcdFont=".Length).Trim();
        } else if (trimmed.StartsWith("showBorders=", StringComparison.OrdinalIgnoreCase)) {
            var val = trimmed.Substring("showBorders=".Length).Trim().ToLower();
            showBorders = (val == "true" || val == "1" || val == "yes");
        } else if (trimmed.StartsWith("lcdHudSize=", StringComparison.OrdinalIgnoreCase)) {
            lcdHudSize = trimmed.Substring("lcdHudSize=".Length).Trim();
        }
    }
}

void Main(string arg, UpdateType updateSource) {
    if (lcd == null || !lcd.IsFunctional) return;

    // Re-parse config in case CustomData changed
    ParseConfig(Me.CustomData);

    // Build hudlcd line using configured size
    string desiredHudLine = (hudPosition == "topleft"
        ? string.Format(HUDLCD_TOPLEFT_TEMPLATE, lcdHudSize)
        : string.Format(HUDLCD_TOPRIGHT_TEMPLATE, lcdHudSize));

    var lines = lcd.CustomData.Split('\n').ToList();
    int hudLineIndex = lines.FindIndex(l => l.Trim().ToLower().StartsWith("hudlcd:"));
    if (hudLineIndex >= 0) {
        if (lines[hudLineIndex].Trim() != desiredHudLine.Trim()) {
            lines[hudLineIndex] = desiredHudLine;
            lcd.CustomData = string.Join("\n", lines);
        }
    } else {
        lines.Insert(0, desiredHudLine);
        lcd.CustomData = string.Join("\n", lines);
    }

    // Handle input arguments (accept multiple variants)
    string normalizedArg = (arg ?? "").Trim().ToLower();
    switch (normalizedArg) {
        case "rcs":
        case "rcstoggle":
        case "togglercs":
            includeRCS = !includeRCS;
            statusLog.AppendLine($"[Toggle] RCS: {(includeRCS ? "ON" : "OFF")}");
            Save();
            break;
        case "burn":
        case "time":
        case "burntime":
        case "burntimetoggle":
        case "toggleburntime":
            displayBurnTimes = !displayBurnTimes;
            statusLog.AppendLine($"[Toggle] Burn Times: {(displayBurnTimes ? "ON" : "OFF")}");
            Save();
            break;
        case "all":
        case "directions":
        case "alldirections":
        case "alldirectionstoggle":
        case "togglealldirections":
            showAllDirections = !showAllDirections;
            statusLog.AppendLine($"[Toggle] All Directions: {(showAllDirections ? "ON" : "OFF")}");
            Save();
            break;
    }

    var mass = controller.CalculateShipMass();
    double m0 = mass.TotalMass;
    double fuelMass = tanks.Sum(t => t.Capacity * t.FilledRatio * HYDROGEN_DENSITY_KG_PER_L);
    double mf = m0 - fuelMass;

    var sb = new StringBuilder();
    int boxWidth = 34;
    string topLine = showBorders ? "╔" + new string('═', boxWidth - 2) + "╗" : "";
    string midLine = showBorders ? "╠" + new string('═', boxWidth - 2) + "╣" : "";
    string sepLine = showBorders ? "╟" + new string('─', 15) + "┬" + new string('─', 15) + "╢" : "";
    string botLine = showBorders ? "╚" + new string('═', boxWidth - 2) + "╝" : "";

    if (showBorders) sb.AppendLine(topLine);
    sb.AppendLine(PadCenter("ΔV HUD", boxWidth, showBorders ? '║' : ' '));
    if (showBorders) sb.AppendLine(midLine);
    if (showHeaderAndSettings) {
        string header = $"RCS: {(includeRCS ? "ON" : "OFF"),-3}  Burn: {(displayBurnTimes ? "ON" : "OFF"),-3}  Dir: {(showAllDirections ? "ALL" : "FWD"),-3}";
        sb.AppendLine(PadCenter(header, boxWidth, showBorders ? '║' : ' '));
        if (showBorders) sb.AppendLine(midLine);
    }
    if (mf <= 0 || m0 <= 0 || mf >= m0) {
        sb.AppendLine(PadCenter("ΔV unavailable: invalid mass/fuel", boxWidth, showBorders ? '║' : ' '));
        if (showBorders) sb.AppendLine(botLine);
        lcd.WriteText(sb.ToString());
        return;
    }

    // Show Delta-V by Group regardless of displayBurnTimes
    if (showBorders)
        sb.AppendLine(sepLine);

    if (groupNames.Count > 0)
    {
        sb.AppendLine(PadCenter("ΔV by Group:", boxWidth, showBorders ? '║' : ' '));

        foreach (var name in groupNames)
        {
            var group = allGroups.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (group == null)
            {
                string naLine = $" {name,-13} │ {"N/a",9}      ";
                sb.AppendLine(PadSides(naLine, boxWidth, showBorders ? '║' : ' '));
                continue;
            }

            var blocks = new List<IMyTerminalBlock>();
            group.GetBlocks(blocks);
            double thrust = 0, fuelLps = 0;
            bool hasThruster = false;

            foreach (var block in blocks)
            {
                var thruster = block as IMyThrust;
                if (thruster == null) continue;

                string subtype = thruster.BlockDefinition.SubtypeName;
                double lps;
                if (!fuelUsageLpsBySubtypeId.TryGetValue(subtype, out lps)) continue;
                if (rcsSubtypes.Contains(subtype) && !includeRCS) continue;

                thrust += thruster.MaxEffectiveThrust;
                fuelLps += lps;
                hasThruster = true;
            }

            if (!hasThruster || thrust <= 0 || fuelLps <= 0)
            {
                string skipLine = $" {name,-13} │ {"N/a",9}      ";
                sb.AppendLine(PadSides(skipLine, boxWidth, showBorders ? '║' : ' '));
                continue;
            }

            double ve = thrust / (fuelLps * HYDROGEN_DENSITY_KG_PER_L);
            double dv = ve * Math.Log(m0 / mf);

            string groupLine = $" {name,-13} │ {dv,9:N0} m/s ";
            sb.AppendLine(PadSides(groupLine, boxWidth, showBorders ? '║' : ' '));
        }
    }
    else
    {
        sb.AppendLine(PadCenter("No groups defined in PB CustomData", boxWidth, showBorders ? '║' : ' '));
    }
    if (showBorders) sb.AppendLine(midLine);



    // Only show Burn Times if displayBurnTimes is ON
    if (displayBurnTimes) {
        sb.AppendLine(PadCenter("Burn Times (sec):", boxWidth, showBorders ? '║' : ' '));
        if (showBorders) sb.AppendLine(sepLine);

        var thrustByDir = new Dictionary<Base6Directions.Direction, double>();
        var fuelByDir = new Dictionary<Base6Directions.Direction, double>();
        foreach (Base6Directions.Direction dir in Enum.GetValues(typeof(Base6Directions.Direction))) {
            thrustByDir[dir] = 0;
            fuelByDir[dir] = 0;
        }

        foreach (var thruster in allThrusters) {
            string subtype = thruster.BlockDefinition.SubtypeName;
            double lps;
            if (!fuelUsageLpsBySubtypeId.TryGetValue(subtype, out lps)) continue;
            if (rcsSubtypes.Contains(subtype) && !includeRCS) continue;

            var dir = Base6Directions.GetFlippedDirection(thruster.Orientation.Forward);
            if (!showAllDirections && dir != controller.Orientation.Forward) continue;

            thrustByDir[dir] += thruster.MaxEffectiveThrust;
            fuelByDir[dir] += lps;
        }

        foreach (var kvp in fuelByDir) {
            if (kvp.Value <= 0) continue;
            double burn = fuelMass / (kvp.Value * HYDROGEN_DENSITY_KG_PER_L);
            string burnLine = $" {kvp.Key,-13} │ {burn,9:N0} sec ";
            sb.AppendLine(PadSides(burnLine, boxWidth, showBorders ? '║' : ' '));
        }
        if (showBorders) sb.AppendLine(botLine);
    } else {
        if (showBorders) sb.AppendLine(botLine);
    }

    if (statusLog.Length > 0) {
        sb.AppendLine();
        sb.Append("[Status]\n");
        sb.Append(statusLog.ToString());
        statusLog.Clear();
    }

    lcd.WriteText(sb.ToString());
}

// Helper to pad a line with spaces and add left/right box chars
string PadSides(string text, int width, char boxChar) {
    text = text.Length > width - 2 ? text.Substring(0, width - 2) : text;
    return boxChar + text.PadRight(width - 2, ' ') + (boxChar == ' ' ? "" : boxChar.ToString());
}

// Helper to center text in box and add left/right box chars
string PadCenter(string text, int width, char boxChar) {
    text = text.Length > width - 2 ? text.Substring(0, width - 2) : text;
    int pad = width - 2 - text.Length;
    int padLeft = pad / 2;
    int padRight = pad - padLeft;
    return boxChar + new string(' ', padLeft) + text + new string(' ', padRight) + (boxChar == ' ' ? "" : boxChar.ToString());
}

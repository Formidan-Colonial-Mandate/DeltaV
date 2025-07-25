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

// ========== Globals (deduplicated & cleaned) ==========

IMyShipController controller;
IMyTextPanel lcd;
List<IMyGasTank> tanks = new List<IMyGasTank>();
List<IMyThrust> allThrusters = new List<IMyThrust>();
List<IMyBlockGroup> allGroups = new List<IMyBlockGroup>();

List<string> groupNames = new List<string>();
StringBuilder statusLog = new StringBuilder();

// Constants
const double HYDROGEN_DENSITY_KG_PER_L = 0.01;

// Runtime settings
bool showBorders = true;
bool showHeaderAndSettings = true;
bool includeRCS = true;
bool displayBurnTimes = true;
bool showAllDirections = true;
bool fastBoot = true;

// LCD configuration
string hudPosition = "topright";
string lcdHudSize = "0.8";
string lcdColor = "225,225,225";
string lcdFont = "Monospace";
string lcdName = "DeltaV";
string HUDLCD_TOPRIGHT_TEMPLATE = "hudlcd:0.60:0.98:{0}";
string HUDLCD_TOPLEFT_TEMPLATE  = "hudlcd:-0.95:0.95:{0}";

// Boot
bool booting = true;
int bootFrame = 0;
int bootTickCounter = 0;
const int BOOT_TICKS_PER_FRAME = 5; // 10 ticks × 5 = ~50 ticks
readonly string[] bootSpinner = new[] { "|", "/", "-", "\\" };
readonly string[] bootSteps = new[] {
    "Boot sequence initialized",
    "Loading HUD modules",
    "Validating LCD interface",
    "Linking cockpit controller",
    "Calculating fuel reserves",
    "Analyzing thruster config",
    "Generating burn tables",
    "Arming on-board explosives",
    "Checking for valid EnCorp software license",
    "License confirmed.",
    "Disarming on-board explosives",
    "Gathering the courage to hate the poor",
    "Launching ΔV HUD"
};

readonly int[] bootStepDurations = new[] {
    1, 1, 1, 1, 1, 1, 1, // 0–6
    3, // "Arming Explosives"
    2, // "Checking for valid license"
    1, // "license confirmed"
    3, // "disarming explosives"
    2, // "courage to hate"
    1 // "activating DeltaV"  
};

int bootStepFrameCounter = 0;

bool GetMassStats(out double m0, out double mf, out double fuelMass) {
    var mass = controller.CalculateShipMass();
    m0 = mass.TotalMass;
    fuelMass = tanks.Sum(t => t.Capacity * t.FilledRatio * HYDROGEN_DENSITY_KG_PER_L);
    mf = m0 - fuelMass;
    return !(mf <= 0 || m0 <= 0 || mf >= m0);
}

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
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

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

    
   var cockpits = new List<IMyShipController>();
GridTerminalSystem.GetBlocksOfType(cockpits, c =>
    c.CubeGrid == Me.CubeGrid && c.CanControlShip);
controller = cockpits.FirstOrDefault(c => c.CustomName.ToLower().Contains("deltavcockpit"));
if (controller == null)
    controller = cockpits.FirstOrDefault(c => c.IsUnderControl);
if (controller == null)
    controller = cockpits.FirstOrDefault(c => c.IsMainCockpit);
if (controller == null && cockpits.Count > 0)
    controller = cockpits[0];
if (controller == null)
{
    throw new Exception("No valid cockpit found.\n\n" +
        "To fix: Place a Cockpit on the grid oriented in your preferred direction.");
}


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


void Main(string arg, UpdateType updateSource) {
    if (lcd == null || !lcd.IsFunctional) return;

    ParseConfig(Me.CustomData);
    
    // Refresh block groups at runtime to catch newly added or renamed groups
    allGroups.Clear();
    GridTerminalSystem.GetBlockGroups(allGroups);

    UpdateHudLineIfChanged();
    HandleToggleArguments(arg);

   if (booting) {
    if (RunBootAnimation()) return;
    // Boot finished
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

    double m0, mf, fuelMass;
    if (!GetMassStats(out m0, out mf, out fuelMass)) {
        DisplayError("ΔV unavailable: invalid mass/fuel");
        return;
    }

    var sb = new StringBuilder();
    int boxWidth = 34;

    if (showBorders) sb.AppendLine(GetBoxLine("top", boxWidth));
    sb.AppendLine(PadCenter("ΔV HUD", boxWidth, showBorders ? '║' : ' '));
    if (showBorders) sb.AppendLine(GetBoxLine("mid", boxWidth));

    if (showHeaderAndSettings) AppendHeaderAndSettings(sb, boxWidth);
    if (showBorders) sb.AppendLine(GetBoxLine("mid", boxWidth));

    RenderDeltaVByGroup(sb, m0, fuelMass, boxWidth);

    if (displayBurnTimes) {
        if (showBorders) sb.AppendLine(GetBoxLine("sep", boxWidth));
        RenderBurnTimes(sb, fuelMass, boxWidth);
    }

    if (showBorders) sb.AppendLine(GetBoxLine("bot", boxWidth));

    AppendStatusLog(sb);
    lcd.WriteText(sb.ToString());

    if (!booting) {
    string freq = Runtime.UpdateFrequency.HasFlag(UpdateFrequency.Update100) ? "Update100" :
                  Runtime.UpdateFrequency.HasFlag(UpdateFrequency.Update10)  ? "Update10" :
                  Runtime.UpdateFrequency.HasFlag(UpdateFrequency.Update1)   ? "Update1" :
                  "Unknown";
}
}



void Save() {
    Storage = $"{includeRCS};{displayBurnTimes};{showAllDirections}";
}

void ParseConfig(string customData) {
    groupNames.Clear();

    var lines = customData.Split('\n');
    foreach (var line in lines) {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("groups=", StringComparison.OrdinalIgnoreCase)) {
            var val = trimmed.Substring("groups=".Length);
            groupNames = val.Split(',')
                            .Select(g => g.Trim())
                            .Where(g => g.Length > 0)
                            .ToList();
            foreach (var g in groupNames) {
            }
        } else if (trimmed.StartsWith("hudPosition=", StringComparison.OrdinalIgnoreCase)) {
            var val = trimmed.Substring("hudPosition=".Length).Trim().ToLower();
            if (val == "topleft" || val == "topright") hudPosition = val;
        } else if (trimmed.StartsWith("showHeaderAndSettings=", StringComparison.OrdinalIgnoreCase)) {
            showHeaderAndSettings = trimmed.Substring("showHeaderAndSettings=".Length).Trim().ToLower() == "true";
        } else if (trimmed.StartsWith("lcdColor=", StringComparison.OrdinalIgnoreCase)) {
            lcdColor = trimmed.Substring("lcdColor=".Length).Trim();
        } else if (trimmed.StartsWith("lcdFont=", StringComparison.OrdinalIgnoreCase)) {
            lcdFont = trimmed.Substring("lcdFont=".Length).Trim();
        } else if (trimmed.StartsWith("showBorders=", StringComparison.OrdinalIgnoreCase)) {
            showBorders = trimmed.Substring("showBorders=".Length).Trim().ToLower() == "true";
        } else if (trimmed.StartsWith("lcdHudSize=", StringComparison.OrdinalIgnoreCase)) {
            lcdHudSize = trimmed.Substring("lcdHudSize=".Length).Trim();
        }
    }
}

string PadCenter(string text, int width, char pad = ' ') {
    text = text.Length > width - 2 ? text.Substring(0, width - 2) : text;
    int padTotal = width - 2 - text.Length;
    int padLeft = padTotal / 2;
    int padRight = padTotal - padLeft;
    return pad + new string(' ', padLeft) + text + new string(' ', padRight) + (pad == ' ' ? "" : pad.ToString());
}

string PadSides(string text, int width, char pad = ' ') {
    text = text.Length > width - 2 ? text.Substring(0, width - 2) : text;
    return pad + text.PadRight(width - 2) + (pad == ' ' ? "" : pad.ToString());
}

List<string> WrapLines(string input, int maxWidth) {
    var lines = new List<string>();
    string remaining = input;

    while (remaining.Length > maxWidth) {
        int split = remaining.LastIndexOf(' ', maxWidth);
        if (split <= 0) split = maxWidth;
        lines.Add(remaining.Substring(0, split).TrimEnd());
        remaining = remaining.Substring(split).TrimStart();
    }

    if (remaining.Length > 0)
        lines.Add(remaining);

    return lines;
}


void HandleToggleArguments(string arg) {
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
            statusLog.AppendLine($"[Toggle] All Directions: {(showAllDirections ? "ALL" : "FWD")}");
            Save();
            break;
    }
}

void UpdateHudLineIfChanged() {
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
}

bool RunBootAnimation() {
    if (!booting) return false;

    // Throttle actual frame rate (manual Update50 simulation)
    bootTickCounter++;
    if (bootTickCounter < BOOT_TICKS_PER_FRAME) return true;
    bootTickCounter = 0;

    int boxBootWidth = 34;
    int barLen = 24;

    // Step index is tied to bootFrame
    int stepIndex = Math.Min(bootFrame, bootSteps.Length - 1);

    string step = bootSteps[stepIndex];
    string spin = bootSpinner[bootFrame % bootSpinner.Length];

    // Loading bar
    int filled = Math.Min((bootFrame * barLen) / (bootSteps.Length + 2), barLen);
    string bar = "[" + new string('#', filled) + new string('-', barLen - filled) + "]";

    // Render
    var bootSb = new StringBuilder();
    bootSb.AppendLine();
    bootSb.AppendLine(PadCenter("ΔV HUD", boxBootWidth, ' '));
    bootSb.AppendLine();
    bootSb.AppendLine(PadCenter(spin, boxBootWidth, ' '));
    foreach (var line in WrapLines(step, boxBootWidth - 2))
        bootSb.AppendLine(PadCenter(line, boxBootWidth, ' '));
    bootSb.AppendLine();
    bootSb.AppendLine(PadCenter(bar, boxBootWidth, ' '));
    bootSb.AppendLine();

    lcd.WriteText(bootSb.ToString());

    // Advance boot frame only when duration is met
    bootStepFrameCounter++;
    if (bootStepFrameCounter >= bootStepDurations[Math.Min(stepIndex, bootStepDurations.Length - 1)]) {
        bootStepFrameCounter = 0;
        bootFrame++;
    }

    if (bootFrame >= bootSteps.Length + 3) {
        booting = false;
        fastBoot = false;
        Runtime.UpdateFrequency = UpdateFrequency.Update100;
    }

    return true;
}




void RenderDeltaVByGroup(StringBuilder sb, double m0, double fuelMass, int boxWidth) {
    if (showBorders) sb.AppendLine(GetBoxLine("sep", boxWidth));

    if (groupNames.Count == 0) {
        sb.AppendLine(PadCenter("No groups defined in PB CustomData", boxWidth, showBorders ? '║' : ' '));
        return;
    }

    sb.AppendLine(PadCenter("ΔV by Group:", boxWidth, showBorders ? '║' : ' '));

    double mf = m0 - fuelMass;
    if (mf <= 0 || m0 <= 0 || mf >= m0) {
        sb.AppendLine(PadCenter("ΔV unavailable: invalid mass/fuel", boxWidth, showBorders ? '║' : ' '));
        return;
    }

    foreach (var name in groupNames) {
        var group = allGroups.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (group == null) {
            string naLine = $" {name,-13} │ {"N/a",9}      ";
            sb.AppendLine(PadSides(naLine, boxWidth, showBorders ? '║' : ' '));
            continue;
        }

        var blocks = new List<IMyTerminalBlock>();
        group.GetBlocks(blocks);
        double thrust = 0, fuelLps = 0;
        bool hasThruster = false;

        foreach (var block in blocks) {
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

        if (!hasThruster || thrust <= 0 || fuelLps <= 0) {
            string skipLine = $" {name,-13} │ {"N/a",9}      ";
            sb.AppendLine(PadSides(skipLine, boxWidth, showBorders ? '║' : ' '));
            continue;
        }

        double ve = thrust / (fuelLps * HYDROGEN_DENSITY_KG_PER_L);
        double dv = ve * Math.Log(m0 / mf);

        string groupLine = $" {name,-13} │ {dv,9:N0} m/s ";
        sb.AppendLine(PadSides(groupLine, boxWidth, showBorders ? '║' : ' '));
    }

    if (showBorders) sb.AppendLine(GetBoxLine("mid", boxWidth));
}


void RenderBurnTimes(StringBuilder sb, double fuelMass, int boxWidth) {
    sb.AppendLine(PadCenter("Burn Times (sec):", boxWidth, showBorders ? '║' : ' '));
    if (showBorders) sb.AppendLine(GetBoxLine("sep", boxWidth));

    var thrustByDir = new Dictionary<Base6Directions.Direction, double>();
    var fuelByDir = new Dictionary<Base6Directions.Direction, double>();

    foreach (Base6Directions.Direction dir in Enum.GetValues(typeof(Base6Directions.Direction))) {
        thrustByDir[dir] = 0;
        fuelByDir[dir] = 0;
    }

    foreach (var thruster in allThrusters) {
        if (!thruster.Enabled) continue;
        
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
}

void AppendStatusLog(StringBuilder sb) {
    if (statusLog.Length == 0) return;
    sb.AppendLine();
    sb.Append("[Status]\n");
    sb.Append(statusLog.ToString());
    statusLog.Clear();
}

void DisplayError(string message) {
    var sb = new StringBuilder();
    int boxWidth = 34;
    if (showBorders) sb.AppendLine(GetBoxLine("top", boxWidth));
    sb.AppendLine(PadCenter("ΔV HUD", boxWidth, showBorders ? '║' : ' '));
    if (showBorders) sb.AppendLine(GetBoxLine("mid", boxWidth));
    sb.AppendLine(PadCenter(message, boxWidth, showBorders ? '║' : ' '));
    if (showBorders) sb.AppendLine(GetBoxLine("bot", boxWidth));
    lcd.WriteText(sb.ToString());
}

string GetBoxLine(string type, int width) {
    switch (type.ToLower()) {
        case "top":
            return "╔" + new string('═', width - 2) + "╗";
        case "mid":
            return "╠" + new string('═', width - 2) + "╣";
        case "sep":
            int innerWidth = width - 3;
            int split = innerWidth / 2;
            int remainder = innerWidth % 2;
            return "╟" + new string('─', split) + "┬" + new string('─', split + remainder) + "╢";

        case "bot":
            return "╚" + new string('═', width - 2) + "╝";
        default:
            return new string(' ', width);
    }
}

void AppendHeaderAndSettings(StringBuilder sb, int boxWidth) {
    string header = $"RCS: {(includeRCS ? "ON" : "OFF"),-3}  Burn: {(displayBurnTimes ? "ON" : "OFF"),-3}  Dir: {(showAllDirections ? "ALL" : "FWD"),-3}";
    sb.AppendLine(PadCenter(header, boxWidth, showBorders ? '║' : ' '));
}

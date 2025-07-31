/*
 * // =========================================
 * // Delta-V HUD Script for Hydrogen Ships
 * // =========================================
 * // Displays delta-V (\u0394v) and hydrogen thruster burn times by group or direction
 * // on a cockpit HUD-compatible LCD, with toggle status and runtime logging.
 * // 
 * // -- HOW TO SET UP IN-GAME --
 * // 1. Place a Text Panel and add this to name: "DeltaV"
 * // 2. Use a cockpit named with "deltavcockpit" or main cockpit
 * // 3. Group hydrogen thrusters using Terminal Groups:
 * //    Example: All Drives, Boost, Efficient, Braking
 * // 4. Load this script into a Programmable Block
 * // 5. After running the script, configure groups, HUD position, and header display in the Programmable Block's CustomData:
 * // 6. Run command "setup" to have the script read the block's changed custom data
 * // 7. Use arguments (rcs, burntime, etc.) in toolbar/timer blocks
 * // 
 * // -- ARGUMENTS --
 * // rcs                     - Toggle inclusion of RCS thrusters
 * // burntime                - Toggle display of burn times
 * // all, alldirections      - Toggle all-direction vs forward-only
 * // setup                   - Have the script re-read the custom data settings for changes
 * // ========== Globals (deduplicated & cleaned) ==========
 */
IMyShipController ç;IMyTextPanel è;List<IMyGasTank>é=new List<IMyGasTank>();List<IMyThrust>ê=new List<IMyThrust>();List<
IMyBlockGroup>ë=new List<IMyBlockGroup>();List<string>ì=new List<string>();StringBuilder í=new StringBuilder();const double î=0.01;
bool ï=true;bool ð=true;bool ñ=true;bool ò=true;bool ó=true;string ô="0.8";string õ="DeltaV";Vector2 ö=new Vector2(0.6f,
0.98f);MyIni ø=new MyIni();const string ú="DeltaV";const string ù="Groups";const string æ="HudPositionX";const string å=
"HudPositionY";const string Ö="ShowHeaderAndSettings";const string Ø="LcdColor";const string Ù="LcdFont";const string Ú="ShowBorders";
const string Û="LcdHudSize";IEnumerator<bool>Ü;int Ý=0;int Þ=0;const int ß=5;string[]à=new[]{"|","/","-","\\"};string[]á=new[
]{"Boot sequence initialized","Loading HUD modules","Validating LCD interface","Linking cockpit controller",
"Calculating fuel reserves","Analyzing thruster config","Generating burn tables","Arming on-board explosives",
"Checking for valid EnCorp software license","License confirmed.","Disarming on-board explosives","Gathering the courage to hate the poor","Launching ΔV HUD",};int[
]â=new[]{1,1,1,1,1,1,1,3,2,1,3,2,1,};int ã=0;bool ä(out double M,out double P,out double N){var Õ=ç.CalculateShipMass();M
=Õ.TotalMass;N=é.Sum(û=>û.Capacity*û.FilledRatio*î);P=M-N;return!(P<=0||M<=0||P>=M);}HashSet<string>ü=new HashSet<string>
(new[]{"LynxRcsThruster1","AryxRCSRamp","AryxRCSHalfRamp","AryxRCSSlant","AryxRCS","RCS2Bare","RCS2Cube","RCS2Half",
"RCS2Slope","RCS2SlopeTip1","RCS2SlopeTip2","RCS21x2Slope1","RCS21x2Slope2",});Dictionary<string,double>ć=new Dictionary<string,
double>{{"ARYLYNX_SILVERSMITH_DRIVE",1560},{"ARYLNX_QUADRA_Epstein_Drive",521.74},{"LargeBlockLargeHydrogenThrust",2000},{
"LargeBlockSmallHydrogenThrust",388.89},{"ARYLNX_RAIDER_Epstein_Drive",1120},{"ARYLNX_DRUMMER_Epstein_Drive",1750},{"ARYLNX_PNDR_Epstein_Drive",645.16}
,{"ARYLNX_Mega_Epstein_Drive",4000},{"ARYLNX_Epstein_Drive",1030},{"ARYLNX_ROCI_Epstein_Drive",2110},{
"ARYLNX_SCIRCOCCO_Epstein_Drive",3110},{"ARYLNX_MUNR_Epstein_Drive",833.33},{"2x2ThrusterZAC",933.33},{"Large5x5RocketThruster",3000},{
"LargeTripleThrusterHeat",1500},{"LargeTripleThrusterHeatNoShroud",1500},{"LynxRcsThruster1",156.25},{"AryxRCSRamp",156.25},{"AryxRCSHalfRamp",
156.25},{"AryxRCSSlant",156.25},{"AryxRCS",156.25},{"RCS2Bare",227.27},{"RCS2Cube",227.27},{"RCS2Half",227.27},{"RCS2Slope",
227.27},{"RCS2SlopeTip1",227.27},{"RCS2SlopeTip2",227.27},{"RCS21x2Slope1",227.27},{"RCS21x2Slope2",227.27},};enum Ĉ{ĉ,Ċ,ċ,Č}
Program(){Runtime.UpdateFrequency=UpdateFrequency.Update10;Ü=č().GetEnumerator();}IEnumerable<bool>č(){var Ď=new List<
IMyTextPanel>();GridTerminalSystem.GetBlocksOfType(Ď,ď=>ď.IsFunctional&&ď.CubeGrid==Me.CubeGrid&&(ď.CustomName.IndexOf("."+õ,
StringComparison.OrdinalIgnoreCase)>=0||ď.CustomName.IndexOf(õ,StringComparison.OrdinalIgnoreCase)>=0||ď.CustomName.IndexOf(" "+õ,
StringComparison.OrdinalIgnoreCase)>=0));è=Ď.FirstOrDefault();if(è==null)throw new Exception(
$"No LCD with '{õ}' in its name found or not functional.");è.ContentType=ContentType.TEXT_AND_IMAGE;S();yield return true;Ā();S();yield return true;if(!string.IsNullOrWhiteSpace
(Storage)){var Đ=Storage.Split(';');if(Đ.Length==3){bool.TryParse(Đ[0],out ñ);bool.TryParse(Đ[1],out ò);bool.TryParse(Đ[2
],out ó);}}S();yield return true;var đ=new List<IMyShipController>();GridTerminalSystem.GetBlocksOfType(đ,Ē=>Ē.CubeGrid==
Me.CubeGrid&&Ē.CanControlShip);S();yield return true;ç=đ.FirstOrDefault(Ē=>Ē.CustomName.ToLower().Contains("deltavcockpit"
));if(ç==null)ç=đ.FirstOrDefault(Ē=>Ē.IsUnderControl);if(ç==null)ç=đ.FirstOrDefault(Ē=>Ē.IsMainCockpit);if(ç==null&&đ.
Count>0)ç=đ[0];if(ç==null){throw new Exception("No valid cockpit found.\n\n"+
"To fix: Place a Cockpit on the grid oriented in your preferred direction.");}S();yield return true;GridTerminalSystem.GetBlocksOfType(ê,û=>û.IsFunctional&&û.CubeGrid==Me.CubeGrid);S();yield
return true;GridTerminalSystem.GetBlocksOfType(é,û=>û.IsFunctional&&û.CubeGrid==Me.CubeGrid&&û.DefinitionDisplayNameText.
ToLower().Contains("hydrogen"));S();yield return true;GridTerminalSystem.GetBlockGroups(ë);Echo("ΔV HUD Loaded");Echo(
$"RCS: {(ñ?"ON":"OFF")}");Echo($"Burn Times: {(ò?"ON":"OFF")}");Echo($"All Directions: {(ó?"ON":"OFF")}");Echo(
$"LCD Found: {(è!=null?è.CustomName:"NOT FOUND")}");Echo($"Controller: {(ç!=null?ç.CustomName:"NOT FOUND")}");while(!S()){yield return true;}yield return false;}void Main
(string n,UpdateType Ć){if(Ü!=null){Þ++;if(Þ<ß)return;Þ=0;if(!Ü.MoveNext()||!Ü.Current){Ü.Dispose();Ü=null;Runtime.
UpdateFrequency=UpdateFrequency.Update100;}else{return;}}if(è==null||!è.IsFunctional)return;m(n);q();double M,P,N;if(!ä(out M,out P,out
N)){µ("ΔV unavailable: invalid mass/fuel");return;}var L=new StringBuilder();int O=34;if(ï)L.AppendLine(À(Ĉ.ĉ,O));L.
AppendLine(ą("ΔV HUD",O,ï?'║':' '));if(ï)L.AppendLine(À(Ĉ.Ċ,O));if(ð)Ä(L,O);if(ï)L.AppendLine(À(Ĉ.Ċ,O));K(L,M,N,O);if(ò){if(ï)L.
AppendLine(À(Ĉ.ċ,O));Ò(L,N,O);}if(ï)L.AppendLine(À(Ĉ.Č,O));ª(L);è.WriteText(L.ToString());}void Save(){Storage=$"{ñ};{ò};{ó}";}
void ý(){StringBuilder þ=new StringBuilder();for(int ÿ=0;ÿ<ì.Count-1;ÿ++){þ.Append(ì[ÿ]);þ.Append(",");}if(ì.Count>0)þ.
Append(ì[ì.Count-1]);ø.Set(ú,ù,þ.ToString());ø.SetComment(ú,ù,"------- Group Names are seperated by comma --------");ø.Set(ú,æ
,ö.X);ø.SetComment(ú,æ,"------- Value from -1 to 1, -1 is left, 1 is right -------");ø.Set(ú,å,ö.Y);ø.SetComment(ú,å,
"------- Value from -1 to 1, -1 is bottom, 1 is top -------");ø.Set(ú,Ö,ð);ø.SetComment(ú,Ö,"------- True, or False -------");ø.Set(ú,Ø,
$"{è.FontColor.X},{è.FontColor.Y},{è.FontColor.Z}");ø.SetComment(ú,Ø,"------- R,G,B -------");ø.Set(ú,Ù,è.Font);ø.SetComment(ú,Ù,"------- Monospace, Debug -------");ø.Set
(ú,Ú,ï);ø.SetComment(ú,Ú,"------- True, or False -------");ø.Set(ú,Û,ô);ø.SetComment(ú,Û,
"------- LCD Size (0.5 - 2.0) -------");Me.CustomData=ø.ToString();}void Ā(){ø.Clear();if(Me.CustomData==""||!ø.TryParse(Me.CustomData)||!ø.ContainsSection(ú)
){ì.Clear();ì.Add("All Drives");ì.Add("Boost");ì.Add("Efficient");ì.Add("Braking");è.Font="Monospace";è.FontColor=new
Color(225/255f);ý();return;}bool ā=false;if(ø.ContainsKey(ú,ù))ì=ø.Get(ú,ù).ToString().Split(',').Select(A=>A.Trim()).Where(A
=>A.Length>0).ToList();else{ì.Clear();ì.Add("All Drives");ì.Add("Boost");ì.Add("Efficient");ì.Add("Braking");ā=true;}if(ø.
ContainsKey(ú,æ))ö.X=ø.Get(ú,æ).ToSingle(0.6f);else ā=true;if(ø.ContainsKey(ú,å))ö.Y=ø.Get(ú,å).ToSingle(0.98f);else ā=true;if(ø.
ContainsKey(ú,Ö))ð=ø.Get(ú,Ö).ToBoolean(true);else ā=true;if(ø.ContainsKey(ú,Ø)){var Ă=ø.Get(ú,Ø).ToString().Split(',');if(Ă.Length
==3){float ă,A,Ą;if(!float.TryParse(Ă[0],out ă)||!float.TryParse(Ă[1],out A)||!float.TryParse(Ă[2],out Ą)){ā=true;è.
FontColor=new Color(225/255f);}else{è.FontColor=new Color(ă/255f,A/255f,Ą/255f);}}}else{è.FontColor=new Vector3(225/255f);}if(ø.
ContainsKey(ú,Ù))è.Font=ø.Get(ú,Ù).ToString();else ā=true;if(ø.ContainsKey(ú,Ú))ï=ø.Get(ú,Ú).ToBoolean(true);else ā=true;if(ø.
ContainsKey(ú,Û))ô=ø.Get(ú,Û).ToString();else ā=true;if(ā)ý();}string ą(string Y,int Z,char a=' '){Y=Y.Length>Z-2?Y.Substring(0,Z-2
):Y;int T=Z-2-Y.Length;int V=T/2;int W=T-V;return a+new string(' ',V)+Y+new string(' ',W)+(a==' '?"":a.ToString());}
string X(string Y,int Z,char a=' '){Y=Y.Length>Z-2?Y.Substring(0,Z-2):Y;return a+Y.PadRight(Z-2)+(a==' '?"":a.ToString());}
List<string>d(string e,int f){var h=new List<string>();string j=e;while(j.Length>f){int k=j.LastIndexOf(' ',f);if(k<=0)k=f;h
.Add(j.Substring(0,k).TrimEnd());j=j.Substring(k).TrimStart();}if(j.Length>0)h.Add(j);return h;}void m(string n){string o
=(n??"").Trim().ToLower();switch(o){case"rcs":case"rcstoggle":case"togglercs":ñ=!ñ;í.AppendLine(
$"[Toggle] RCS: {(ñ?"ON":"OFF")}");Save();break;case"burn":case"time":case"burntime":case"burntimetoggle":case"toggleburntime":ò=!ò;í.AppendLine(
$"[Toggle] Burn Times: {(ò?"ON":"OFF")}");Save();break;case"all":case"directions":case"alldirections":case"alldirectionstoggle":case"togglealldirections":ó=!ó;í
.AppendLine($"[Toggle] All Directions: {(ó?"ALL":"FWD")}");Save();break;case"setup":case"parseconfig":case"readconfig":Ā(
);ë.Clear();GridTerminalSystem.GetBlockGroups(ë);break;}}void q(){string s=$"hudlcd:{ö.X}:{ö.Y}:{ô}";var h=è.CustomData.
Split('\n').ToList();int u=h.FindIndex(U=>U.Trim().ToLower().StartsWith("hudlcd:"));if(u>=0){if(h[u].Trim()!=s.Trim()){h[u]=s
;è.CustomData=string.Join("\n",h);}}else{h.Insert(0,s);è.CustomData=string.Join("\n",h);}}bool S(){const int B=34;const
int C=24;int D=Math.Min(Ý,á.Length-1);string E=á[D];string F=à[Ý%à.Length];int G=Math.Min((Ý*C)/(á.Length+2),C);string H=
"["+new string('#',G)+new string('-',C-G)+"]";var I=new StringBuilder();I.AppendLine();I.AppendLine(ą("ΔV HUD",B,' '));I.
AppendLine();I.AppendLine(ą(F,B,' '));foreach(var J in d(E,B-2))I.AppendLine(ą(J,B,' '));I.AppendLine();I.AppendLine(ą(H,B,' '));I
.AppendLine();è.WriteText(I.ToString());ã++;if(ã>=â[Math.Min(D,â.Length-1)]){ã=0;Ý++;}if(Ý>=á.Length+3){return true;}
return false;}void K(StringBuilder L,double M,double N,int O){if(ï)L.AppendLine(À(Ĉ.ċ,O));if(ì.Count==0){L.AppendLine(ą(
"No groups defined in PB CustomData",O,ï?'║':' '));return;}L.AppendLine(ą("ΔV by Group:",O,ï?'║':' '));double P=M-N;if(P<=0||M<=0||P>=M){L.AppendLine(ą(
"ΔV unavailable: invalid mass/fuel",O,ï?'║':' '));return;}foreach(var Q in ì){var R=ë.FirstOrDefault(A=>A.Name.Equals(Q,StringComparison.OrdinalIgnoreCase)
);if(R==null){string v=$" {Q,-13} │ {"N/a",9}      ";L.AppendLine(X(v,O,ï?'║':' '));continue;}var w=new List<
IMyTerminalBlock>();R.GetBlocks(w);double È=0,É=0;bool Ê=false;foreach(var Ë in w){var Ì=Ë as IMyThrust;if(Ì==null)continue;string Í=Ì.
BlockDefinition.SubtypeName;double Ç;if(!ć.TryGetValue(Í,out Ç))continue;if(ü.Contains(Í)&&!ñ)continue;È+=Ì.MaxEffectiveThrust;É+=Ç;Ê=
true;}if(!Ê||È<=0||É<=0){string Î=$" {Q,-13} │ {"N/a",9}      ";L.AppendLine(X(Î,O,ï?'║':' '));continue;}double Ï=È/(É*î);
double Ð=Ï*Math.Log(M/P);string Ñ=$" {Q,-13} │ {Ð,9:N0} m/s ";L.AppendLine(X(Ñ,O,ï?'║':' '));}if(ï)L.AppendLine(À(Ĉ.Ċ,O));}
void Ò(StringBuilder L,double N,int O){L.AppendLine(ą("Burn Times (sec):",O,ï?'║':' '));if(ï)L.AppendLine(À(Ĉ.ċ,O));var Ó=
new Dictionary<Base6Directions.Direction,double>();var Ô=new Dictionary<Base6Directions.Direction,double>();foreach(
Base6Directions.Direction Å in Enum.GetValues(typeof(Base6Directions.Direction))){Ó[Å]=0;Ô[Å]=0;}foreach(var Ì in ê){if(!Ì.Enabled)
continue;string Í=Ì.BlockDefinition.SubtypeName;double Ç;if(!ć.TryGetValue(Í,out Ç))continue;if(ü.Contains(Í)&&!ñ)continue;var Å
=Base6Directions.GetFlippedDirection(Ì.Orientation.Forward);if(!ó&&Å!=ç.Orientation.Forward)continue;Ó[Å]+=Ì.
MaxEffectiveThrust;Ô[Å]+=Ç;}foreach(var x in Ô){if(x.Value<=0)continue;double y=N/(x.Value*î);string z=$" {x.Key,-13} │ {y,9:N0} sec ";L.
AppendLine(X(z,O,ï?'║':' '));}}void ª(StringBuilder L){if(í.Length==0)return;L.AppendLine();L.Append("[Status]\n");L.Append(í.
ToString());í.Clear();}void µ(string º){var L=new StringBuilder();int O=34;if(ï)L.AppendLine(À(Ĉ.ĉ,O));L.AppendLine(ą("ΔV HUD",O
,ï?'║':' '));if(ï)L.AppendLine(À(Ĉ.Ċ,O));L.AppendLine(ą(º,O,ï?'║':' '));if(ï)L.AppendLine(À(Ĉ.Č,O));è.WriteText(L.
ToString());}string À(Ĉ Á,int Z){switch(Á){case Ĉ.ĉ:return"╔"+new string('═',Z-2)+"╗";case Ĉ.Ċ:return"╠"+new string('═',Z-2)+"╣"
;case Ĉ.ċ:int Â=Z-3;int k=Â/2;int Ã=Â%2;return"╟"+new string('─',k)+"┬"+new string('─',k+Ã)+"╢";case Ĉ.Č:return"╚"+new
string('═',Z-2)+"╝";default:return new string(' ',Z);}}void Ä(StringBuilder L,int O){string Æ=
$"RCS: {(ñ?"ON":"OFF"),-3}  Burn: {(ò?"ON":"OFF"),-3}  Dir: {(ó?"ALL":"FWD"),-3}";L.AppendLine(ą(Æ,O,ï?'║':' '));}
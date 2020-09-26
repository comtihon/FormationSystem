using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class Settings
        {
            private readonly IMyProgrammableBlock me;
            private readonly MyIni ini = new MyIni();
            private const string section = "FormationSystem-Rover";
            private bool init = false;

            public IniValueString followerSystemId = new IniValueString(section, "followerSystemId", "System1", 
                "\n The id that the ship should listen to.");
            public IniValueString followerId = new IniValueString(section, "followerId", "Drone1",
                "\n The follower id of this ship." +
                "\n All remote commands not prefixed with this id will be ignored.");
            public IniValueString cockpitName = new IniValueString(section, "cockpitName", "Cockpit",
                "\n The name of the cockpit in the ship. If this cockpit is not" +
                "\n found, the script will attempt to find one on its own.");
            public IniValueBool useSubgridBlocks = new IniValueBool(section, "useSubgridBlocks", false, 
                "\n When true, the script will be able to see blocks that are " +
                "\n connected via rotor, connector, etc.");
            public IniValueBool autoStop = new IniValueBool(section, "autoStop", true, 
                "\n When true, the script will not run if there is a player" +
                "\n in the designated cockpit.");
            public IniValueBool autoStartHere = new IniValueBool(section, "autoStartHere", false, 
                "\n When true, upon leaving the cockpit, the script will set the offset to" +
                "\n the current position instead of returning to its designated point." +
                "\n This is equivalent to the starthere command.");
            public IniValueEnum<UpdateFrequency> tickSpeed = new IniValueEnum<UpdateFrequency>(section, "tickSpeed", UpdateFrequency.Update1, 
                "\n This is the frequency that the script is running at. If you are" +
                "\n experiencing lag because of this script, try decreasing this value." +
                "\n Update1 : Runs the script every tick" +
                "\n Update10 : Runs the script every 10th tick" +
                "\n Update100 : Runs the script every 100th tick");
            public IniValueBool calculateMissingTicks = new IniValueBool(section, "calculateMissingTicks", true,
                "\n When the tick speed of the leader is lower than the tick speed of" +
                "\n the follower, this workaround can be activated. When this is enabled," +
                "\n the script will \"guess\" what the leader position should be.");
            public IniValueInt maxMissingScriptTicks = new IniValueInt(section, "maxMissingScriptTicks", 100,
                "\n When calculateMissingTicks is enabled, the maximum number of ticks" +
                "\n to estimate before assuming the leader is no longer active." +
                "\n 1 game tick = 1/60 seconds");
            public IniValueBool spaceSeparators = new IniValueBool(section, "spaceSeparators", false,
                "\n Uses spaces as separators inside commands instead of semicolons." +
                "\n Example: setoffset 50 0 0 vs setoffset;50;0;0");
            public IniValueConfigs configs = new IniValueConfigs(section, "configs",
                "\n Configurations:" +
                "\n Use this to store a list of preset offsets. There must be at least one config." +
                "\n You can use commands to edit this or you can edit directly and" +
                "\n recompile. Each line stores an offset and a tag for that offset." +
                "\n These values have no direct effect on the offset." +
                "\n Format: \"|Name X Y Z\"" +
                "\n X: +Right -Left" +
                "\n Y: +Up -Down" +
                "\n Z: +Backward -Forward");

            public Settings (IMyProgrammableBlock me)
            {
                this.me = me;
            }

            public void Load ()
            {
                if (!string.IsNullOrWhiteSpace(me.CustomData))
                {
                    MyIniParseResult result;
                    if (!ini.TryParse(me.CustomData, out result))
                        throw new IniParseException(result.ToString());

                    if (ini.ContainsSection(section))
                    {
                        followerSystemId.Load(ini, me);
                        followerId.Load(ini, me);
                        cockpitName.Load(ini, me);
                        useSubgridBlocks.Load(ini, me);
                        autoStop.Load(ini, me);
                        autoStartHere.Load(ini, me);
                        tickSpeed.Load(ini, me);
                        calculateMissingTicks.Load(ini, me);
                        maxMissingScriptTicks.Load(ini, me);
                        spaceSeparators.Load(ini, me);
                        configs.Load(ini, me);
                        init = true;
                        return;
                    }
                }


                SaveAll();
                throw new Exception("\n\nSettings file has been generated in Custom Data.\nThis is NOT an error.\nRecompile the script once you ready.\n\n\n\n\n\n\n\n\n\n\n\n\n\n");
            }

            public void Save ()
            {
                if (!init)
                    return;
                MyIniParseResult result;
                if (!ini.TryParse(me.CustomData, out result))
                    throw new IniParseException(result.ToString());
                SaveAll();
            }

            private void SaveAll()
            {
                followerSystemId.Save(ini);
                followerId.Save(ini);
                cockpitName.Save(ini);
                useSubgridBlocks.Save(ini);
                autoStop.Save(ini);
                autoStartHere.Save(ini);
                tickSpeed.Save(ini);
                calculateMissingTicks.Save(ini);
                maxMissingScriptTicks.Save(ini);
                spaceSeparators.Save(ini);
                configs.Save(ini);

                ini.SetSectionComment(section,
                    " Formation System - Rover Script Version 1.2" +
                    "\n If you edit this configuration manually, you must recompile the" +
                    "\n script afterwards or you could lose the changes.");
                me.CustomData = ini.ToString();
            }

            public class IniValueConfigs : IniValue<Dictionary<string, Vector3D>>
            {
                public IniValueConfigs (string section, string key, string comment = null) : base(section, key, null, comment) 
                {
                    value = new Dictionary<string, Vector3D>();
                    value ["default-preset"] = new Vector3D(50, 0, 0);
                }

                protected override void Set (MyIni ini)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (KeyValuePair<string, Vector3D> kv in value)
                    {
                        sb.Append(kv.Key);
                        sb.Append(' ');
                        sb.Append(kv.Value.X);
                        sb.Append(' ');
                        sb.Append(kv.Value.Y);
                        sb.Append(' ');
                        sb.Append(kv.Value.Z);
                        sb.Append('\n');
                    }
                    if(value.Count > 1)
                        sb.Length--;
                    ini.Set(key, sb.ToString());
                }

                protected override bool TryGetValue (MyIniValue storage, out Dictionary<string, Vector3D> value)
                {
                    value = new Dictionary<string, Vector3D>();
                    List<string> lines = new List<string>();
                    storage.GetLines(lines);
                    foreach(string l in lines)
                    {
                        if (string.IsNullOrWhiteSpace(l))
                            continue;
                        string [] configValues = l.Split(new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (configValues.Length != 4)
                            return false;
                        Vector3D offset = new Vector3D();
                        if (!double.TryParse(configValues [1], out offset.X))
                            return false;
                        if (!double.TryParse(configValues [2], out offset.Y))
                            return false;
                        if (!double.TryParse(configValues [3], out offset.Z))
                            return false;
                        value[configValues [0]] = offset;
                    }
                    return value.Count > 0;
                }
            }
        }
    }
}

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
            private const string section = "FormationSystem-Leader";
            private bool init = false;

            public IniValueString followerSystemId = new IniValueString(section, "followerSystemId", "System1",
                "\n The id of the system that the followers are listening on.");
            public IniValueString cockpitName = new IniValueString(section, "cockpitName", "Cockpit",
                "\n The name of the cockpit in the ship. If this cockpit is not" +
                "\n found, the script will attempt to find one on its own.");
            public IniValueBool useSubgridBlocks = new IniValueBool(section, "useSubgridBlocks", false, 
                "\n When true, the script will be able to see blocks that are " +
                "\n connected via rotor, connector, etc.");
            public IniValueBool autoStop = new IniValueBool(section, "autoStop", true, 
                "\n When true, the script will not run if there is a player" +
                "\n in the designated cockpit.");
            public IniValueEnum<UpdateFrequency> tickSpeed = new IniValueEnum<UpdateFrequency>(section, "tickSpeed", UpdateFrequency.Update1, 
                "\n This is the frequency that the script is running at. If you are" +
                "\n experiencing lag because of this script, try decreasing this value." +
                "\n Update1 : Runs the script every tick" +
                "\n Update10 : Runs the script every 10th tick" +
                "\n Update100 : Runs the script every 100th tick" +
                "\n Note due to limitations," +
                "\n the script will only run every 10th tick max when not using itself.");
            public IniValueString sensorGroup = new IniValueString(section, "sensorGroup", "-", 
                "\n The name of the sensor group that the script" +
                "\n will use to keep track of the leader ship." +
                "\n Leave as \"-\" to use all connected sensors.");
            public IniValueDouble scanDistance = new IniValueDouble(section, "scanDistance", 1000,
                "\n The distance that the ship will scan while using the scan command.");
            public IniValueBool attemptReconnection = new IniValueBool(section, "attemptReconnection", true,
                "\n When true, the script will attempt to reconnect to the " +
                "\n previous leader on start. This functions in a similar way to" +
                "\n the find command if an exact match is not found.");
            public IniValueBool allowFollowSelf = new IniValueBool(section, "allowFollowSelf", true,
                "\n When true, the script will broadcast its own location" +
                "\n when no other leader is specified.");
            public IniValueBool activeRaycasting = new IniValueBool(section, "activeRaycasting", false,
                "\n When true, the script will use cameras to try to keep a lock" +
                "\n on the designated leader." + 
                "\n This is a CPU intensive process.");
            public IniValueBool alignFollowersToGravity = new IniValueBool(section, "alignFollowersToGravity", false,
                "\n When true, the script will align the followers with gravity." +
                "\n Recommended for rovers.");
            public IniValueBool spaceSeparators = new IniValueBool(section, "spaceSeparators", false,
                "\n Uses spaces as separators inside commands instead of semicolons." +
                "\n Example: :setoffset 50 0 0 vs :setoffset;50;0;0");


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
                        cockpitName.Load(ini, me);
                        useSubgridBlocks.Load(ini, me);
                        autoStop.Load(ini, me);
                        tickSpeed.Load(ini, me);
                        sensorGroup.Load(ini, me);
                        scanDistance.Load(ini, me);
                        attemptReconnection.Load(ini, me);
                        allowFollowSelf.Load(ini, me);
                        activeRaycasting.Load(ini, me);
                        alignFollowersToGravity.Load(ini, me);
                        spaceSeparators.Load(ini, me);
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
                cockpitName.Save(ini);
                useSubgridBlocks.Save(ini);
                autoStop.Save(ini);
                tickSpeed.Save(ini);
                sensorGroup.Save(ini);
                scanDistance.Save(ini);
                attemptReconnection.Save(ini);
                allowFollowSelf.Save(ini);
                activeRaycasting.Save(ini);
                alignFollowersToGravity.Save(ini);
                spaceSeparators.Save(ini);

                ini.SetSectionComment(section,
                    " Formation System - Leader Script Version 1.1" +
                    "\n If you edit this configuration manually, you must recompile the" +
                    "\n script afterwards or you could lose the changes.");
                me.CustomData = ini.ToString();
            }
        }
    }
}

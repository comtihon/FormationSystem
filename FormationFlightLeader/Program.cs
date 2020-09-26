using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;
using System;
using VRageMath;
using VRage;
using System.Text;
using System.Collections.Immutable;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // Leader Script Version 1.1
        // Compatible with follower/rover script version 1.2

        // ============= Settings ==============
        // Settings are located entirely in Custom Data.
        // To edit "Custom Data", check this block's menu and click the button above the "Edit" button. 
        // Run the script once to generate an initial config.
        // Recompile the script after making any changes.
        // =====================================

        // ============= Commands ==============
        // All commands can be used by running them on the programmable block.
        // Arguments with parentheses are optional.
        //
        // <id> : either blank (to send to all) or the followerId of the follower 
        // <id>:setoffset;x;y;z : sets the offset variable to this value on the follower 
        // <id>:addoffset;x;y;z : adds these values to the current offset on the follower 
        // <id>:stop : stops the followers and releases control to you
        // <id>:start : starts the followers after a stop command
        // <id>:starthere : starts the followers in the current position
        // <id>:clear : forces the script to forget a previous leader when calculateMissingTicks is true
        // <id>:action;block_name : triggers the specified timer block
        // <id>:reset : loads the current config, or the first offset
        // <id>:save(;name) : saves the configuration on the follower to the current offset
        // <id>:savehere(;name) : saves the configuration on the follower to the current position
        // <id>:load;name : loads the offset from the configuration on the follower 
        //
        // scan : scans forward using cameras to set which ship is the leader ship
        // find;name : scans the sensors using ship name to set which ship is the leader ship
        // stop : stops the script from broadcasting leader data
        // start : resumes broadcasting data after a stop command
        // reset : sets the leader ship to be the current ship (if allowFollowSelf is enabled)
        // =====================================

        // You can ignore any unreachable code warnings that appear in this script.

        Settings settings;

        IMyShipController rc;
        readonly string transmitTag = "FSLeader";
        readonly string transmitCommandTag = "FSCommand";

        List<IMySensorBlock> sensors = new List<IMySensorBlock>();
        List<IMyCameraBlock> forwardCameras = new List<IMyCameraBlock>();
        long target = 0;
        string targetName = null;
        readonly int echoFrequency = 100; // every 100th game tick
        int runtime = 0;
        bool isDisabled = false;
        const bool debug = false;
        readonly char commandSep;

        // Used for active raycasting.
        List<IMyCameraBlock> allCameras;
        MyDetectedEntityInfo previousHit = new MyDetectedEntityInfo();
        int hitRuntime = 0;

        public Program ()
        {
            settings = new Settings(Me);
            settings.Load();

            if (settings.spaceSeparators.Value)
                commandSep = ' ';
            else
                commandSep = ';';

            transmitTag += settings.followerSystemId.Value;
            transmitCommandTag += settings.followerSystemId.Value;

            bool useSubgrids = settings.useSubgridBlocks.Value;

            if (settings.sensorGroup.Value == "-")
                sensors = GetBlocks<IMySensorBlock>(useSubgrids);
            else
                sensors = GetBlocks<IMySensorBlock>(settings.sensorGroup.Value, useSubgrids);

            // Prioritize the given cockpit name	
            rc = GetBlock<IMyShipController>(settings.cockpitName.Value, useSubgrids);
            if (rc == null) // Second priority cockpit	
                rc = GetBlock<IMyCockpit>(useSubgrids);
            if (rc == null) // Third priority remote control	
                rc = GetBlock<IMyRemoteControl>(useSubgrids);
            if (rc == null) // No cockpits found.
                throw new Exception("No cockpit/remote control found. Set the cockpitName field in settings.");

            if (settings.activeRaycasting.Value)
                allCameras = new List<IMyCameraBlock>();
            foreach (IMyCameraBlock c in GetBlocks<IMyCameraBlock>(useSubgrids))
            {
                if (EqualsPrecision(Vector3D.Dot(rc.WorldMatrix.Forward, c.WorldMatrix.Forward), 1, 0.01))
                {
                    forwardCameras.Add(c);
                    c.EnableRaycast = true;
                }
                if(settings.activeRaycasting.Value)
                    allCameras.Add(c);
            }

            LoadStorage();

            if (settings.tickSpeed.Value == UpdateFrequency.Update10)
                echoFrequency = 10;
            else if (settings.tickSpeed.Value == UpdateFrequency.Update100)
                echoFrequency = 1;

            if (!isDisabled)
                Runtime.UpdateFrequency = settings.tickSpeed.Value;

            Echo("Ready.");
        }

        public void SaveStorage ()
        {
            char disabled = '0';
            if (isDisabled)
                disabled = '1';
            if (targetName == null)
                Storage = disabled.ToString();
            else
                Storage = $"{disabled};{target};{targetName}";
        }

        public void LoadStorage ()
        {
            if (string.IsNullOrWhiteSpace(Storage))
            {
                SaveStorage();
                return;
            }

            try
            {
                string [] args = Storage.Split(new [] { ';' }, 3);
                bool wasDisabled = args [0] == "1";
                if (args.Length == 1)
                {
                    this.target = 0;
                    this.targetName = null;
                    this.isDisabled = wasDisabled;
                    return;
                }

                long target = long.Parse(args [1]);
                string targetName = args [2];
                if (!settings.attemptReconnection.Value)
                {
                    this.target = target;
                    this.targetName = targetName;
                    this.isDisabled = wasDisabled;
                    return;
                }

                // Verify that the target exists
                bool done = false;
                MyDetectedEntityInfo? lastResult = null;
                foreach (IMySensorBlock s in sensors)
                {
                    if (done)
                        break;

                    List<MyDetectedEntityInfo> entites = new List<MyDetectedEntityInfo>();
                    s.DetectedEntities(entites);
                    foreach (MyDetectedEntityInfo i in entites)
                    {
                        if (target != 0 && i.EntityId == target)
                        {
                            // Found an exact match.
                            done = true;
                            lastResult = i;
                            break;
                        }

                        if (i.Name == targetName)
                        {
                            // Found a partial match
                            lastResult = i;
                        }
                    }

                    if (lastResult.HasValue)
                    {
                        // Target exists, update the info.
                        this.target = lastResult.Value.EntityId;
                        this.targetName = lastResult.Value.Name;
                        this.isDisabled = wasDisabled;
                        SaveStorage();
                    }
                    else
                    {
                        // Could not find target, use find mode.
                        this.target = 0;
                        this.targetName = targetName;
                        this.isDisabled = wasDisabled;
                        SaveStorage();
                    }
                }

            }
            catch
            {
                SaveStorage();
            }
        }

        public void Main (string argument, UpdateType updateSource)
        {
            if (updateSource == UpdateType.Update100 || updateSource == UpdateType.Update10 || updateSource == UpdateType.Update1)
            {
                if (debug || runtime % echoFrequency == 0)
                    WriteEcho();
                runtime++;

                if (target != 0 || targetName != null)
                {
                    MyDetectedEntityInfo? info = null;
                    foreach (IMySensorBlock s in sensors)
                    {
                        if (info.HasValue)
                            break;

                        List<MyDetectedEntityInfo> entites = new List<MyDetectedEntityInfo>();
                        s.DetectedEntities(entites);
                        foreach (MyDetectedEntityInfo i in entites)
                        {
                            if (target != 0)
                            {
                                // target id exists
                                if (i.EntityId == target)
                                {
                                    info = i;
                                    break;
                                }
                            }
                            else if(targetName != null)
                            {
                                // target id does not exist
                                if (i.Name == targetName)
                                {
                                    info = i;
                                    target = i.EntityId;
                                    SaveStorage(); // Update save data with the id
                                    break;
                                }
                            }
                        }

                    }

                    if (settings.activeRaycasting.Value && target != 0 && !previousHit.IsEmpty() && !info.HasValue)
                    {
                        float sec = Math.Abs(runtime - hitRuntime);
                        if (Runtime.UpdateFrequency == UpdateFrequency.Update10)
                            sec *= 1f / 6;
                        else if (Runtime.UpdateFrequency == UpdateFrequency.Update100)
                            sec *= 5f / 3;
                        else
                            sec *= 1f / 60;

                        Vector3D prediction = previousHit.Position + previousHit.Velocity * sec;
                        foreach (IMyCameraBlock c in allCameras)
                        {
                            if (!c.EnableRaycast)
                                c.EnableRaycast = true;
                            if (c.CanScan(prediction))
                            {
                                MyDetectedEntityInfo result = c.Raycast(prediction);
                                if (result.EntityId == target)
                                    info = result;
                                break;
                            }
                        }
                    }

                    if (info.HasValue)
                    {
                        if (settings.activeRaycasting.Value)
                        {
                            previousHit = info.Value;
                            hitRuntime = runtime;
                        }
                        Vector3D up = info.Value.Orientation.Up;
                        if (settings.alignFollowersToGravity.Value)
                        {
                            Vector3D gravity = rc.GetNaturalGravity();
                            if (gravity != Vector3D.Zero)
                                up = -Vector3D.Normalize(gravity);
                        }
                        MatrixD worldMatrix = MatrixD.CreateWorld(info.Value.Position, info.Value.Orientation.Forward, up);
                        IGC.SendBroadcastMessage<MyTuple<MatrixD, Vector3D, long>>(transmitTag, new MyTuple<MatrixD, Vector3D, long>(worldMatrix, info.Value.Velocity, info.Value.EntityId));
                    }
                }
                else if(settings.allowFollowSelf.Value)
                {
                    MatrixD matrix = rc.WorldMatrix;
                    if (settings.alignFollowersToGravity.Value)
                    {
                        Vector3D gravity = rc.GetNaturalGravity();
                        if (gravity != Vector3D.Zero)
                        {
                            Vector3D up = -Vector3D.Normalize(gravity);
                            Vector3D right = matrix.Forward.Cross(up);
                            Vector3D forward = up.Cross(right);
                            matrix.Up = up;
                            matrix.Right = right;
                            matrix.Forward = forward;
                        }
                    }
                    IGC.SendBroadcastMessage<MyTuple<MatrixD, Vector3D, long>>(transmitTag, new MyTuple<MatrixD, Vector3D, long>(matrix, rc.GetShipVelocities().LinearVelocity, Me.CubeGrid.EntityId));
                }
            }
            else
            {
                ProcessCommand(argument);
            }
        }

        void WriteEcho ()
        {
            StringBuilder sb = new StringBuilder();
            if (targetName != null)
            {
                sb.AppendLine("Running.");
                if (target == 0)
                    sb.Append("Searching for ").Append(targetName).Append("...").AppendLine();
                else
                    sb.Append("Following ").Append(targetName).AppendLine();
            }
            else if (settings.allowFollowSelf.Value)
            {
                sb.AppendLine("Running.");
                sb.AppendLine("Following me.");
            }
            else
            {
                sb.AppendLine("Idle.");
            }
            Echo(sb.ToString());
        }

        void ProcessCommand (string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
                return;
            
            // Split to find id
            string [] args = argument.Split(new [] { ':' }, 2);

            // Find the index of the command
            if (args.Length > 1)
            {
                TransmitCommand(args [0], args [1]);
            }
            else // Command does not have a follower target, try a command on this script before broadcasting.
            {
                SelfCommand(args [0]);
            }

        }

        void TransmitCommand (string target, string data, TransmissionDistance distance = TransmissionDistance.AntennaRelay)
        {
            string[] dataSplit = data.Split(new[] { commandSep }, StringSplitOptions.RemoveEmptyEntries);
            MyTuple<string, ImmutableArray<string>> msg = new MyTuple<string, ImmutableArray<string>>(target, dataSplit.ToImmutableArray());
            IGC.SendBroadcastMessage<MyTuple<string, ImmutableArray<string>>>(transmitCommandTag, msg, distance);
        }

        void SelfCommand (string argument)
        {
            string [] cmdArgs = argument.Split(new[] { commandSep }, StringSplitOptions.RemoveEmptyEntries);
            if (cmdArgs.Length < 1)
                return;
            switch (cmdArgs [0])
            {
                case "stop":
                    isDisabled = true;
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    break;
                case "start":
                    isDisabled = false;
                    Runtime.UpdateFrequency = settings.tickSpeed.Value;
                    break;
                case "reset":
                    isDisabled = false;
                    Runtime.UpdateFrequency = settings.tickSpeed.Value;

                    target = 0;
                    targetName = null;
                    WriteEcho();
                    break;
                case "scan":
                    isDisabled = false;
                    Runtime.UpdateFrequency = settings.tickSpeed.Value;

                    MyDetectedEntityInfo? info = Raycast();
                    if (info.HasValue)
                    {
                        target = info.Value.EntityId;
                        targetName = info.Value.Name;
                        if (settings.tickSpeed.Value == UpdateFrequency.Update1)
                            Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    }
                    WriteEcho();
                    break;
                case "find": // find;name
                    isDisabled = false;
                    Runtime.UpdateFrequency = settings.tickSpeed.Value;

                    target = 0;
                    targetName = cmdArgs [1];
                    if (settings.tickSpeed.Value == UpdateFrequency.Update1)
                        Runtime.UpdateFrequency = UpdateFrequency.Update10;
                    WriteEcho();
                    break;
                default:
                    TransmitCommand("", argument);
                    return;
            }
            SaveStorage();
        }

        MyDetectedEntityInfo? Raycast ()
        {
            foreach (IMyCameraBlock c in forwardCameras)
            {
                if (c.CanScan(settings.scanDistance.Value))
                {
                    MyDetectedEntityInfo info = c.Raycast(settings.scanDistance.Value);
                    if (info.IsEmpty())
                        return null;
                    return info;
                }
            }
            return null;
        }

        bool EqualsPrecision (double d1, double d2, double precision)
        {
            return Math.Abs(d1 - d2) <= precision;
        }


    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;

namespace ServerMessages
{
    public class SnapshotMessage : INetMessage
    {
        byte id = 1;
        byte numProcessed;
        uint snapshotSequenceNum;
        public List<SnapShotInfo> snapShotInfos;

        public SnapshotMessage(uint snapshotSequenceNum)
        {
            this.snapshotSequenceNum = snapshotSequenceNum;
            //this.numProcessed = numProcessed;
            snapShotInfos = new List<SnapShotInfo>();
        }

        public void AddInfo(SnapShotInfo ssi)
        {
            snapShotInfos.Add(ssi);
        }

        void INetMessage.WriteMessage(ref DataStreamWriter writer)
        {
            writer.WriteByte(id);
            writer.WriteUInt(snapshotSequenceNum);
            for (int i = 0; i < snapShotInfos.Count; i++)
            {
                writer.WriteShort(snapShotInfos[i].playerId);
                writer.WriteFloat(snapShotInfos[i].position.x);
                writer.WriteFloat(snapShotInfos[i].position.y);
                writer.WriteFloat(snapShotInfos[i].position.z);
                writer.WriteFloat(snapShotInfos[i].rotation.x);
                writer.WriteFloat(snapShotInfos[i].rotation.y);
                writer.WriteFloat(snapShotInfos[i].rotation.z);
                writer.WriteFloat(snapShotInfos[i].rotation.w);
            }
        }

        public static uint GetLastSequence(ref DataStreamReader reader)
        {
            int readBytes = reader.GetBytesRead();
            reader.SeekSet(reader.Length - 4);
            uint lastSeq = reader.ReadUInt();
            reader.SeekSet(readBytes);
            return lastSeq;
        }
    }

    public class FullStateMessage : INetMessage
    {
        byte id = 2;

        public List<SnapShotInfo> snapShotInfos;

        public FullStateMessage()
        {
            snapShotInfos = new List<SnapShotInfo>();
        }

        public void AddInfo(SnapShotInfo ssi)
        {
            snapShotInfos.Add(ssi);
        }

        void INetMessage.WriteMessage(ref DataStreamWriter writer)
        {
            writer.WriteByte(id);
            for (int i = 0; i < snapShotInfos.Count; i++)
            {
                writer.WriteShort(snapShotInfos[i].playerId);
                writer.WriteFloat(snapShotInfos[i].position.x);
                writer.WriteFloat(snapShotInfos[i].position.y);
                writer.WriteFloat(snapShotInfos[i].position.z);
                writer.WriteFloat(snapShotInfos[i].rotation.x);
                writer.WriteFloat(snapShotInfos[i].rotation.y);
                writer.WriteFloat(snapShotInfos[i].rotation.z);
                writer.WriteFloat(snapShotInfos[i].rotation.w);
            }
        }
    }
    //Used to send the player ID and server tickrate to a newly connected client
    public class OnConnectMessage : INetMessage
    {
        byte id = 10;
        int playerid = -2;
        float serverTickrate = 0;

        public OnConnectMessage(int playerid, float serverTickrate)
        {
            this.playerid = playerid;
            this.serverTickrate = serverTickrate;
        }
        public void WriteMessage(ref DataStreamWriter writer)
        {
            writer.WriteByte(id);
            writer.WriteInt(playerid);
            writer.WriteFloat(serverTickrate);
        }

        public void WriteMessage(DataStreamWriter writer)
        {
            writer.WriteByte(id);
            writer.WriteInt(playerid);
            writer.WriteFloat(serverTickrate);
        }
    }

    public class FullPlayerListMessage : INetMessage
    {
        byte id = 11;
        List<int> ids;
        List<string> names;

        public FullPlayerListMessage(int destinationId, NativeList<NetworkConnection> connections)
        {
            ids = new List<int>();
            for (int i = 0; i < connections.Length; i++)
            {
                if (connections[i].InternalId != destinationId)
                {
                    ids.Add(connections[i].InternalId);
                }
            }
        }
        public void WriteMessage(ref DataStreamWriter writer)
        {
            writer.WriteByte(id);
            for (int i = 0; i < ids.Count; i++)
            {
                writer.WriteInt(ids[i]);
            }
        }
    }
    public class SpawnRequestReplyMessage : INetMessage
    {
        byte id = 12;
        byte response = 0; //0 to refuse spawn, 1 to accept
        Vector3 spawnPos;
        Quaternion spawnRot;

        public SpawnRequestReplyMessage(byte response, Vector3 spawnPos, Quaternion spawnRot)
        {
            this.response = response;
            this.spawnPos = spawnPos;
            this.spawnRot = spawnRot;
        }
        public void WriteMessage(ref DataStreamWriter writer)
        {
            writer.WriteByte(id);
            writer.WriteByte(response);
            writer.WriteFloat(spawnPos.x);
            writer.WriteFloat(spawnPos.y);
            writer.WriteFloat(spawnPos.z);
            writer.WriteFloat(spawnRot.x);
            writer.WriteFloat(spawnRot.y);
            writer.WriteFloat(spawnRot.z);
            writer.WriteFloat(spawnRot.w);
        }
    }

    public class SpawnOtherInformMessage : INetMessage //add transform info to this
    {
        byte id = 13;
        int playerID = -1;
        Vector3 spawnPos;
        Quaternion spawnRot;

        public SpawnOtherInformMessage(int playerID, Vector3 spawnPos, Quaternion spawnRot)
        {
            this.playerID = playerID;
            this.spawnPos = spawnPos;
            this.spawnRot = spawnRot;
        }
        public void WriteMessage(ref DataStreamWriter writer)
        {
            writer.WriteByte(id);
            writer.WriteInt(playerID);
            writer.WriteFloat(spawnPos.x);
            writer.WriteFloat(spawnPos.y);
            writer.WriteFloat(spawnPos.z);
            writer.WriteFloat(spawnRot.x);
            writer.WriteFloat(spawnRot.y);
            writer.WriteFloat(spawnRot.z);
            writer.WriteFloat(spawnRot.w);
        }
    }

    public class ClientConnectedMessage : INetMessage
    {
        byte id = 14;
        int playerID = -1;
        string name;

        public ClientConnectedMessage(int playerID)
        {
            this.playerID = playerID;
        }
        public void WriteMessage(ref DataStreamWriter writer)
        {
            writer.WriteByte(id);
            writer.WriteInt(playerID);
        }
    }

    public class ClientDisconnectedMessage : INetMessage
    {
        byte msgID = 15;
        int playerID = -1;

        public ClientDisconnectedMessage(int playerID)
        {
            this.playerID = playerID;
        }
        public void WriteMessage(ref DataStreamWriter writer)
        {
            writer.WriteByte(msgID);
            writer.WriteInt(playerID);
        }
    }

    public class TickrateUpdateMessage : INetMessage
    {
        byte id = 16;
        float tickrate;

        public TickrateUpdateMessage(float tickrate)
        {
            this.tickrate = tickrate;
        }

        public void WriteMessage(ref DataStreamWriter writer)
        {
            writer.WriteByte(id);
            writer.WriteFloat(tickrate);
        }
    }

    public struct SnapShotInfo
    {
        public const int maxSize = 30; //max size in bytes of this message with a short player ID, a vector 3 position and a quaternion rotation.  One short (2) 7 floats (4)
        public short playerId;
        public Vector3 position;
        public Quaternion rotation;

        public SnapShotInfo(short playerId, Vector3 position, Quaternion rotation)
        {
            this.playerId = playerId;
            this.position = position;
            this.rotation = rotation;
        }
    }
}

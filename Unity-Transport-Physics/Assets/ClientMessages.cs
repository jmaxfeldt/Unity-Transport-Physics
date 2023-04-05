using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Networking.Transport;

namespace ClientMessages
{
    public class InputMessage : INetMessage
    {
        public const byte maxLength = 9; //doesn't include ID byte MUST CHANGE THIS IF MORE IS SENT
        byte id = 4;
        public uint sequenceNum = 0;
        public float deltaTime;
        public byte moveKeysBitmask = 0;
        public Vector3 predictedPos;
        public Quaternion predictedRot;

        public InputMessage(uint sequenceNum, float deltaTime, byte moveKeysBitmask)
        {
            this.sequenceNum = sequenceNum;
            this.deltaTime = deltaTime;
            this.moveKeysBitmask = moveKeysBitmask;
        }

        public void WriteMessage(ref DataStreamWriter writer)
        {
            writer.WriteByte(id);
            writer.WriteUInt(sequenceNum);
            writer.WriteFloat(deltaTime);
            writer.WriteByte(moveKeysBitmask);
        }
    }

    public class MultiInputMessage : INetMessage
    {
        byte id = 1;
        InputMessage[] messages;

        public MultiInputMessage() { }
        public MultiInputMessage(List<InputMessage> inputs)
        {
            messages = new InputMessage[inputs.Count];

            for (int i = 0; i < inputs.Count; i++)
            {
                messages[i] = inputs[i];
            }
        }

        public void WriteMessage(ref DataStreamWriter writer)
        {
            writer.WriteByte(id);
            for (int i = 0; i < messages.Length; i++)
            {
                writer.WriteUInt(messages[i].sequenceNum);
                writer.WriteFloat(messages[i].deltaTime);
                writer.WriteByte(messages[i].moveKeysBitmask);
            }
        }
        public static InputMessage[] ReadMessage(ref DataStreamReader reader)
        {
            InputMessage[] messages = new InputMessage[reader.Length / InputMessage.maxLength];
            for (int i = 0; i < messages.Length; i++)
            {
                messages[i] = new InputMessage(
                reader.ReadUInt(),
                reader.ReadFloat(),
                reader.ReadByte()
                );
            }
            return messages;
        }

        public static uint GetLastSequence(ref DataStreamReader reader)
        {
            int readBytes = reader.GetBytesRead();
            reader.SeekSet(reader.Length - InputMessage.maxLength);
            uint lastSeq = reader.ReadUInt();
            reader.SeekSet(readBytes);
            return lastSeq;
        }
    }

    public class PingMessage : INetMessage
    {
        byte id = 20;
        float sendTime;

        public PingMessage(float sendTime)
        {
            this.sendTime = sendTime;
        }

        public void WriteMessage(ref DataStreamWriter writer)
        {
            writer.WriteByte(id);
            writer.WriteFloat(sendTime);
        }
    }

    public class RequestSpawnMessage : INetMessage
    {
        byte id = 3;
        public void WriteMessage(ref DataStreamWriter writer)
        {
            writer.WriteByte(id);
        }
    }

    public class DisconnectNotificationMessage : INetMessage
    {
        byte id = 255;

        public void WriteMessage(ref DataStreamWriter writer)
        {
            writer.WriteByte(id);
        }
    }
}


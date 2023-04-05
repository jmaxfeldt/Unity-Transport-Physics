using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Networking.Transport;

public static class ReadExtensions
{
    public static Vector3 ReadVector3(ref DataStreamReader reader)
    {
        return new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
    }

    public static Quaternion ReadQuaternion(ref DataStreamReader reader)
    {
        return new Quaternion(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
    }
}

public static class WriteExtensions
{
    public static void WriteVector3(Vector3 v, ref DataStreamWriter writer)
    {
        writer.WriteFloat(v.x);
        writer.WriteFloat(v.y);
        writer.WriteFloat(v.z);
    }

    public static void WriteQuaternion(Quaternion q, ref DataStreamWriter writer)
    {
        writer.WriteFloat(q.x);
        writer.WriteFloat(q.y);
        writer.WriteFloat(q.z);
        writer.WriteFloat(q.w);
    }
}

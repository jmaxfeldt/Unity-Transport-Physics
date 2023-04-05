using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Networking.Transport;

public interface INetMessage
{
    public void WriteMessage(ref DataStreamWriter writer);
}

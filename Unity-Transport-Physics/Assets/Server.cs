using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using NetMessages;

public class Server : MonoBehaviour
{
    public NetworkDriver netDriver;
    public NativeList<NetworkConnection> connections;

    private Dictionary<int, ServerPlayer> playersDict;
    //The indices of this list should always match the player id which is set using the connection's internal id.  Might be too convoluted/not worth the higher speed?
    private List<ServerPlayer> players;

    public ServerMultiMovePhysScene physScene;

    public NetworkPipeline reliableSeqSimPipeline;
    public NetworkPipeline unreliableSimPipeline;

    public GameObject playerPrefab;

    float tickRate = 20;
    float nextTick = 0;
    int maxPlayers = 20;
    uint snapshotSequence = 0;

    public bool isStarted;

    public float TickRate
    {
        get => tickRate;
        set
        {
            tickRate = value;
            SendToAll(reliableSeqSimPipeline, new TickrateUpdateMessage(TickRate));
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        playerPrefab = Resources.Load("Player") as GameObject;
        players = new List<ServerPlayer>(maxPlayers);
        InitPlayersList();
        nextTick = Time.time;
        physScene = GameObject.FindFirstObjectByType<ServerMultiMovePhysScene>();
        Debug.Log("Did the whole START thing on server behaviour");
    }

    public void ConfigServer()
    {
        NetworkSettings netSettings = new NetworkSettings();
        netSettings.WithSimulatorStageParameters(3000, 256, 50, 0, 0, 0);//, 0, 0, 0);
        netDriver = NetworkDriver.Create(netSettings);

        reliableSeqSimPipeline = netDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage), typeof(SimulatorPipelineStage));
        unreliableSimPipeline = netDriver.CreatePipeline(typeof(SimulatorPipelineStage));

        connections = new NativeList<NetworkConnection>(maxPlayers, Allocator.Persistent);
    }

    public bool StartServer()
    {
        ConfigServer();
        NetworkEndPoint endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = 9000;

        if (netDriver.Bind(endpoint) != 0)
        {
            Debug.Log("Failed to bind to port 9000");
        }
        else
        {
            netDriver.Listen();
            isStarted = true;
            return true;
        }
        return false;
    }

    public bool ShutDown()
    {
        OnDestroy();
        if (!netDriver.IsCreated)
        {
            isStarted = false;
            return true;
        }
        return false;
    }

    private void InitPlayersList()
    {
        for (int i = 0; i < players.Capacity; i++)
        {
            players.Add(new ServerPlayer());
        }
    }

    private void OnDestroy()
    {
        if (netDriver.IsCreated)
        {
            netDriver.Dispose();
            connections.Dispose();
            Destroy(this);
        }
    }

    public void SendToClient(NetworkPipeline pipeline, NetworkConnection connection, INetMessage message)//consider adding a pipeline parameter to be able to do reliable and unreliable in one method
    {
        if (message != null)
        {
            netDriver.BeginSend(pipeline, connection, out DataStreamWriter writer);
            message.WriteMessage(ref writer);
            netDriver.EndSend(writer);
        }
    }

    public void SendToAll(NetworkPipeline pipeline, INetMessage message)
    {
        //DataStreamWriter writer = new DataStreamWriter(); //will this work? Writing message once to send multiple times instead of writing message on ever send;
        for (int i = 0; i < connections.Length; i++)
        {
            netDriver.BeginSend(pipeline, connections[i], out DataStreamWriter writer);
            message.WriteMessage(ref writer);
            netDriver.EndSend(writer);
        }
    }

    public void SendToAllExcept(NetworkPipeline pipeline, int exception, INetMessage message)
    {
        //DataStreamWriter writer = new DataStreamWriter(); //will this work? Writing message once to send multiple times instead of writing message on ever send;
        for (int i = 0; i < connections.Length; i++)
        {
            if (connections[i].InternalId != exception)
            {
                netDriver.BeginSend(pipeline, connections[i], out DataStreamWriter writer);
                message.WriteMessage(ref writer);
                netDriver.EndSend(writer);
            }
        }
    }

    public void SendSnapshotToAll(NetworkPipeline pipeline, INetMessage message)//BAD BAD BAD.  Appending the last sequence to the end is stupid.  Find another way to do this
    {
        //Debug.LogWarning("Number of connections: " + connections.Length);
        if (message != null)
        {
            for (int i = 0; i < connections.Length; i++)
            {
                netDriver.BeginSend(pipeline, connections[i], out DataStreamWriter writer);
                message.WriteMessage(ref writer);
                writer.WriteUInt(players[connections[i].InternalId].lastProcessedInput); // <-- DON'T LEAVE THIS THAT WAY
                netDriver.EndSend(writer);
            }
        }
        //else
        //{
        //    Debug.LogWarning("The snapshot message is null");
        //}
    }

    bool AddPlayer(NetworkConnection connection)
    {
        if (!connections.Contains(connection) && !players[connection.InternalId].inUse)
        {
            connections.Add(connection);
            players[connection.InternalId] = new ServerPlayer(connection, this);
            return true;
        }
        return false;
    }

    bool RemovePlayer(NetworkConnection connection)
    {
        if (connections.Contains(connection))
        {
            //connections.RemoveAtSwapBack(connections.IndexOf(connection));

            if (players[connection.InternalId].inUse)
            {
                players[connection.InternalId].Despawn();
                players[connection.InternalId] = new ServerPlayer();
            }
            //connection = default(NetworkConnection);
            return true;
        }
        Debug.LogError("Could not remove player " + connection.InternalId + ". It's connection was not found in the connections list.");
        return false;
    }

    private void FixedUpdate()
    {
        if (connections.Length > 0)
        {
            for (int i = 0; i < connections.Length; i++)
            {
                if (players[connections[i].InternalId].isSpawned)//temporary. do something in server player to check this probably
                {
                    players[connections[i].InternalId].ProcessInputs();
                }
                //Debug.LogError("Inputs processed this tick: " + players[connections[i].InternalId].processedSinceLast);
            }
            snapshotSequence++;
            SnapshotCreateAndSend();           
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (isStarted)
        {
            netDriver.ScheduleUpdate().Complete();

            //Clean up connections
            for (int i = 0; i < connections.Length; i++)
            {
                if (!connections[i].IsCreated)
                {
                    connections.RemoveAtSwapBack(i);
                    Debug.LogError("Cleaning up a connection...");
                    //players.RemoveAt(i);
                    --i;
                }
            }

            //Accept new connections
            NetworkConnection netConnection;
            while ((netConnection = netDriver.Accept()) != default(NetworkConnection))
            {
                //connections.Add(netConnection);
                if (AddPlayer(netConnection))
                {
                    Debug.Log("Accepted a connection.  Internal ID: " + netConnection.InternalId);
                    SendToClient(reliableSeqSimPipeline, netConnection, new OnConnectMessage(netConnection.InternalId, TickRate));
                    SendToClient(reliableSeqSimPipeline, netConnection, new FullPlayerListMessage(netConnection.InternalId, connections));
                    SendToClient(reliableSeqSimPipeline, netConnection, CreateFullStateMessage());
                    SendToAllExcept(reliableSeqSimPipeline, netConnection.InternalId, new ClientConnectedMessage(netConnection.InternalId));
                    Debug.Log(netDriver.RemoteEndPoint(netConnection).Address);//this is how you get IP and port information
                }
            }

            DataStreamReader stream;
            for (int i = 0; i < connections.Length; i++)
            {
                if (!connections[i].IsCreated)
                {
                    continue;
                }

                NetworkEvent.Type cmd;
                while ((cmd = netDriver.PopEventForConnection(connections[i], out stream)) != NetworkEvent.Type.Empty)
                {
                    if (cmd == NetworkEvent.Type.Data)
                    {
                        byte messageType = stream.ReadByte();
                        //Debug.Log("SERVER: Got a message of type " + messageType + " from the client.");
                        HandleMessage(connections[i], messageType, ref stream);
                    }

                    else if (cmd == NetworkEvent.Type.Disconnect)
                    {
                        Debug.Log("Client disconnected from server");
                        OnDisconnect(connections[i]);
                    }
                }
            }

            if (Time.time >= nextTick)
            {
                nextTick = Time.time + (1 / tickRate);
                //if (connections.Length > 0)
                //{                  
                //    snapshotSequence++;
                //    SendSnapshotToAll(unreliableSimPipeline, CreateSnapshot());
                //}
            }
        }
    }

    void SnapshotCreateAndSend()
    {        
        for (int i = 0; i < connections.Length; i++)
        {
            int connId = connections[i].InternalId;
            SnapshotMessage ssMsg = new SnapshotMessage(snapshotSequence);
            ssMsg.playerState = players[connId].GetStateInfo(); 

            for (int k = 0; k < connections.Length; k++)
            {
                if (players[connections[k].InternalId].isSpawned && players[connections[k].InternalId].id != connId)// && players[connId].hasNewSnapshotData)//temporary. do something in server player to check this probably
                {
                    ssMsg.AddInfo(players[connections[k].InternalId].GetSnapshotInfo());
                }
            }
            SendToClient(unreliableSimPipeline, connections[i], ssMsg);
        }       
    }

    FullStateMessage CreateFullStateMessage()
    {
        FullStateMessage ssMsg = new FullStateMessage();
        for (int i = 0; i < connections.Length; i++)
        {
            int connId = connections[i].InternalId;
            if (players[connId].isSpawned)//temporary. do something in server player to check this probably
            {
                ssMsg.AddInfo(players[connId].GetSnapshotInfo());
            }
        }
        if (ssMsg.snapShotInfos.Count > 0)
        {
            //Debug.LogWarning("Snapshot message has data from " + ssMsg.snapShotInfos.Count + " Player");
            return ssMsg;
        }
        return null;
    }

    void OnConnect()
    {

    }

    private void OnDisconnect(NetworkConnection connection)
    {
        RemovePlayer(connection);
        Debug.LogWarning("Connection ID before disconnect: " + connection.InternalId);
        netDriver.Disconnect(connection);
        Debug.LogWarning("Connection ID after disconnect: " + connection.InternalId);
        SendToAllExcept(reliableSeqSimPipeline, connection.InternalId, new ClientDisconnectedMessage(connection.InternalId));
        //connection = default(NetworkConnection);
        Debug.LogWarning("Connections count before removeatswapback");
        connections.RemoveAtSwapBack(connections.IndexOf(connection));
        Debug.LogWarning("Connections count after removeatswapback");
        Debug.LogError("Connection is created: " + connection.IsCreated);
        //connections[id] = default(NetworkConnection); //sets connection to default.  Gets cleaned up by the clean up connections code on the next loop
    }

    void HandleMessage(NetworkConnection connection, byte type, ref DataStreamReader reader)
    {
        if (type == 1)//Multi Input.  This would come from a client sending all unacknowledged inputs and is probably the most commonly received message
        {
            uint lastSeq = reader.ReadUInt();//MultiInputMessage.GetLastSequence(ref reader);
            
            //Debug.LogError("Latest inputs last seq: " + lastSeq);
            if (lastSeq > players[connection.InternalId].lastProcessedInput && lastSeq > players[connection.InternalId].latestInputs.lastSequence)
            {
                players[connection.InternalId].latestInputs = MultiInputMessage.ReadMessage(ref reader);
                //Debug.Log("Latest inputs last seq: " + latestInputs.lastSeq);
            }       
        }
        else if (type == 2)
        {

        }
        else if (type == 3)//spawn request
        {
            if (!players[connection.InternalId].isSpawned)
            {
                if (players[connection.InternalId].Spawn(playerPrefab, new Vector3(0,2,0), Quaternion.identity))
                {
                    physScene.SpawnCharacterRep(playerPrefab, new Vector3(0, 2, 0), Quaternion.identity);
                    SendToClient(reliableSeqSimPipeline, connection, new SpawnRequestReplyMessage(1, new Vector3(0, 2, 0), Quaternion.identity));
                    SendToAllExcept(reliableSeqSimPipeline, connection.InternalId, new SpawnOtherInformMessage(connection.InternalId, new Vector3(0, 2, 0), Quaternion.identity));
                }
                else
                {
                    SendToClient(reliableSeqSimPipeline, connection, new SpawnRequestReplyMessage(0, Vector3.zero, Quaternion.identity));
                }
            }
        }
        else if (type == 4)//Input from client
        {
            uint seqNum = reader.ReadUInt();   
            float delta = reader.ReadFloat();
            players[connection.InternalId].playerMove.Move(reader.ReadByte());
        }
        else if (type == 10)//
        { 

            
        }
        else if (type == 20) //ping request
        {
            SendToClient(unreliableSimPipeline, connection, new PingMessage(reader.ReadFloat()));
        }
        else if (type == 255)
        {
            Debug.LogWarning("Client " + connection.InternalId + " has sent a disconnect message");
            //SendToClientReliable(connection, new DisconnectNotificationMessage());//This doesn't get sent because(I think) the connection gets set to default before the message is sent
            Debug.LogWarning("Got beyond the send reply to disconnect stage");
            OnDisconnect(connection);
        }
        else
        {
            Debug.LogWarning("Message type unrecognized");
        }
    }
}

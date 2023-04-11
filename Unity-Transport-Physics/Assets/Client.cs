using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using NetMessages;
using TMPro;

public class Client : MonoBehaviour
{
    int thisClientID;
    int maxPlayers = 20; //maybe have the server assign this on connect
    float serverTickrate = 30;
    bool useInterpolation = true;

    NetworkDriver netDriver;
    NetworkConnection connection;

    public NetworkPipeline reliableSeqSimPipeline;
    public NetworkPipeline unreliableSimPipeline;

    public ClientPlayer localPlayer; //Quick reference to the local player
    public GameObject playerPrefab; //Gets loaded from the resources folder on Start
    public List<ClientPlayer> players;

    public ReconciliationPhysScene physScene;

    public TMP_Text pingText;
    float pingDelay = 1.0f;
    float lastPing;

    public bool done;
    public float lastTime;
    uint lastSnapshotSequence = 0;

    // Start is called before the first frame update
    void Start()
    {
        playerPrefab = Resources.Load("Player") as GameObject;
        //pingText = GameObject.Find("PingText").GetComponent<TMP_Text>();
        physScene = GameObject.FindFirstObjectByType<ReconciliationPhysScene>(); //for reconciliation physics scene
        InitPlayersList();
        lastTime = Time.time;
        lastPing = Time.time;
    }

    private void InitPlayersList()
    {
        players = new List<ClientPlayer>(maxPlayers);
        for (int i = 0; i < players.Capacity; i++)
        {
            players.Add(new ClientPlayer());
        }
    }

    public bool ConnectToServer()
    {
        ConfigClient();
        NetworkEndPoint endpoint = NetworkEndPoint.LoopbackIpv4;
        endpoint.Port = 9000;
        connection = netDriver.Connect(endpoint);
        return true;
    }

    void ConfigClient()
    {
        NetworkSettings netSettings = new NetworkSettings();
        netSettings.WithSimulatorStageParameters(3000, 256, 50, 0, 0, 0);//, 0, 0, 0);
        netDriver = NetworkDriver.Create(netSettings);

        reliableSeqSimPipeline = netDriver.CreatePipeline(typeof(ReliableSequencedPipelineStage), typeof(SimulatorPipelineStage));
        unreliableSimPipeline = netDriver.CreatePipeline(typeof(SimulatorPipelineStage));

        connection = default(NetworkConnection);
    }

    public bool AddPlayer(int id, bool islocal)
    {
        if (!players[id].inUse)
        {
            players[id] = new ClientPlayer(id, islocal);

            if (islocal)
            {
                localPlayer = players[id];
            }
            Debug.LogWarning("Player Added " + id + " - isLocal: " + islocal);
            return true;
        }
        Debug.LogError("Could not add new player.  The ID is already in use.");
        return false;
    }

    public void RemovePlayer(int id)
    {
        players[id].Despawn();
        players[id] = new ClientPlayer();
    }

    public void RemoveAllPlayers()
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].inUse)
            {
                RemovePlayer(i);
            }
        }
    }

    public void OnConnect(int ID, float tickrate)
    {
        AddPlayer(ID, true);
        serverTickrate = tickrate;
        Debug.LogWarning("Received id and tickrate from server.  ID: " + thisClientID + "  - Tickrate: " + serverTickrate);
        Debug.Log("Requesting spawn....");
        SendToServer(reliableSeqSimPipeline, new RequestSpawnMessage());//This sends a request to the server to spawn the local player character.  it's only here for testing purposes  
    }

    public void Disconnect()
    {
        SendToServer(reliableSeqSimPipeline, new DisconnectNotificationMessage());
    }

    public void OnDisconnect()
    {
        Debug.LogError("OnDisconnect called");
        RemoveAllPlayers();
        netDriver.Disconnect(connection);
        netDriver.Dispose();
        //Destroy(playerControl);
        Destroy(this);
    }

    private void OnDestroy()
    {
        netDriver.Dispose();
    }

    public void SendToServer(NetworkPipeline pipeline, INetMessage message)
    {
        netDriver.BeginSend(pipeline, connection, out DataStreamWriter writer);
        message.WriteMessage(ref writer);
        netDriver.EndSend(writer);
    }

    // Update is called once per frame
    void Update()
    {
        if (netDriver.IsCreated)
        {
            netDriver.ScheduleUpdate().Complete();

            if (!connection.IsCreated)
            {
                if (!done)
                {
                    Debug.Log("Something went wrong during connect.");
                }
                return;
            }

            DataStreamReader stream;
            NetworkEvent.Type cmd;

            while ((cmd = connection.PopEvent(netDriver, out stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Connect)
                {
                    Debug.Log("We are now connected to the server.");
                }

                else if (cmd == NetworkEvent.Type.Data)
                {
                    byte type = stream.ReadByte();
                    HandleMessage(type, ref stream);
                }

                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client got disconnected from server");
                    OnDisconnect();
                    connection = default(NetworkConnection);
                }
            }

            if (Time.time - lastPing >= pingDelay)
            {
                lastPing = Time.time;
                SendToServer(unreliableSimPipeline, new PingMessage(Time.time));
            }

            if (useInterpolation)
            {
                for (int i = 0; i < players.Count; i++)//don't leave this like this.  It will go through the whole player list each time
                {
                    if (players[i].inUse && !players[i].isLocal)
                    {
                        players[i].InterpolateMove(serverTickrate);
                    }
                }
            }
        }
    }

    void HandleMessage(byte type, ref DataStreamReader reader)
    {
        if(type == 1)
        {
            uint snapshotSeqNum = reader.ReadUInt();//This is for testing.  Not sure if I should send this to the client at all.  Can be used to detect out of order snapshots that could mess with interpolation
            //Debug.Log("Snapshot received.  Length: " + reader.Length );

            if (snapshotSeqNum > lastSnapshotSequence)
            {
                if (snapshotSeqNum != (lastSnapshotSequence + 1))
                {
                    Debug.LogError("LOST SNAPSHOT(S): " + (snapshotSeqNum - (lastSnapshotSequence + 1)));
                }
                lastSnapshotSequence = snapshotSeqNum;

                //StateInfo sInfo = new StateInfo(reader.ReadUInt(), ReadExtensions.ReadVector3(ref reader), ReadExtensions.ReadQuaternion(ref reader), ReadExtensions.ReadVector3(ref reader), ReadExtensions.ReadVector3(ref reader));
                localPlayer.playerControl.HandleSnapshot(reader.ReadUInt(), ReadExtensions.ReadVector3(ref reader), ReadExtensions.ReadQuaternion(ref reader), ReadExtensions.ReadVector3(ref reader), ReadExtensions.ReadVector3(ref reader));

                while (reader.GetBytesRead() < reader.Length)
                {
                    short playerId = reader.ReadShort();
                    //byte numProcessed = reader.ReadByte();
                    //Debug.LogError("new snapshot info for player ID: " + playerId + " - snapshot sequence: " + snapshotSeqNum);
                    Vector3 newPos = ReadExtensions.ReadVector3(ref reader); //new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
                    Quaternion newRot = ReadExtensions.ReadQuaternion(ref reader);//new Quaternion(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());                   
                  
                    if (useInterpolation)
                    {
                        players[playerId].AddToInterpBuffer(Time.time, newPos, newRot);
                        //players[playerId].interpPool.AddItem(Time.time, newPos, newRot);
                    }
                    else
                    {
                        players[playerId].DummyMove(newPos, newRot);
                    }             
                }
            }
            else
            {
                Debug.LogError("Snapshot arrived out of order.  Ignoring?....");
            }
        }

        else if(type == 2)
        {
            //Debug.LogError("RECEIVED FULL STATE.  SIZE: " + reader.Length);
            while (reader.GetBytesRead() < reader.Length)
            {
                short id = reader.ReadShort();
                
                if (players[id].inUse)
                {      
                    players[id].Spawn(playerPrefab, ReadExtensions.ReadVector3(ref reader), ReadExtensions.ReadQuaternion(ref reader));
                }
                else
                {
                    Debug.LogError("Received state info for a player that does not yet exist on this client");
                    reader.SeekSet(reader.GetBytesRead() + ServerMessages.SnapShotInfo.maxSize);
                }
            }
        }
        else if(type == 3) //stateinfo
        {
            //Debug.LogError("Received state info from the server");
            StateInfo sInfo = new StateInfo(reader.ReadUInt(), ReadExtensions.ReadVector3(ref reader), ReadExtensions.ReadQuaternion(ref reader), ReadExtensions.ReadVector3(ref reader), ReadExtensions.ReadVector3(ref reader));
            localPlayer.playerControl.HandleSnapshot(sInfo.sequence, sInfo.position, sInfo.rotation, sInfo.linearVelocity, sInfo.angularVelocity);
        }

        else if (type == 10)//on connection id receive from server
        {
            OnConnect(reader.ReadInt(), reader.ReadFloat());
        }
        else if (type == 11) //Full player list
        {
            Debug.LogError("Received player list.  Size: " + reader.Length);
            while (reader.GetBytesRead() < reader.Length)
            {
                AddPlayer(reader.ReadInt(), false);
            }              
        }
        else if (type == 12)//spawn request reply
        {
            if (reader.ReadByte() == 1)
            {
                if (playerPrefab != null)
                {                    
                    if(localPlayer.Spawn(playerPrefab, ReadExtensions.ReadVector3(ref reader), ReadExtensions.ReadQuaternion(ref reader)))
                    {
                        localPlayer.playerControl.SetClientRef(this);
                        physScene.SpawnCharacterRep(playerPrefab, Vector3.up, Quaternion.identity);
                        localPlayer.playerControl.physScene = this.physScene;
                    }
                }
                else
                {
                    Debug.LogError("Could not spawn player.  The Prefab is null");
                }
            }
            else
            {
                Debug.LogError("The server has denied the spawn request.");
            }
        }
        else if (type == 13)//Spawn other
        {
            int playerID = reader.ReadInt();
            if (players[playerID].inUse)
            {
                if (!players[playerID].isSpawned)
                {
                    players[playerID].Spawn(playerPrefab, ReadExtensions.ReadVector3(ref reader), ReadExtensions.ReadQuaternion(ref reader));
                }
                else
                {
                    Debug.LogError("Player (" + playerID + ") is already spawned!");
                }
            }
            else
            {
                Debug.LogError("Player (" + playerID + ") does not exist.  Can't spawn player.");
            }
        }
        else if (type == 14)//New Player Connection
        {
            Debug.Log("A new player has connected to the server.");
            int newPlayerId = reader.ReadInt();
            AddPlayer(newPlayerId, false);
            //uint seqNum = reader.ReadUInt();
            //Vector3 newPos = ReadExtensions.ReadVector3(ref reader); 
            //Quaternion newRot = ReadExtensions.ReadQuaternion(ref reader);
            ////Debug.LogError("Received new position from server: " + newPos);
            //playerControl.GetComponent<Player>().HandleSnapshot(seqNum, newPos, newRot);
        }
        else if (type == 15)//player disconnect
        {
            int ID = reader.ReadInt();
            if (ID == thisClientID)
            {
                OnDisconnect();
            }
            RemovePlayer(ID);
        }
        else if (type == 16)
        {
            serverTickrate = reader.ReadFloat();
            Debug.LogError("Received server tickrate change update.  Now: " + serverTickrate);
        }          
        else if (type == 20)
        {
            if (pingText != null)
            {
                //pingText.text = "Ping: " + (Time.time - reader.ReadFloat()) * 1000;
            }
        }
        else if (type == 255)//disconnect
        {
            Debug.LogError("Disconnect reply from server...");
            OnDisconnect();
        }
        else
        {
            Debug.LogWarning("Message type (" + type + ") unrecognized");
        }
    }
}

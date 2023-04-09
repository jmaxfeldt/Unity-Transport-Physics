using System.Collections.Generic;
using UnityEngine;
using Unity.Networking.Transport;
using NetMessages;

public class ServerPlayer
{
    public int id = -1;
    public NetworkConnection connection;
    public Server serverRef;
    public GameObject playerCharacterRep = null;
    public PlayerMove playerMove = null;
    
    Vector3 lastPos;
    Quaternion lastRot;

    public MultiInputMessage latestInputs = new MultiInputMessage();
    public List<StateInfo> latestStates = new List<StateInfo>();
    public byte processedSinceLast = 0;
    public uint lastProcessedInput = 0;

    public bool inUse = false;
    public bool isSpawned = false;
    public bool hasNewSnapshotData = false;

    public ServerPlayer() { }
    public ServerPlayer(NetworkConnection connection, Server serverRef)
    {
        this.connection = connection;
        id = connection.InternalId;
        this.serverRef = serverRef;
        inUse = true;
        Debug.LogWarning("New ServerPlayer created.  Internal ID: " + this.connection.InternalId);
    }

    public bool Spawn(GameObject playerCharacter)
    {
        this.playerCharacterRep = GameObject.Instantiate(playerCharacter, new Vector3(0, 2, 0), Quaternion.identity);
        if (this.playerCharacterRep != null)
        {
            playerMove = playerCharacterRep.GetComponent<PlayerMove>();
            isSpawned = true;
            lastPos = playerCharacter.transform.position;
            lastRot = playerCharacter.transform.rotation;
            return true;
        }
        return false;
    }

    public bool Despawn()
    {
        if (playerCharacterRep != null)
        {
            GameObject.Destroy(playerCharacterRep);
            return true;
        }
        return false;
    }

    int processedInputsCount = 0;
    public void ProcessInputs()
    {
        if (isSpawned)
        {
            
            //Debug.Log("Velocity: " + playerCharacterRep.GetComponent<Rigidbody>().velocity);
            if (latestInputs != null && latestInputs.lastSequence != lastProcessedInput)
            {
                //Debug.LogError("Number of inputs to process: " + latestInputs.messages.Length);
                if (latestInputs.lastSequence > lastProcessedInput)
                {
                    //int moveCounter = 0;

                    for (int i = 0; i < latestInputs.messages.Length; i++)
                    {
                        //Debug.Log("Sequence at index (" + i + ") is: " + inputs[i].sequenceNum);
                        if (latestInputs.messages[i].sequenceNum <= lastProcessedInput)
                        {
                            continue;
                        }
                        
                        //Debug.LogError("Received input sequence (" + latestInputs.messages[i].sequenceNum + ") bitmask value: " + Convert.ToString(latestInputs.messages[i].moveKeysBitmask, 2).PadLeft(8, '0'));
                        playerMove.Move(latestInputs.messages[i].moveKeysBitmask);
                        //Debug.LogError("Server processed inputs count: " + processedInputsCount);
                        Debug.LogError("Velocity after sequence " + latestInputs.messages[i].sequenceNum + ": " + playerCharacterRep.GetComponent<Rigidbody>().velocity);
                        processedInputsCount++;
                        //Debug.LogError("Server position after phys move for input " + latestInputs.messages[i].sequenceNum + ": " + playerCharacterRep.transform.position);
                        
                        processedSinceLast++;
                        //Debug.Log("Server player position: " + serverPlayer.transform.position);
                        lastProcessedInput = latestInputs.messages[i].sequenceNum;
                        serverRef.SendToClient(serverRef.unreliableSimPipeline, connection, new StateInfoMessage(new StateInfo(lastProcessedInput, playerCharacterRep.transform.position, playerCharacterRep.transform.rotation, 
                                             playerCharacterRep.GetComponent<Rigidbody>().velocity, playerCharacterRep.GetComponent<Rigidbody>().angularVelocity)));
                        //moveCounter++;
                    }

                    //Debug.LogError("Server moved player (" + id + ") " + moveCounter + " times");
                    //SendToClient(connection, new UpdatePositionMessage(lastProcessedClientInput, serverPlayer.transform.position, serverPlayer.transform.rotation));
                    lastPos = playerCharacterRep.transform.position;
                    lastRot = playerCharacterRep.transform.rotation;
                    hasNewSnapshotData = true;
                }
                else
                {
                    Debug.LogWarning("Out of order multiInput message.  Ignoring....");
                    Debug.LogError("Expected: " + (lastProcessedInput + 1) + " - Got: " + latestInputs.messages[latestInputs.messages.Length - 1].sequenceNum);
                }
            }
            //else if (lastTransform.position != playerCharacterRep.transform.position || lastTransform.rotation != playerCharacterRep.transform.rotation)//something here to send updates when objects are undergoing physics movements
            else if (lastPos != playerCharacterRep.transform.position || lastRot != playerCharacterRep.transform.rotation)
            {
                //Debug.Log("Physics Move!");
                //SendToClient(new UpdatePositionMessage(lastProcessedInput, playerCharacterRep.transform.position, playerCharacterRep.transform.rotation));
                lastPos = playerCharacterRep.transform.position;
                lastRot = playerCharacterRep.transform.rotation;
                hasNewSnapshotData = true;
            }
            //Debug.Log("last position: " + lastTransform.position + " - Current position: " + playerCharacterRep.transform.position);
        }
    }

    public SnapShotInfo GetSnapshotInfo()
    {
        hasNewSnapshotData = false;
        processedSinceLast = 0;
        return new SnapShotInfo((short)id, playerCharacterRep.transform.position, playerCharacterRep.transform.rotation);
    }

    //public StateInfo GetStateInfo()
    //{

    //}
}

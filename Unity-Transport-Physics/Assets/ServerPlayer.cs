using System.Collections.Generic;
using UnityEngine;
using Unity.Networking.Transport;
using NetMessages;

public class ServerPlayer
{
    public int id = -1;
    public NetworkConnection connection;
    public Server serverRef;

    public GameObject playerCharRep = null;
    public Rigidbody rb = null;

    public ServerMultiMovePhysScene physSceneRef = null;
    public GameObject physSceneCharRep = null;
    public Rigidbody physRepRB = null;

    public PlayerMove playerMove = null;
    
    Vector3 lastPos;
    Quaternion lastRot;

    public MultiInputMessage latestInputs = new MultiInputMessage();
    //public List<StateInfo> latestStates = new List<StateInfo>();
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
        physSceneRef = serverRef.physScene;
        inUse = true;
        Debug.LogWarning("New ServerPlayer created.  Internal ID: " + this.connection.InternalId);
    }

    public bool Spawn(GameObject playerPrefab, Vector3 pos, Quaternion rot)
    {
        this.playerCharRep = GameObject.Instantiate(playerPrefab, pos, rot);
        if (this.playerCharRep != null)
        {
            playerMove = playerCharRep.GetComponent<PlayerMove>();
            rb = playerCharRep.GetComponent<Rigidbody>();

            if(!SpawnPhysSceneRep(playerPrefab, pos, rot))
            {
                Debug.LogError("Failed to spawn player.  Could not create its physics scene representation");
                GameObject.Destroy(playerCharRep);
                return false;
            }

            isSpawned = true;

            lastPos = playerCharRep.transform.position;
            lastRot = playerCharRep.transform.rotation;            
            return true;
        }
        else
        {
            Debug.Log("Spawn failed for player with id: " + id);
        }
        return false;
    }

    public bool SpawnPhysSceneRep(GameObject playerPrefab, Vector3 pos, Quaternion rot)
    {
        physSceneCharRep = physSceneRef.SpawnCharacterRep(playerPrefab, pos, rot);
        if(physSceneCharRep != null)
        {
            physRepRB = physSceneCharRep.GetComponent<Rigidbody>();
            return true;
        }
        return false;
    }

    public bool Despawn()
    {
        if (playerCharRep != null)
        {
            GameObject.Destroy(playerCharRep);
            GameObject.Destroy(physSceneCharRep);
            return true;
        }
        return false;
    }
    
    public void ProcessInputs()
    {
        if (isSpawned)
        {           
            if (latestInputs != null && latestInputs.lastSequence != lastProcessedInput)
            {
                //Debug.LogError("Number of inputs to process: " + latestInputs.messages.Length);
                if (latestInputs.lastSequence > lastProcessedInput)
                {
                    for (int i = 0; i < latestInputs.messages.Length; i++)
                    {
                        //Debug.Log("Sequence at index (" + i + ") is: " + inputs[i].sequenceNum);
                        if (latestInputs.messages[i].sequenceNum <= lastProcessedInput)
                        {
                            continue;
                        }
                        //Debug.LogError("Received input sequence (" + latestInputs.messages[i].sequenceNum + ") bitmask value: " + Convert.ToString(latestInputs.messages[i].moveKeysBitmask, 2).PadLeft(8, '0'));

                        //If there are multiple inputs to be processed, simulate the oldest ones in the multi-move physics scene and then let the main physics simulation simulate the last one
                        if (i == latestInputs.messages.Length - 1)//MAY BE A BAD IDEA Move doesn't move until after the physics simulation is done on the main scene.  Could accidentally send an unupdated position causing prediction to fail
                        {
                            Debug.LogError("MOVING ID: " + id + " - sequence: " + latestInputs.messages[i].sequenceNum + " - On server tick: " + serverRef.serverTick);
                            playerMove.Move(latestInputs.messages[i].moveKeysBitmask);
                        }
                        else
                        {
                            StateInfo newState = physSceneRef.Simulate(physSceneCharRep.transform, physRepRB, playerCharRep.transform.position, playerCharRep.transform.rotation, rb.velocity, rb.angularVelocity, latestInputs.messages[i].moveKeysBitmask);
                            playerCharRep.transform.SetPositionAndRotation(newState.position, newState.rotation);
                            playerMove.SetVelocities(newState.linearVelocity, newState.angularVelocity);
                        }

                        lastProcessedInput = latestInputs.messages[i].sequenceNum;                       
                    }
                 
                    //lastPos = playerCharRep.transform.position;
                    //lastRot = playerCharRep.transform.rotation;
                    hasNewSnapshotData = true;
                }
                else
                {
                    Debug.LogWarning("Out of order multiInput message.  Ignoring....");
                    Debug.LogError("Expected: " + (lastProcessedInput + 1) + " - Got: " + latestInputs.messages[latestInputs.messages.Length - 1].sequenceNum);
                }
            }
            //else if (lastTransform.position != playerCharacterRep.transform.position || lastTransform.rotation != playerCharacterRep.transform.rotation)//something here to send updates when objects are undergoing physics movements
            //else if (lastPos != playerCharRep.transform.position || lastRot != playerCharRep.transform.rotation)
            //{
            //    //Debug.Log("Physics Move!");
            //    //SendToClient(new UpdatePositionMessage(lastProcessedInput, playerCharacterRep.transform.position, playerCharacterRep.transform.rotation));
            //    lastPos = playerCharRep.transform.position;
            //    lastRot = playerCharRep.transform.rotation;
            //    hasNewSnapshotData = true;
            //}
            //Debug.Log("last position: " + lastTransform.position + " - Current position: " + playerCharacterRep.transform.position);
        }
    }

    public SnapShotInfo GetSnapshotInfo()
    {
        hasNewSnapshotData = false;
        processedSinceLast = 0;
        return new SnapShotInfo((short)id, playerCharRep.transform.position, playerCharRep.transform.rotation);
    }

    public StateInfo GetStateInfo()
    {
        return new StateInfo(lastProcessedInput, playerCharRep.transform.position, playerCharRep.transform.rotation, rb.velocity, rb.angularVelocity);
    }
}

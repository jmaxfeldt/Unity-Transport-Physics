using UnityEngine;
using Unity.Networking.Transport;
using NetMessages;

public class ServerPlayer : MonoBehaviour
{
    public int id = -1;
    public NetworkConnection connection;
    public GameObject playerCharacterRep = null;
    public PlayerMove playerMove = null;
    
    Vector3 lastPos;
    Quaternion lastRot;

    public MultiInputMessage latestInputs = new MultiInputMessage();
    public byte processedSinceLast = 0;
    public uint lastProcessedInput = 0;

    public bool inUse = false;
    public bool isSpawned = false;
    public bool hasNewSnapshotData = false;

    public ServerPlayer() { }
    public ServerPlayer(NetworkConnection connection)
    {
        this.connection = connection;
        id = connection.InternalId;
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

    public void ProcessInputs()
    {
        if (isSpawned)
        {
            //Debug.Log("Velocity: " + playerCharacterRep.GetComponent<Rigidbody>().velocity);
            if (latestInputs != null && latestInputs.lastSequence != lastProcessedInput)
            {
                if (latestInputs.messages[latestInputs.messages.Length - 1].sequenceNum > lastProcessedInput)
                {
                    //int moveCounter = 0;

                    for (int i = 0; i < latestInputs.messages.Length; i++)
                    {
                        //Debug.Log("Sequence at index (" + i + ") is: " + inputs[i].sequenceNum);
                        if (latestInputs.messages[i].sequenceNum <= lastProcessedInput)
                        {
                            continue;
                        }
                        if (usePhysicsMove)
                        {
                            //Debug.LogError("Received input sequence (" + latestInputs.messages[i].sequenceNum + ") bitmask value: " + Convert.ToString(latestInputs.messages[i].moveKeysBitmask, 2).PadLeft(8, '0'));
                            playerMove.PhysicsMove(latestInputs.messages[i].deltaTime, latestInputs.messages[i].moveKeysBitmask);
                            Debug.LogError("Server position after phys move for input " + latestInputs.messages[i].sequenceNum + ": " + playerCharacterRep.transform.position);
                        }
                        else
                        {
                            playerMove.BitmaskMove(latestInputs.messages[i].deltaTime, latestInputs.messages[i].moveKeysBitmask);
                        }
                        processedSinceLast++;
                        //Debug.Log("Server player position: " + serverPlayer.transform.position);
                        lastProcessedInput = latestInputs.messages[i].sequenceNum;
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
                Debug.Log("Physics Move!");
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
}

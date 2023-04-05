using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClientPlayer
{
    public int id = -1;
    public bool isLocal = false;
    public bool isSpawned = false;
    public bool inUse = false;
    public float interpTime = .1f;

    GameObject playerCharacterRep;
    public Player playerControl = null; //will remain null on all but the local player.  This is what handles inputs etc.
    public PhysicsMove physMove = null;
    PlayerMove playerMove = null;

    List<InterpBufferData> interpolationBuffer; //should use some kind of pool for this

    public ClientPlayer() { }
    public ClientPlayer(int id, bool isLocal)
    {
        this.id = id;
        this.isLocal = isLocal;
        this.inUse = true;
        interpolationBuffer = new List<InterpBufferData>();        
    }

    public bool Spawn(GameObject playerCharacter, Vector3 position, Quaternion rotation)
    {
        this.playerCharacterRep = GameObject.Instantiate(playerCharacter, position, rotation);
        if (this.playerCharacterRep != null)
        {
            playerMove = playerCharacterRep.GetComponent<PlayerMove>();
            if (isLocal)
            {
                if (usePhysicsMove)
                {
                    PhysicsMove physMove = playerCharacterRep.AddComponent<PhysicsMove>();
                    physMove.id = id;
                    physMove.isLocalPlayer = true;
                    physMove.isSpawned = true;

                }
                else
                {
                    Player newPlayer = playerCharacterRep.AddComponent<Player>();
                    newPlayer.id = id;
                    newPlayer.isLocalPlayer = true;
                    newPlayer.isSpawned = true;
                }
            }
            isSpawned = true;
            return true;
        }
        return false;
    }

    public void AddToInterpBuffer(float timeStamp, Vector3 position, Quaternion rotation)
    {
        interpolationBuffer.Add(new InterpBufferData(timeStamp, position, rotation));
    }

    public void RemoveFromInterpBuffer(int id)
    {
        interpolationBuffer.RemoveAt(id);
    }

    public void InterpolateMove(float updateRate)
    {
        float renderTimeStamp = Time.time - ((1 / updateRate) * 3);//((1 / updateRate) * 2);
        //Debug.LogError("Using Interp Move.  Buffer Length: " + interpolationBuffer.Count);
        while (interpolationBuffer.Count >= 2 && interpolationBuffer[1].timeStamp <= renderTimeStamp)
        {
            interpolationBuffer.RemoveAt(0);
        }

        if (isSpawned)
        {
            if (interpolationBuffer.Count >= 2 && interpolationBuffer[0].timeStamp <= renderTimeStamp && renderTimeStamp <= interpolationBuffer[1].timeStamp)
            {
                //Debug.LogError("Actual Interpolation move.  I don't think it gets here");
                //Debug.LogError("Interpolation.  Element 0 timestamp: " + interpolationBuffer[0].timeStamp + " -Render time stamp: " + renderTimeStamp + " -Element 1 time stamp: " + interpolationBuffer[1].timeStamp);
                //Debug.LogError("Fraction moved: " + ((renderTimeStamp - interpolationBuffer[0].timeStamp) / (interpolationBuffer[1].timeStamp - interpolationBuffer[0].timeStamp)));
                //if (playerCharacterRep.transform.position != interpolationBuffer[0].position)
                //{
                playerCharacterRep.transform.position = Vector3.Lerp(interpolationBuffer[0].position, interpolationBuffer[1].position,
                                                                    (renderTimeStamp - interpolationBuffer[0].timeStamp) / (interpolationBuffer[1].timeStamp - interpolationBuffer[0].timeStamp));

                //}
            }
            else if (interpolationBuffer.Count == 1 && renderTimeStamp < interpolationBuffer[0].timeStamp)
            {
                Debug.LogError("One element left in interpolation buffer.  Element timestamp: " + interpolationBuffer[0].timeStamp + " - Render timestamp: " + renderTimeStamp);
                playerCharacterRep.transform.position = Vector3.Lerp(playerCharacterRep.transform.position, interpolationBuffer[0].position, .25f);
            }
        }
    }

    public void DummyMove(Vector3 pos, Quaternion rot)
    {
        if (isSpawned)
        {
            playerCharacterRep.transform.SetPositionAndRotation(pos, rot);
        }
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

    public MonoBehaviour GetPlayerControl()
    {
        if (isLocal)
        {
            if (usePhysicsMove)
            {
                if (playerCharacterRep != null && playerCharacterRep.GetComponent<PhysicsMove>() != null)
                {
                    return playerCharacterRep.GetComponent<PhysicsMove>();
                }
                else
                {
                    Debug.Log("Could not find the player control.");
                }
            }
            else
            {
                if (playerCharacterRep != null && playerCharacterRep.GetComponent<Player>() != null)
                {
                    return playerCharacterRep.GetComponent<Player>();
                }
                else
                {
                    Debug.Log("Could not find the player control.");
                }
            }
        }
        else
        {
            Debug.LogError("Cannot retrieve playerControl from a non-local player");
        }
        return null;
    }
}

public struct InterpBufferData
{
    public float timeStamp;
    public Vector3 position;
    public Quaternion rotation;

    public InterpBufferData(float timeStamp, Vector3 position, Quaternion rotation)
    {
        this.timeStamp = timeStamp;
        this.position = position;
        this.rotation = rotation;
    }

    public void SetData(float timeStamp, Vector3 position, Quaternion rotation)
    {
        this.timeStamp = timeStamp;
        this.position = position;
        this.rotation = rotation;
    }
}



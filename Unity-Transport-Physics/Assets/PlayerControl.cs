using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetMessages;

public class PlayerControl : MonoBehaviour
{
    Client clientRef = null;
    PlayerMove playerMove = null;

    uint inputSequence = 1;
    uint responseCount = 0;
    uint lastResponseSeqNum = 0;

    float moveSpeed = 10;
    Vector3 lastPos;
    Quaternion lastRot;
    Vector3 lerpTarget;

    float inputPollingRate = 33f;
    float nextPollTime = 0;
    float lastPollTime = 0;

    public bool isLocalPlayer;
    public bool isSpawned = false;
    public bool sendUnackdInputs = false;

    bool usePrediction = true;
    bool useReconciliation = false;

    List<InputMessage> unacknowledgedInputs = new List<InputMessage>(); //use a pool for this?
    InputQueue inputQueue;

    byte moveKeysBitmask = 0;


    // Start is called before the first frame update
    void Start()
    {
        //clientRef = FindObjectOfType<Client>();
        playerMove = GetComponent<PlayerMove>();
        inputQueue = new InputQueue(20);

        nextPollTime = Time.time;
        lastPollTime = Time.time;
    }


    void FixedUpdate()
    {
        //InputMessage input = new InputMessage(inputSequence, deltaTime, moveKeysBitmask);
        if (isSpawned)
        {
            while (inputQueue.numInputs > 0)
            {
                InputMessage input = new InputMessage(inputSequence, Time.fixedDeltaTime, inputQueue.Dequeue());

                if (usePrediction)
                {
                    playerMove.Move(input.moveKeysBitmask);
                    input.predictedPos = transform.position;
                    input.predictedRot = transform.rotation;
                    Debug.LogError("Predicted position after Client phys move for input " + inputSequence + ": " + input.predictedPos);
                    //Debug.Log("Sequence " + inputSequence + " predicted position: " + input.predictedPos + " -Delta time: " + deltaTime);
                }
                unacknowledgedInputs.Add(input);
                inputSequence++;

                if (sendUnackdInputs && unacknowledgedInputs.Count > 0)
                {
                    //Debug.LogError("Sending input sequence (" + inputSequence +") bitmask value: " + Convert.ToString(input.moveKeysBitmask, 2).PadLeft(8, '0'));
                    clientRef.SendToServer(clientRef.unreliableSimPipeline, new MultiInputMessage(unacknowledgedInputs));            
                }
                else
                {
                    clientRef.SendToServer(clientRef.unreliableSimPipeline, input);
                }
            }
        }
        //clientInputCounter.text = "Client Inputs: " + inputSequence;
    }

    // Update is called once per frame
    void Update()
    {
        if (isLocalPlayer && isSpawned)
        {
            if (Input.GetKey(KeyCode.W))
            {
                moveKeysBitmask |= 1;
            }

            if (Input.GetKey(KeyCode.S))
            {
                moveKeysBitmask |= 2;
            }

            if (Input.GetKey(KeyCode.A))
            {
                moveKeysBitmask |= 4;
            }

            if (Input.GetKey(KeyCode.D))
            {
                moveKeysBitmask |= 8;
            }

            if (Time.time >= nextPollTime)
            {
                nextPollTime = Time.time + 1 / inputPollingRate;
                float deltaTime = Time.time - lastPollTime;
                //Debug.Log("Delta Time: " + deltaTime);            
                lastPollTime = Time.time;

                if (moveKeysBitmask != 0)
                {
                    inputQueue.Enqueue(moveKeysBitmask);
                }
                moveKeysBitmask = 0;
                //Debug.Log(Convert.ToString(moveKeysBitmask, 2).PadLeft(8, '0'));
            }
        }
    }

    public void HandleSnapshot(uint responseNum, Vector3 position, Quaternion rotation) //Gets called on the client when the server sends updated position
    {      
        if (responseNum > lastResponseSeqNum) //Server doesn't resend snapshots.  This can't be used for things that must be reliable
        {
            lastResponseSeqNum = responseNum;
            //lerpTarget = position;          
            transform.SetPositionAndRotation(position, rotation);

            //Debug.Log("Response to " + responseNum + " position: " + position);
            for (int i = 0; i < unacknowledgedInputs.Count; i++)//CHECKING FOR PREDICTION ERRORS?
            {
                if (unacknowledgedInputs[i].sequenceNum == responseNum)
                {
                    if (unacknowledgedInputs[i].predictedPos != position)
                    {
                        Debug.LogWarning("A client prediction error has occured.");
                        //Debug.LogWarning("Sequence " + responseNum + "  -Predicted for response " + unacknowledgedInputs[i].sequenceNum + ": " + unacknowledgedInputs[i].predictedPos + "  - Actual: " + position);
                        //unacknowledgedInputs.RemoveAt(i);                   
                    }
                    break;
                }
            }

            //reconciliation code
            if (useReconciliation)
            {
                int loopCount = 0;
                while (loopCount < unacknowledgedInputs.Count)
                {
                    if (unacknowledgedInputs[loopCount].sequenceNum <= responseNum)
                    {
                        unacknowledgedInputs.RemoveAt(loopCount);
                        //unackdCounter.text = "Unack'd Inputs: " + unacknowledgedInputs.Count;
                    }
                    else
                    {
                        playerMove.Move(unacknowledgedInputs[loopCount].moveKeysBitmask);
                        //BitmaskMove(unacknowledgedInputs[loopCount].sequenceNum, unacknowledgedInputs[loopCount].deltaTime, unacknowledgedInputs[loopCount].moveKeysBitmask);
                        loopCount++;
                    }
                }
            }
            else
            {
                unacknowledgedInputs.Clear();
            }
        }
        //else if(responseNum == lastResponseSeqNum && (position != playerCharacter.transform.position || rotation != playerCharacter.transform.rotation))
        //else if (lastResponseSeqNum == inputSequence && responseNum == lastResponseSeqNum && (position != transform.position || rotation != transform.rotation))
        else if (unacknowledgedInputs.Count == 0) //Not waiting for a response.  Any new positions received would be server physics moves.  unacked packets are sent repeatedly, so this can't get stuck for long.
        {
            //transform.SetPositionAndRotation(position, rotation);
        }
        else if (responseNum < lastResponseSeqNum)
        {
            Debug.Log("out of order snapshot detected...");
        }
    }

    public void SetClientRef(Client client)
    {
        clientRef = client;
    }
}

public class InputQueue
{
    int head;
    int tail;
    public int numInputs;

    byte[] inputs;

    public InputQueue(int size)
    {
        this.head = 0;
        this.tail = 0;
        this.numInputs = 0;
        inputs = new byte[size];
    }

    public void Enqueue(byte inputMask)
    {
        inputs[head] = inputMask;
        head++;
        numInputs++;
        if (head >= inputs.Length)
        {
            head = 0;
        }
    }

    public byte Dequeue()
    {
        int temp = tail;
        tail++;
        if (tail >= inputs.Length)
        {
            tail = 0;
        }
        numInputs--;
        return inputs[temp];
    }
}

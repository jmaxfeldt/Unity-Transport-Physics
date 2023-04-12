using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NetMessages;

public class PlayerControl : MonoBehaviour
{
    Client clientRef = null;
    PlayerMove playerMove = null;
    Rigidbody rb;

    public ReconciliationPhysScene physScene;
    public float errorThreshold = .01f;

    uint inputSequence = 1;
    uint responseCount = 0;
    uint lastResponseSeqNum = 0;

    float inputPollingRate = 50f;
    float nextPollTime = 0;
    float lastPollTime = 0;

    public bool isLocalPlayer;
    public bool isSpawned = false;
    public bool sendUnackdInputs = true;

    bool usePrediction = true;
    bool useReconciliation = true;

    List<InputMessage> unackdInputs = new List<InputMessage>(); //use a pool for this?
    //InputQueue<InputMessage> unackdIns;
    InputQueue inputQueue;

    byte moveKeysBitmask = 0;


    // Start is called before the first frame update
    void Start()
    {
        playerMove = GetComponent<PlayerMove>();
        rb = GetComponent<Rigidbody>();
        inputQueue = new InputQueue(20);
        //unackdIns = new InputQueue<InputMessage>(50);

        nextPollTime = Time.time;
        lastPollTime = Time.time;
    }

    int processedCount = 0;
    void FixedUpdate()
    {
        //if (isLocalPlayer && isSpawned)
        //{
        //    if (Input.GetKey(KeyCode.W))
        //    {
        //        moveKeysBitmask |= 1;
        //    }

        //    if (Input.GetKey(KeyCode.S))
        //    {
        //        moveKeysBitmask |= 2;
        //    }

        //    if (Input.GetKey(KeyCode.A))
        //    {
        //        moveKeysBitmask |= 4;
        //    }

        //    if (Input.GetKey(KeyCode.D))
        //    {
        //        moveKeysBitmask |= 8;
        //    }
          
        //    inputQueue.Enqueue(moveKeysBitmask);
            


        //    moveKeysBitmask = 0;
        //    //Debug.Log(Convert.ToString(moveKeysBitmask, 2).PadLeft(8, '0'));
        //}


        //InputMessage input = new InputMessage(inputSequence, deltaTime, moveKeysBitmask);
        if (isSpawned)
        {
            //Debug.LogError("Input Queue Length: " + inputQueue.numInputs);

            while (inputQueue.numInputs > 0) //Multiple moves here and on the server within a single physics update are probably not working as intended.  They should be simulated in another physics scene like with reconciliation
            {
              
                InputMessage input = new InputMessage(inputSequence, Time.fixedDeltaTime, inputQueue.Dequeue());

                if (usePrediction)
                {
                    if (inputQueue.numInputs == 1)
                    {
                        playerMove.Move(input.moveKeysBitmask);
                    }
                    else
                    {
                        physScene.Simulate(transform.position, transform.rotation, rb.velocity, rb.angularVelocity, moveKeysBitmask);
                    }
                    input.SetPredictions(transform.position, transform.rotation, rb.velocity, rb.angularVelocity);
                    
                    //Debug.LogError("Predicted position after Client phys move for input " + inputSequence + ": " + input.predictedPos);
                    //Debug.Log("Sequence " + inputSequence + " predicted position: " + input.predictedPos + " -Delta time: " + deltaTime);
                }
                //Debug.LogError("Client processed input count: " + processedCount);
                processedCount++;
                unackdInputs.Add(input);
                inputSequence++;

                if (sendUnackdInputs && unackdInputs.Count > 0)
                {
                    //Debug.LogError("Sending input sequence (" + inputSequence +") bitmask value: " + Convert.ToString(input.moveKeysBitmask, 2).PadLeft(8, '0'));
                    clientRef.SendToServer(clientRef.unreliableSimPipeline, new MultiInputMessage(unackdInputs));                 
                }
                else
                {
                    clientRef.SendToServer(clientRef.unreliableSimPipeline, input);
                }
            }
        }
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

              
                inputQueue.Enqueue(moveKeysBitmask);
                

                moveKeysBitmask = 0;
                //Debug.Log(Convert.ToString(moveKeysBitmask, 2).PadLeft(8, '0'));
            }
        }
    }

    public void HandleSnapshot(uint responseNum, Vector3 position, Quaternion rotation, Vector3 linearVelocity, Vector3 angularVelocity) //Gets called on the client when the server sends updated position
    {
        bool hasPredictionError = false;

        if (responseNum >= lastResponseSeqNum) //Server doesn't resend snapshots.  This can't be used for things that must be reliable
        {
            lastResponseSeqNum = responseNum;
            //lerpTarget = position;          


            //Debug.Log("Response to " + responseNum + " position: " + position);
            for (int i = 0; i < unackdInputs.Count; i++)//CHECKING FOR PREDICTION ERRORS?
            {
                if (unackdInputs[i].sequenceNum == responseNum)
                {
                    if (Mathf.Abs(Vector3.Distance(unackdInputs[i].predictedPos, position)) > errorThreshold)// || Mathf.Abs(Vector3.Distance(unackdInputs[i].predictedRot.eulerAngles, rotation.eulerAngles)) > errorThreshold)
                    {
                        hasPredictionError = true;
                        Debug.Log("A position prediction error has occured!  Distance: " + Mathf.Abs(Vector3.Distance(unackdInputs[i].predictedPos, position)));
                    }
                    //if (Mathf.Abs(Vector3.Distance(unackdInputs[i].predictedRot.eulerAngles, rotation.eulerAngles)) > errorThreshold)
                    //{
                    //    hasPredictionError = true;
                    //    Debug.Log("A rotation prediction error has occured!  Distance: "  + Mathf.Abs(Vector3.Distance(unackdInputs[i].predictedRot.eulerAngles, rotation.eulerAngles)));
                    //}
                    break;
                }
            }

            //reconciliation code
            if (useReconciliation)
            {
                int loopCount = 0;

                transform.SetPositionAndRotation(position, rotation);
                playerMove.SetVelocities(linearVelocity, angularVelocity);
                
                while (loopCount < unackdInputs.Count)
                {
                    if (unackdInputs[loopCount].sequenceNum <= responseNum)
                    {
                        unackdInputs.RemoveAt(loopCount);
                        //unackdCounter.text = "Unack'd Inputs: " + unacknowledgedInputs.Count;
                    }                  
                    else
                    {                      
                        if(hasPredictionError)
                        {
                            StateInfo simState = physScene.Simulate(transform.position, transform.rotation, GetComponent<Rigidbody>().velocity, GetComponent<Rigidbody>().angularVelocity, unackdInputs[loopCount].moveKeysBitmask);
                            SetState(simState.position, simState.rotation, simState.linearVelocity, simState.angularVelocity);

                            unackdInputs[loopCount].SetPredictions(transform.position, transform.rotation, rb.velocity, rb.angularVelocity);               
                        }
                        else
                        {
                            StateInfo simState = physScene.Simulate(transform.position, transform.rotation, GetComponent<Rigidbody>().velocity, GetComponent<Rigidbody>().angularVelocity, unackdInputs[loopCount].moveKeysBitmask);
                            SetState(simState.position, simState.rotation, simState.linearVelocity, simState.angularVelocity);

                            //playerMove.Move(unacknowledgedInputs[loopCount].moveKeysBitmask);
                        }
                        //BitmaskMove(unacknowledgedInputs[loopCount].sequenceNum, unacknowledgedInputs[loopCount].deltaTime, unacknowledgedInputs[loopCount].moveKeysBitmask);
                        loopCount++;
                    }
                }
                //Debug.LogError("Velocity after reconciliation: " + GetComponent<Rigidbody>().velocity + " -at sequence: " + inputSequence);
            }
            else
            {
                unackdInputs.Clear();
            }
        }
        //else if(responseNum == lastResponseSeqNum && (position != playerCharacter.transform.position || rotation != playerCharacter.transform.rotation))
        //else if (lastResponseSeqNum == inputSequence && responseNum == lastResponseSeqNum && (position != transform.position || rotation != transform.rotation))
        else if (unackdInputs.Count == 0) //Not waiting for a response.  Any new positions received would be server physics moves.  unacked packets are sent repeatedly, so this can't get stuck for long.
        {
            //transform.SetPositionAndRotation(position, rotation);
        }
        else if (responseNum < lastResponseSeqNum)
        {
            Debug.Log("out of order snapshot detected...");
        }
    }

    public void SetState(Vector3 pos, Quaternion rot, Vector3 velocity, Vector3 angularVelocity)
    {
        transform.SetPositionAndRotation(pos, rot);
        rb.velocity = velocity;
        rb.angularVelocity = angularVelocity;
    }

    public void SetClientRef(Client client)
    {
        clientRef = client;
    }
}

public class UnackdInputQueue
{
    int head;
    int tail;
    int size;
    public int numInputs;

    InputMessage[] inputs;

    public UnackdInputQueue(int size)
    {
        this.size = size;
        this.head = 0;
        this.tail = 0;
        this.numInputs = 0;
        inputs = new InputMessage[size];
    }

    public void Enqueue(InputMessage element)
    {
        inputs[head] = element;
        head++;
        numInputs++;
        if (head >= inputs.Length)
        {
            head = 0;
        }
    }

    public InputMessage Dequeue()
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

    public void AdvanceTail()
    {
        tail++;
        if (tail >= inputs.Length)
        {
            tail = 0;
        }
        numInputs--;
    }

    public void RemoveAckd(uint sequence)
    {
        for(int i = 0; i < numInputs; i++)
        {
            if(inputs[tail].sequenceNum <= sequence)
            {
                AdvanceTail();
            }
        }
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

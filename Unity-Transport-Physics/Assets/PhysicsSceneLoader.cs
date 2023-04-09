using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using NetMessages;

public class PhysicsSceneLoader : MonoBehaviour
{
    public string physicsSceneName;
    Scene reconciliationScene;
    PhysicsScene physicsScene;
    [SerializeField]
    Transform SceneGeometryParent;
    GameObject characterRepPrefab;
    GameObject characterRep;


    void CreatePhysicsScene()
    {
        reconciliationScene = SceneManager.CreateScene("Reconciliation Scene", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        physicsScene = reconciliationScene.GetPhysicsScene();

        foreach(Transform obj in SceneGeometryParent)
        {
            var sceneObj = Instantiate(obj.gameObject, obj.position, obj.rotation);
            sceneObj.GetComponent<Renderer>().enabled = false;
            SceneManager.MoveGameObjectToScene(sceneObj, reconciliationScene);
        }
    }

    public void SpawnCharacterRep(GameObject charPrefab, Vector3 position, Quaternion rotation)
    {
        characterRep = Instantiate(charPrefab, position, rotation);
        characterRep.GetComponent<Renderer>().enabled = false;
        SceneManager.MoveGameObjectToScene(characterRep, reconciliationScene);
    }

    public StateInfo Resimulate(Vector3 startPos, Quaternion StartRot, Vector3 startVelocity, Vector3 startAngularVelocity, ref List<InputMessage> inputs)
    {
        characterRep.transform.position = startPos;
        characterRep.transform.rotation = StartRot;
        characterRep.GetComponent<Rigidbody>().velocity = startVelocity;
        characterRep.GetComponent<Rigidbody>().angularVelocity = startAngularVelocity;

        for(int i = 0; i < inputs.Count; i++)
        {
            characterRep.GetComponent<PlayerMove>().Move(inputs[i].moveKeysBitmask);
            physicsScene.Simulate(Time.fixedDeltaTime);

            inputs[i].predictedPos = characterRep.transform.position;
            inputs[i].predictedRot = characterRep.transform.rotation;
            inputs[i].predictedVelocity = characterRep.GetComponent<Rigidbody>().velocity;
            inputs[i].predictedAngularVelocity = characterRep.GetComponent<Rigidbody>().angularVelocity;
        }       

        Debug.LogError("ReSimulate velocity: " + characterRep.GetComponent<Rigidbody>().velocity);
        return new StateInfo(0, characterRep.transform.position, characterRep.transform.rotation, characterRep.GetComponent<Rigidbody>().velocity, characterRep.GetComponent<Rigidbody>().angularVelocity);
    }

    public StateInfo Simulate(Vector3 startPos, Quaternion StartRot, Vector3 startVelocity, Vector3 startAngularVelocity, byte moveKeysBitmask)
    {
        characterRep.transform.position = startPos;
        characterRep.transform.rotation = StartRot;
        characterRep.GetComponent<Rigidbody>().velocity = startVelocity;
        characterRep.GetComponent<Rigidbody>().angularVelocity = startAngularVelocity;

        characterRep.GetComponent<PlayerMove>().Move(moveKeysBitmask);
        physicsScene.Simulate(Time.fixedDeltaTime);

        return new StateInfo(0, characterRep.transform.position, characterRep.transform.rotation, characterRep.GetComponent<Rigidbody>().velocity, characterRep.GetComponent<Rigidbody>().angularVelocity);
    }

    // Start is called before the first frame update
    void Start()
    {
        CreatePhysicsScene();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

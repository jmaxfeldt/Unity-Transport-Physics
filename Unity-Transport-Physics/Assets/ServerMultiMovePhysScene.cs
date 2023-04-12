using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using NetMessages;

public class ServerMultiMovePhysScene : MonoBehaviour
{
    Scene multiMoveScene;
    PhysicsScene physicsScene;
    [SerializeField]
    Transform SceneGeometryParent;
    //GameObject characterRep;
    //Rigidbody charRepRB;


    void CreatePhysicsScene()
    {
        multiMoveScene = SceneManager.CreateScene("Server Multi Move Scene", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        physicsScene = multiMoveScene.GetPhysicsScene();

        foreach (Transform obj in SceneGeometryParent)
        {
            var sceneObj = Instantiate(obj.gameObject, obj.position, obj.rotation);
            sceneObj.GetComponent<Renderer>().enabled = false;
            SceneManager.MoveGameObjectToScene(sceneObj, multiMoveScene);
        }
    }

    public GameObject SpawnCharacterRep(GameObject charPrefab, Vector3 position, Quaternion rotation)
    {
        GameObject characterRep = Instantiate(charPrefab, position, rotation);
        characterRep.GetComponent<Renderer>().enabled = false;
        SceneManager.MoveGameObjectToScene(characterRep, multiMoveScene);

        if(characterRep != null)
        {
            return characterRep;
        }
        return null;
    }

    public StateInfo Simulate(Transform playerRep, Rigidbody playerRepRB, Vector3 startPos, Quaternion StartRot, Vector3 startVelocity, Vector3 startAngularVelocity, byte moveKeysBitmask)
    {
        playerRep.GetComponent<BoxCollider>().enabled = true;
        playerRep.position = startPos;
        playerRep.rotation = StartRot;
        playerRepRB.velocity = startVelocity;
        playerRepRB.angularVelocity = startAngularVelocity;

        playerRep.GetComponent<PlayerMove>().Move(moveKeysBitmask);
        physicsScene.Simulate(Time.fixedDeltaTime);
        playerRep.GetComponent<BoxCollider>().enabled = false;

        return new StateInfo(0, playerRep.position, playerRep.rotation, playerRepRB.velocity, playerRepRB.angularVelocity);
    }

    // Start is called before the first frame update
    void Start()
    {
        CreatePhysicsScene();
    }
}

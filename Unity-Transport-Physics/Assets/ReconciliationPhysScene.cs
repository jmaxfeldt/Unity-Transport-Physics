using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using NetMessages;

public class ReconciliationPhysScene : MonoBehaviour
{
    Scene reconciliationScene;
    PhysicsScene physicsScene;
    [SerializeField]
    Transform SceneGeometryParent;
    GameObject characterRep;
    Rigidbody charRepRB;


    void CreatePhysicsScene()
    {
        reconciliationScene = SceneManager.CreateScene("Reconciliation Scene", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        physicsScene = reconciliationScene.GetPhysicsScene();

        foreach (Transform obj in SceneGeometryParent)
        {
            var sceneObj = Instantiate(obj.gameObject, obj.position, obj.rotation);
            sceneObj.GetComponent<Renderer>().enabled = false;
            SceneManager.MoveGameObjectToScene(sceneObj, reconciliationScene);
        }
    }

    public void SpawnCharacterRep(GameObject charPrefab, Vector3 position, Quaternion rotation)
    {
        characterRep = Instantiate(charPrefab, position, rotation);
        if (characterRep != null)
        {
            characterRep.GetComponent<Renderer>().enabled = false;
            charRepRB = characterRep.GetComponent<Rigidbody>();
            SceneManager.MoveGameObjectToScene(characterRep, reconciliationScene);
        }
        else
        {
            Debug.LogError("Failed to spawn phsyics scene character representation.");
        }
    }

    public StateInfo Simulate(Vector3 startPos, Quaternion StartRot, Vector3 startVelocity, Vector3 startAngularVelocity, byte moveKeysBitmask)
    {
        //Debug.LogError("Simulating....");

        characterRep.transform.SetPositionAndRotation(startPos, StartRot);
        charRepRB.velocity = startVelocity;
        charRepRB.angularVelocity = startAngularVelocity;

        characterRep.GetComponent<PlayerMove>().Move(moveKeysBitmask);

        //Debug.LogError("Position before simulate: " + characterRep.transform.position);
        //Debug.LogError("Position before simulate move: " + characterRep.transform.position + " - Velocity: " + charRepRB.velocity);
        physicsScene.Simulate(Time.fixedDeltaTime);
        //Debug.LogError("Position after simulate move: " + characterRep.transform.position + " - Velocity: " + charRepRB.velocity);
        //Debug.LogError("Position after simulate: " + characterRep.transform.position);
        //Debug.LogError("Velocity after simulate: " + charRepRB.velocity);

        return new StateInfo(0, characterRep.transform.position, characterRep.transform.rotation, charRepRB.velocity, charRepRB.angularVelocity);
    }

    // Start is called before the first frame update
    void Start()
    {
        CreatePhysicsScene();
    }
}

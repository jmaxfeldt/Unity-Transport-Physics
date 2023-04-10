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
        characterRep.GetComponent<Renderer>().enabled = false;
        charRepRB = characterRep.GetComponent<Rigidbody>();
        SceneManager.MoveGameObjectToScene(characterRep, reconciliationScene);
    }

    public StateInfo Simulate(Vector3 startPos, Quaternion StartRot, Vector3 startVelocity, Vector3 startAngularVelocity, byte moveKeysBitmask)
    {
        characterRep.transform.position = startPos;
        characterRep.transform.rotation = StartRot;
        charRepRB.velocity = startVelocity;
        charRepRB.angularVelocity = startAngularVelocity;

        characterRep.GetComponent<PlayerMove>().Move(moveKeysBitmask);
        physicsScene.Simulate(Time.fixedDeltaTime);

        return new StateInfo(0, characterRep.transform.position, characterRep.transform.rotation, charRepRB.velocity, charRepRB.angularVelocity);
    }

    // Start is called before the first frame update
    void Start()
    {
        CreatePhysicsScene();
    }
}

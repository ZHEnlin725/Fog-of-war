using FOW.Core;
using FOW.FOV;
using UnityEngine;

public class Launch : MonoBehaviour
{
    private MapFOWRender mapFOWRender;

    // Start is called before the first frame update
    void Start()
    {
        FOWLogic.sharedInst.InitFOWSystem(new Vector3(512, 0, 512), 
            new Vector3(-256, 0, -256));
        mapFOWRender = new MapFOWRender();
        var o = new GameObject("FOV-Circle");
        var fov = o.AddComponent<TestFOV>();
        FOWLogic.sharedInst.AddFOV(fov);
        // Physics
    }


    // Update is called once per frame
    void Update()
    {
        FOWLogic.sharedInst.Update();
    }
}
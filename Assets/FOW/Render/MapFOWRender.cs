using FOW.Core;
using UnityEngine;

public class MapFOWRender
{
    public MapFOWRender()
    {
        var render = FOWLogic.sharedInst.CreateFOWRender();
        if (render != null)
        {
            var fCenterX = 0f;
            var fCenterZ = 0f;
            var scaleX = FOWSystem.sharedInst.worldSize.x / 128f * 2.56f;
            var scaleY = FOWSystem.sharedInst.worldSize.z / 128f * 2.56f;
            render.transform.position = new Vector3(fCenterX, 0f, -fCenterZ);
            render.transform.eulerAngles = new Vector3(-90f, 180f, 0f);
            render.transform.localScale = new Vector3(scaleX, scaleY, 1f);

            render.setFOWSystem(FOWSystem.sharedInst);
        }
    }
}
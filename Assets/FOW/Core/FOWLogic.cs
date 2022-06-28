using System.Collections.Generic;
using UnityEngine;

namespace FOW.Core
{
    public class FOWLogic : SingletonTemplate<FOWLogic>
    {
        private List<IFOV> FOVList = new List<IFOV>();

        private List<FOWRender> FOWRenders = new List<FOWRender>();

        public FOWRender CreateFOWRender()
        {
            FOWRender render = null;
            // TODO：实际项目中，从这里的资源管理类加载预设
            // 为了简单，这里直接从Resource加载
            var prefabs = Resources.Load<GameObject>("FOWRender");
            if (prefabs != null)
            {
                var mesh = Object.Instantiate(prefabs);
                if (mesh != null)
                {
                    render = mesh.gameObject.AddComponent<FOWRender>();
                }
            }

            if (render != null)
            {
                FOWRenders.Add(render);
            }

            return render;
        }

        public void InitFOWSystem(Vector3 worldSize, Vector3 worldOrigin, int textureWidth = 512,
            int textureHeight = 512)
        {
            FOWSystem.sharedInst.setWorld(worldSize, worldOrigin);
            FOWSystem.sharedInst.setTexture(textureWidth, textureHeight);
            FOWSystem.sharedInst.Init();
        }

        public void AddRender(FOWRender render)
        {
            FOWRenders.Add(render);
        }

        public void AddFOV(IFOV fov)
        {
            if (FOVList.Contains(fov)) return;
            FOVList.Add(fov);
            FOWSystem.sharedInst.Add(fov);
        }

        public void RemoveFOV(IFOV fov)
        {
            if (FOVList.Remove(fov))
                FOWSystem.sharedInst.Remove(fov);
        }

        public void Update()
        {
            for (int i = 0; i < FOVList.Count; i++)
            {
                var FOV = FOVList[i];
                FOV.UpdateVisible();
                if (!FOV.Visible())
                {
                    FOVList.RemoveAt(i--);
                    FOWSystem.sharedInst.Remove(FOV);
                }
            }

            foreach (var render in FOWRenders)
                render.setActive(FOWSystem.sharedInst.enableFog);
        }
    }
}
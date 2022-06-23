using FOW.Core;
using UnityEngine;

namespace FOW.FOV
{
    public class TestFOV : MonoBehaviour, IFOV
    {
        [SerializeField] private float radius = 15f;
        private Vector3 position;

        public void UpdateVisible() => position = transform.position;

        public bool Visible() => true;

        public float getRadius() => radius;

        public Vector3 getPosition() => position;
    }
}
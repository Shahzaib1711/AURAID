using UnityEngine;

namespace AURAID.UI
{
    public class XRUIBootstrap : MonoBehaviour
    {
        [Header("Place UI in front of camera on Play")]
        public float distance = 1.2f;
        public float height = 1.4f;
        public bool faceCamera = true;

        void Start()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            transform.position = cam.transform.position + forward * distance;
            transform.position = new Vector3(transform.position.x, height, transform.position.z);

            if (faceCamera)
            {
                var dir = transform.position - cam.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    }
}

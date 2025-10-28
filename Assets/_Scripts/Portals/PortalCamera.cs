using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RenderPipeline = UnityEngine.Rendering.RenderPipelineManager;

namespace _Scripts.Portals
{
    public class PortalCamera : MonoBehaviour
    {
        [SerializeField] 
        private Portal[] portals = new Portal[2];
        
        [SerializeField] 
        private Camera portalCamera;

        [SerializeField] 
        private int iterations = 7;
        
        private RenderTexture tempTexture1;
        private RenderTexture tempTexture2;

        private Camera mainCamera;
        
        private void Awake()
        {
            mainCamera = GetComponent<Camera>();
            tempTexture1 = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB64);
            tempTexture2 = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB64);
        }

        private void Start()
        {
            portals[0].Renderer.material.mainTexture = tempTexture1;
            portals[1].Renderer.material.mainTexture = tempTexture2;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += UpdateCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= UpdateCamera;
        }

        void UpdateCamera(ScriptableRenderContext src, Camera cam)
        {
            // Opcional: evita renders duplicados si hay más cámaras en la escena
            if (cam != mainCamera) return;

            if (!portals[0].IsPlaced || !portals[1].IsPlaced) return;

            if (portals[0].Renderer.isVisible)
            {
                portalCamera.targetTexture = tempTexture1;
                for (int i = iterations - 1; i >= 0; i--)
                {
                    RenderCamera(portals[0], portals[1], i, src);
                }
            }

            if (portals[1].Renderer.isVisible)
            {
                portalCamera.targetTexture = tempTexture2;
                for (int i = iterations - 1; i >= 0; i--)
                {
                    RenderCamera(portals[1], portals[0], i, src);
                }
            }
        }

        private void RenderCamera(Portal inPortal, Portal outPortal, int iterationID, ScriptableRenderContext SRC)
        {
            Transform inTransform = inPortal.transform;
            Transform outTransform = outPortal.transform;
            
            Transform cameraTransform = portalCamera.transform;
            cameraTransform.position = transform.position;
            cameraTransform.rotation = transform.rotation;
            
            for (int i = 0; i <= iterationID; i++)
            {
                Vector3 relativePos = inTransform.InverseTransformPoint(cameraTransform.position);
                relativePos = Quaternion.Euler(0f, 180f, 0f) * relativePos;
                cameraTransform.position = outTransform.TransformPoint(relativePos);

                Quaternion relativeRot = Quaternion.Inverse(inTransform.rotation) * cameraTransform.rotation;
                relativeRot = Quaternion.Euler(0f, 180f, 0f) * relativeRot;
                cameraTransform.rotation = outTransform.rotation * relativeRot;
            }
            
            Plane p = new Plane(-outTransform.forward, outTransform.position);
            Vector4 clipPlaneWorldSpace = new Vector4(p.normal.x, p.normal.y, p.normal.z, p.distance);
            Vector4 clipPlaneCameraSpace = Matrix4x4.Transpose(Matrix4x4.Inverse(portalCamera.worldToCameraMatrix)) * clipPlaneWorldSpace;
            
            var newMatrix = mainCamera.CalculateObliqueMatrix(clipPlaneCameraSpace);
            portalCamera.projectionMatrix = newMatrix;

            var request = new UniversalRenderPipeline.SingleCameraRequest();
            request.destination = portalCamera.targetTexture;

            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(portalCamera, request);
        }
    }
}   
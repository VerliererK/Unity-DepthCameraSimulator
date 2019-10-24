using UnityEngine;

[RequireComponent(typeof(Camera))]
public class DepthCamera : MonoBehaviour
{
    // PrimeSense: FOV = 57.5 x 45 (H x V, degree)
    // FOV in Unity is Vertical FOV, so set camera's fov = 45
    // Target Texture = 640 x 480, aspect ratio is 1.33, it means Horizontal FOV is 60

    private Camera m_Camera;
    private Material m_ptsMaterial;
    private ComputeBuffer m_PointCloudBuffer;
    private Vector3[] m_PointCloud;

    public Color m_pointTint = new Color(1, 1, 1, 1);

    private void Start()
    {
        m_Camera = GetComponent<Camera>();
    }

    private void OnDestroy()
    {
        if (m_ptsMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(m_ptsMaterial);
            else
                DestroyImmediate(m_ptsMaterial);
        }
        if (m_PointCloudBuffer != null)
        {
            m_PointCloudBuffer.Release();
            m_PointCloudBuffer.Dispose();
            m_PointCloudBuffer = null;
        }
    }

    private void OnRenderObject()
    {
        // Check the camera condition.
        if (m_Camera == null || (m_Camera.cullingMask & (1 << gameObject.layer)) == 0) return;
        if (m_Camera.activeTexture == null) return;

        // Material initial
        if (m_ptsMaterial == null)
        {
            m_ptsMaterial = new Material(Shader.Find("Hidden/CameraDepthToPtCloud"));
            m_ptsMaterial.hideFlags = HideFlags.DontSave;
        }

        m_ptsMaterial.SetPass(0);
        m_ptsMaterial.SetColor("_Tint", m_pointTint);
        m_ptsMaterial.SetTexture("_MainTex", m_Camera.activeTexture);
        // Let Shader Transform to World Coordinate
        m_ptsMaterial.SetMatrix("_CameraInverseVP", (m_Camera.projectionMatrix * m_Camera.worldToCameraMatrix).inverse);

        int ptsCount = m_Camera.activeTexture.width * m_Camera.activeTexture.height;

        if (m_PointCloudBuffer == null)
        {
            m_PointCloudBuffer = new ComputeBuffer(ptsCount, sizeof(float) * 3, ComputeBufferType.Default);
            m_PointCloud = new Vector3[ptsCount];
        }

        m_ptsMaterial.SetBuffer("_PointCloudBuffer", m_PointCloudBuffer);
        Graphics.SetRandomWriteTarget(1, m_PointCloudBuffer, false);

#if UNITY_2019
        Graphics.DrawProceduralNow(MeshTopology.Points, ptsCount, 1);
#else
        Graphics.DrawProcedural(MeshTopology.Points, ptsCount, 1);
#endif
    }

    private void OnPostRender()
    {
        // Not sure why need Blit to temp, otherwise there are something wrong during moving the model with multiple camera
        var tmp = RenderTexture.GetTemporary(m_Camera.activeTexture.descriptor);
        Graphics.Blit(m_Camera.targetTexture, tmp);
        RenderTexture.ReleaseTemporary(tmp);
    }

    private void OnDrawGizmosSelected()
    {
        var pts = GetPointCloud();
        if (pts == null) return;

        int width = m_Camera.targetTexture.width;
        int height = m_Camera.targetTexture.height;

        // Prevent CPU bound
        for (int j = 0; j < height; j += 10)
            for (int i = 0; i < width; i += 10)
            {
                int index = j * width + i;
                if (index >= pts.Length) continue;

                Vector3 p = pts[index];

                if (p == Vector3.zero) continue;

                Gizmos.color = m_pointTint;
                Gizmos.DrawSphere(p, 0.03f);
            }
    }

    public Vector3[] GetPointCloud()
    {
        if (m_PointCloudBuffer == null) return null;

        m_PointCloudBuffer.GetData(m_PointCloud);
        return m_PointCloud;
    }

}

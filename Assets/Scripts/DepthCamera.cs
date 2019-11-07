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

    private Texture2D m_Depth;

    private void CopyTargetTexture()
    {
        var tmp = RenderTexture.GetTemporary(m_Camera.targetTexture.width, m_Camera.targetTexture.height);
        Graphics.Blit(m_Camera.targetTexture, tmp);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = tmp;

        if (m_Depth == null)
            m_Depth = new Texture2D(tmp.width, tmp.height, TextureFormat.RGBAFloat, false);
        m_Depth.ReadPixels(new Rect(0, 0, m_Depth.width, m_Depth.height), 0, 0);
        m_Depth.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(tmp);
    }

    // Why Not Correct ??
    public Vector3[] GetPointCloud_CPU()
    {
        CopyTargetTexture();

        if (!m_Depth) return null;

        Vector3[] pointCloud = new Vector3[m_Depth.height * m_Depth.width];

        for (int j = 0; j < m_Depth.height; j++)
            for (int i = 0; i < m_Depth.width; i++)
            {
                float z = m_Depth.GetPixel(i, j).r;
                if (z == 1 || z == 0) continue;

                // Important!! if UNITY_REVERSED_Z
                z = 1 - z;

                Vector3 camPos = m_Camera.transform.position;
                Vector3 camFar = m_Camera.ScreenToWorldPoint(new Vector3(i, j, m_Camera.farClipPlane));
                Vector3 p = Vector3.Lerp(camPos, camFar, Linear01Depth(z, GetZBufferParams(m_Camera)));

                pointCloud[j * m_Depth.width + i] = p;
            }
        return pointCloud;
    }

    public Vector3[] GetNormals_CPU()
    {
        CopyTargetTexture();

        if (!m_Depth) return null;

        Vector3[] normals = new Vector3[m_Depth.height * m_Depth.width];

        for (int j = 1; j < m_Depth.height - 1; j++)
            for (int i = 1; i < m_Depth.width - 1; i++)
            {
                //dzdx = (z(x + 1, y) - z(x - 1, y)) / 2.0;
                //dzdy = (z(x, y + 1) - z(x, y - 1)) / 2.0;
                //direction = (-dzdx, -dzdy, 1.0)
                //magnitude = sqrt(direction.x * *2 + direction.y * *2 + direction.z * *2)
                //normal = direction / magnitude

                float dzdx = (m_Depth.GetPixel(i - 1, j).r - m_Depth.GetPixel(i + 1, j).r) / 2.0f;
                float dzdy = (m_Depth.GetPixel(i, j - 1).r - m_Depth.GetPixel(i, j + 1).r) / 2.0f;
                Vector3 normal = new Vector3(-dzdx, -dzdy, 1.0f);
                normal.Normalize();
                normals[j * m_Depth.width + i] = m_Camera.transform.localRotation * normal * -1;
            }
        return normals;
    }

    #region Camera Function

    // Z buffer to linear 0..1 depth (0 at eye, 1 at far plane)
    public static Vector4 GetZBufferParams(Camera cam)
    {
        float zc0, zc1;
        // OpenGL would be this:
        //zc0 = (1.0f - m_Camera.farClipPlane / m_Camera.nearClipPlane) / 2.0f;
        //zc1 = (1.0f + m_Camera.farClipPlane / m_Camera.nearClipPlane) / 2.0f;
        // D3D is this:
        zc0 = 1.0f - cam.farClipPlane / cam.nearClipPlane;
        zc1 = cam.farClipPlane / cam.nearClipPlane;
        return new Vector4(zc0, zc1, zc0 / cam.farClipPlane, zc1 / cam.farClipPlane);
    }

    // Z buffer to linear 0..1 depth (0 at eye, 1 at far plane)
    public static float Linear01Depth(float z, Vector4 zBufferParams)
    {
        return 1.0f / (zBufferParams.x * z + zBufferParams.y);
    }

    // Z buffer to linear depth
    public static float LinearEyeDepth(float z, Vector4 zBufferParams)
    {
        return 1.0f / (zBufferParams.z * z + zBufferParams.w);
    }

    #endregion
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PLYExporter : MonoBehaviour
{
    public string PLYFileName;
    public List<DepthCamera> Sensors;

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.F5))
            ExportPointCloudToPly(PLYFileName);
    }

    public void ExportPointCloudToPly(string filePath)
    {
        List<float[]> ply = new List<float[]>();

        for (int i = 0; i < Sensors.Count; i++)
        {
            if (!Sensors[i]) continue;
            var pts = Sensors[i].GetPointCloud();

            for (int j = 0; j < pts.Length; j++)
            {
                if (pts[j] == Vector3.zero)
                    continue;

                ply.Add(new float[] { pts[j].x, pts[j].y, pts[j].z });
            }
        }
        SavePly(filePath, ply);
    }

    public void SavePly(string filePath, IEnumerable<float[]> ply)
    {
        using (var file = System.IO.File.CreateText(filePath))
        {
            file.WriteLine("ply");
            file.WriteLine("format ascii 1.0");
            file.WriteLine("comment Unity-DepthCamera generated");
            file.WriteLine("element vertex " + ply.Count());
            file.WriteLine("property float x");
            file.WriteLine("property float y");
            file.WriteLine("property float z");

            if (ply.Count() > 0 && ply.ElementAt(0).Length == 6)
            {
                file.WriteLine("property float nx");
                file.WriteLine("property float ny");
                file.WriteLine("property float nz");
            }

            file.WriteLine("element face 0");
            file.WriteLine("property list uchar int vertex_indices");
            file.WriteLine("end_header");

            foreach (var p in ply)
            {
                file.WriteLine(string.Join(" ", p));
            }
        }

        Debug.Log("Save to " + filePath + " (vertices = " + ply.Count() + ")");
    }
}
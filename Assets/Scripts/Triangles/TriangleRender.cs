using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class TriangleRender : MonoBehaviour
{
    public Mesh Mesh;
    public Material Material;

    ComputeBuffer meshTriangles;
    ComputeBuffer meshPositions;

    Bounds bounds;
    private void Start()
    {
        bounds = new Bounds(Vector3.zero, Vector3.one * 20);
        int[] triangles = Mesh.triangles;
        meshTriangles = new ComputeBuffer(triangles.Length, sizeof(int));
        meshTriangles.SetData(triangles);
        Vector3[] positions = Mesh.vertices;
        meshPositions = new ComputeBuffer(positions.Length, sizeof(float) * 3);
        meshPositions.SetData(positions);

        //Material.SetBuffer("SphereLocations", resultBuffer);
        Material.SetBuffer("Triangles", meshTriangles);
        Material.SetBuffer("Positions", meshPositions);
    }

    private void OnEnable()
    {
        RenderPipelineManager.endCameraRendering += RenderPipelineManagerOnendCameraRendering;
    }
    
    private void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= RenderPipelineManagerOnendCameraRendering;
    }

    private void RenderPipelineManagerOnendCameraRendering(ScriptableRenderContext context, Camera cam)
    {
      // Graphics.DrawProcedural(Material, bounds, MeshTopology.Triangles, meshTriangles.count, SphereAmount);
    }

    void OnDestroy()
    {
      //  resultBuffer.Dispose();
        meshTriangles.Dispose();
        meshPositions.Dispose();
    }
}

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

public readonly struct RenderInfo {
    private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");
    public readonly Mesh mesh;
    public readonly Material mat;

    public RenderInfo(Mesh m, Material material) {
        mesh = m;
        mat = material;
    }

    public static RenderInfo FromSprite(Material baseMaterial, Sprite s) {
        var renderMaterial = UnityEngine.Object.Instantiate(baseMaterial);
        renderMaterial.enableInstancing = true;
        renderMaterial.SetTexture(MainTexPropertyId, s.texture);
        Mesh m = new Mesh {
            vertices = s.vertices.Select(v => (Vector3)v).ToArray(),
            triangles = s.triangles.Select(t => (int)t).ToArray(),
            uv = s.uv
        };
        return new RenderInfo(m, renderMaterial);
    }
}

public class ManyObjectHolder : MonoBehaviour {
    private class FObject {
        private static readonly Random r = new Random();
        public Vector2 position;
        public readonly float scale;
        private readonly Vector2 velocity;
        public float rotation;
        private readonly float rotationRate;
        public float time;

        public FObject() {
            position = new Vector2((float)r.NextDouble() * 10f - 5f, (float)r.NextDouble() * 8f - 4f);
            velocity = new Vector2((float)r.NextDouble() * 0.4f - 0.2f, (float)r.NextDouble() * 0.4f - 0.2f); 
            rotation = (float)r.NextDouble();
            rotationRate = (float)r.NextDouble() * 0.6f - 0.2f;
            scale = 0.6f + (float) r.NextDouble() * 0.8f;
            time = (float) r.NextDouble() * 6f;
        }

        public void DoUpdate(float dT) {
            position += velocity * dT;
            rotation += rotationRate * dT;
            time += dT;
        }
    }
    private static readonly int positionPropertyId = Shader.PropertyToID("positionBuffer");
    private static readonly int directionPropertyId = Shader.PropertyToID("directionBuffer");
    private static readonly int timePropertyId = Shader.PropertyToID("timeBuffer");
    
    private MaterialPropertyBlock pb;
    private static readonly ComputeBufferPool fCBP = new ComputeBufferPool(batchSize, 4, ComputeBufferType.Default);
    private static readonly ComputeBufferPool v2CBP = new ComputeBufferPool(batchSize, 8, ComputeBufferType.Default);
    private static readonly ComputeBufferPool argsCBP = new ComputeBufferPool(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
    private readonly Vector2[] posArr = new Vector2[batchSize];
    private readonly Vector2[] dirArr = new Vector2[batchSize];
    private readonly float[] timeArr = new float[batchSize];
    private readonly uint[] args = new uint[] { 0, 0, 0, 0, 0 };
    private const int batchSize = 7;
    public int instanceCount;
    
    public Sprite sprite;
    public Material baseMaterial;
    private RenderInfo ri;
    public string layerRenderName;
    private int layerRender;
    private FObject[] objects;
    
    private void Start() {
        pb = new MaterialPropertyBlock();
        layerRender = LayerMask.NameToLayer(layerRenderName);
        ri = RenderInfo.FromSprite(baseMaterial, sprite);
        Camera.onPreCull += RenderMe;
        objects = new FObject[instanceCount];
        for (int ii = 0; ii < instanceCount; ++ii) {
            objects[ii] = new FObject();
        }
    }

    private void Update() {
        float dT = Time.deltaTime;
        for (int ii = 0; ii < instanceCount; ++ii) {
            objects[ii].DoUpdate(dT);
        }
    }

    private void RenderMe(Camera c) {
        if (!Application.isPlaying) { return; }
        fCBP.Flush();
        v2CBP.Flush();
        argsCBP.Flush();
        args[0] = ri.mesh.GetIndexCount(0);
        for (int done = 0; done < instanceCount; done += batchSize) {
            int run = Math.Min(instanceCount - done, batchSize);
            args[1] = (uint)run;
            for (int batchInd = 0; batchInd < run; ++batchInd) {
                var obj = objects[done + batchInd];
                posArr[batchInd] = obj.position;
                dirArr[batchInd] = new Vector2(Mathf.Cos(obj.rotation) * obj.scale, Mathf.Sin(obj.rotation) * obj.scale);
                timeArr[batchInd] = obj.time;
            }
            var posCB = v2CBP.Rent();
            var dirCB = v2CBP.Rent();
            var timeCB = fCBP.Rent();
            posCB.SetData(posArr, 0, 0, run);
            dirCB.SetData(dirArr, 0, 0, run);
            timeCB.SetData(timeArr, 0, 0, run);
            pb.SetBuffer(positionPropertyId, posCB);
            pb.SetBuffer(directionPropertyId, dirCB);
            pb.SetBuffer(timePropertyId, timeCB);
            var argsCB = argsCBP.Rent();
            argsCB.SetData(args);
            CallRender(c, argsCB);
        }
    }
    
    
    private void CallRender(Camera c, ComputeBuffer argsBuffer) {
        Graphics.DrawMeshInstancedIndirect(ri.mesh, 0, ri.mat,
            bounds: new Bounds(Vector3.zero, Vector3.one * 1000f),
            bufferWithArgs: argsBuffer,
            argsOffset: 0,
            properties: pb,
            castShadows: ShadowCastingMode.Off,
            receiveShadows: false,
            layer: layerRender,
            camera: c);
    }

    private void OnDestroy() {
        Debug.Log("Cleaning up compute buffers");
        fCBP.Dispose();
        v2CBP.Dispose();
        argsCBP.Dispose();
    }
}
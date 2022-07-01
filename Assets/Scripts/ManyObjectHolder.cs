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
    private static readonly int posDirPropertyId = Shader.PropertyToID("posDirBuffer");
    private static readonly int timePropertyId = Shader.PropertyToID("timeBuffer");
    
    private MaterialPropertyBlock pb;
    private readonly Vector4[] posDirArr = new Vector4[batchSize];
    private readonly float[] timeArr = new float[batchSize];
    private readonly Matrix4x4[] posMatrixArr = new Matrix4x4[batchSize];
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

    private float Smoothstep(float low, float high, float t) {
        t = Mathf.Clamp01((t - low) / (high - low));
        return t * t * (3 - 2 * t);
    }
    private void RenderMe(Camera c) {
        if (!Application.isPlaying) { return; }
        for (int done = 0; done < instanceCount; done += batchSize) {
            int run = Math.Min(instanceCount - done, batchSize);
            for (int batchInd = 0; batchInd < run; ++batchInd) {
                var obj = objects[done + batchInd];
                posDirArr[batchInd] = new Vector4(obj.position.x, obj.position.y, 
                    Mathf.Cos(obj.rotation) * obj.scale, Mathf.Sin(obj.rotation) * obj.scale);
                timeArr[batchInd] = obj.time;
                ref var m = ref posMatrixArr[batchInd];

                var scale = obj.scale * Smoothstep(0, 10, obj.time);
                m.m00 = m.m11 = Mathf.Cos(obj.rotation) * scale;
                m.m01 = -(m.m10 = Mathf.Sin(obj.rotation) * scale);
                m.m22 = m.m33 = 1;
                m.m03 = obj.position.x;
                m.m13 = obj.position.y;
            }
            pb.SetVectorArray(posDirPropertyId, posDirArr);
            pb.SetFloatArray(timePropertyId, timeArr);
            //CallRender(c, run);
            CallLegacyRender(c, run);
        }
    }
    
    
    private void CallRender(Camera c, int count) {
        Graphics.DrawMeshInstancedProcedural(ri.mesh, 0, ri.mat,
            bounds: new Bounds(Vector3.zero, Vector3.one * 1000f),
            count: count,
            properties: pb,
            castShadows: ShadowCastingMode.Off,
            receiveShadows: false,
            layer: layerRender,
            camera: c);
    }

    //Use this for legacy GPU support or WebGL support
    private void CallLegacyRender(Camera c, int count) {
        Graphics.DrawMeshInstanced(ri.mesh, 0, ri.mat,
            posMatrixArr,
            count: count,
            properties: pb,
            castShadows: ShadowCastingMode.Off,
            receiveShadows: false,
            layer: layerRender,
            camera: c);
    }
}
using UnityEngine;
using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using System.IO;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
#endif

namespace Arts.TA.VoxelPointLight
{
    [ExecuteAlways]
    public class VoxelPointLightMgr : MonoBehaviour
    {
        public enum TexSizeEnum
        {
            Small, //256*256 表达4层 128*128的格子
            Mid, //512*512 表达4层 256*256的格子
            Large //1024*1024 表达4层 512*512的格子
        }
        
        private Dictionary<TexSizeEnum, int> SizeEnum2Size = new Dictionary<TexSizeEnum, int>()
        {
            {TexSizeEnum.Small, 256},
            {TexSizeEnum.Mid, 512},
            {TexSizeEnum.Large, 1024},
        };
        
        [HideInInspector] public Vector4[] PosAndRangeArray = new Vector4[256];
        [HideInInspector] public Vector4[] ColorArray = new Vector4[256];
        

        public int Priority = 0;
        [Range(0.1f, 5f)] public float GridSize = 1;
        [Range(0.5f, 30f)]public float VolumeHeight = 30;
        public Texture2D IdxTex; 
        public TexSizeEnum TexSize;

        public static VoxelPointLightMgr CurVoxelPointLightMgr = null;

        public Vector3 VolumeSize {
            get
            {
                var size = GridSize * SizeEnum2Size[TexSize] / 2;
                return new Vector3(size, VolumeHeight*2, size);
            }
        }
        
        private static int POS_RANGE_ARRAY_ID = Shader.PropertyToID("_TOPointPosRange");
        private static int COLOR_ARRAY_ID = Shader.PropertyToID("_TOPointColor");
        private static int CENTER_POS_ID = Shader.PropertyToID("_TPVoxelCenter");
        private static int SIZE_ID = Shader.PropertyToID("_TPVoxelSize");
        private static int VOXEL_MAP_ID = Shader.PropertyToID("_VoxelIdxMap");
        private static int TOGGLE_ID = Shader.PropertyToID("_IfEnableVoxelPointLights");

        private void Update()
        {
            if (CurVoxelPointLightMgr == null)
            {
                CurVoxelPointLightMgr = this;
            }
            else if(CurVoxelPointLightMgr.Priority < this.Priority)
            {
                CurVoxelPointLightMgr = this;
            }

            // 实际上不应该这里调用，应该渲染时调用
            SetUpVoxelPointLights();
        }

        public void OnDisable()
        {
            Shader.SetGlobalFloat(TOGGLE_ID, 0);
        }

        public static void SetUpVoxelPointLights()
        {
            if (CurVoxelPointLightMgr != null)
            {
                Shader.SetGlobalFloat(TOGGLE_ID, 1);
                CurVoxelPointLightMgr.SetLightingArray();
            }
            else
            {
                Shader.SetGlobalFloat(TOGGLE_ID, 0);
            }
        }

        private void SetLightingArray()
        {
#if UNITY_EDITOR
            RefreshLightingArray();
#endif
            Shader.SetGlobalVectorArray(POS_RANGE_ARRAY_ID, PosAndRangeArray);
            Shader.SetGlobalVectorArray(COLOR_ARRAY_ID, ColorArray);
            Shader.SetGlobalVector(CENTER_POS_ID, transform.position);
            Shader.SetGlobalVector(SIZE_ID, new Vector4(VolumeSize.x, VolumeSize.y, VolumeSize.z, GridSize));
            Shader.SetGlobalTexture(VOXEL_MAP_ID, IdxTex);
        }
        
// Editor工具
#if UNITY_EDITOR
        public int GridCount
        {
            get
            {
                return SizeEnum2Size[TexSize];
            }
        }

        public List<VoxelPointLight> Lights;
        
        private BoxBoundsHandle BoxBoundsHandle = new BoxBoundsHandle();
        private int[] tempInts = new int[8];
        
        private bool IsRunning()
        {
            return CurVoxelPointLightMgr == this;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.DrawCube(transform.position, VolumeSize);
            Gizmos.color = new Color(1, 1, 1, 1);
            Gizmos.DrawWireCube(transform.position, VolumeSize);

            // var halfGridCount = GridCount / 2;
            //
            // for (int hLayer = 0; hLayer < 4; hLayer++)
            // {
            //
            //     var colliders = new Collider[300];
            //     var collidersLen = 0;
            //     var sortedLights = new List<VoxelPointLight>();
            //
            //     var gridFix = new Vector3(GridSize, 0, GridSize) * 0.5f;
            //     var checkBoxSize = new Vector3(GridSize, VolumeHeight * 0.5f, GridSize) * 0.5f;
            //     var checkBoxYOffset = (hLayer - 1.5f) * 0.5f * VolumeHeight;
            //
            //     for (int i = 0; i < halfGridCount; i++)
            //     {
            //         for (int j = 0; j < halfGridCount; j++)
            //         {
            //             sortedLights.Clear();
            //
            //             var curBoxCenter = transform.position +
            //                                new Vector3((i - halfGridCount / 2) * GridSize, checkBoxYOffset, (j - halfGridCount / 2) * GridSize) + gridFix;
            //
            //             Gizmos.DrawWireCube(curBoxCenter, checkBoxSize*2);
            //
            //         }
            //     }
            // }
        }

        //预计算衰减
        private Vector2 GetAttenParams(VoxelPointLight l)
        {
            Vector2 lightAttenuation = Vector2.zero;
            float lightRangeSqr = l.Range * l.Range;
            float fadeStartDistanceSqr = 0.8f * 0.8f * lightRangeSqr;
            float fadeRangeSqr = (fadeStartDistanceSqr - lightRangeSqr);
            float oneOverLightRangeSqr = 1.0f / Mathf.Max(0.0001f, l.Range * l.Range);

            float lightRangeSqrOverFadeRangeSqr = -lightRangeSqr / fadeRangeSqr;

            lightAttenuation.x = oneOverLightRangeSqr;
            lightAttenuation.y = lightRangeSqrOverFadeRangeSqr;

            return lightAttenuation;
        }

        private void RefreshLightingArray()
        {
            var posRangeArray = new Vector4[256];
            var colorArray = new Vector4[256];

            if (Lights == null) return;
            
            Lights = Lights.FindAll(l => l != null && l.isActiveAndEnabled);
            
            Lights.Sort((a, b) => { return a.Number - b.Number; });

            for (int i = 0; i < Lights.Count; i++)
            {
                var pos = Lights[i].transform.position;
                var atten = GetAttenParams(Lights[i]);
                var color = Lights[i].LightColor;
                posRangeArray[Lights[i].Number] = new Vector4(pos.x, pos.y, pos.z, atten.x);
                colorArray[Lights[i].Number] = new Vector4(color.r, color.g, color.b, atten.y) * Lights[i].Strength;
            }

            ColorArray = colorArray;
            PosAndRangeArray = posRangeArray;
        }

        // [Button("新建体素灯")]
        private void Create()
        {
            var obj = new GameObject("VoxelLight-" + gameObject.name + Lights.Count);
            var l = obj.AddComponent<VoxelPointLight>();

            var trans = SceneView.lastActiveSceneView.camera.transform;
            obj.transform.SetParent(gameObject.transform);
            obj.transform.position = trans.position + trans.forward * 5;
            Selection.activeObject = obj;
            Lights.Add(l);
            ClearNonMgrVoexlPointLight();
        }
        
        // [Button("生成静态灯光")]
        public void Bake()
        {
            CreateOrLoadTex(ref IdxTex);
            
            var lights = FindObjectsOfType<VoxelPointLight>();
            int layer = 0;
            var idx = 0;

            foreach (var l in lights)
            {
                l.Number = idx++;
                var c = l.gameObject.GetComponent<SphereCollider>();
                if (c == null)
                {
                    c = l.gameObject.AddComponent<SphereCollider>();
                }

                c.center = Vector3.zero;

                c.radius = l.isSelected ? Math.Max(l.Range * 5, 50) : l.Range;
                layer |= c.gameObject.layer;
            }

            RefreshLightingArray();

            var halfGridCount = GridCount / 2;

            for (int hLayer = 0; hLayer < 4; hLayer++)
            {
                
                var colliders = new Collider[300];
                var collidersLen = 0;
                var sortedLights = new List<VoxelPointLight>();
                
                var gridFix = new Vector3(GridSize, 0, GridSize) * 0.5f;
                var checkBoxSize = new Vector3(GridSize, VolumeHeight * 0.5f, GridSize) * 0.5f;
                var checkBoxYOffset = (hLayer - 1.5f) * 0.5f * VolumeHeight;
                
                for (int i = 0; i < halfGridCount; i++)
                {
                    for (int j = 0; j < halfGridCount; j++)
                    {
                        sortedLights.Clear();
                        
                        var curBoxCenter =  transform.position + new Vector3((i - halfGridCount/2) * GridSize, checkBoxYOffset, (j - halfGridCount/2) * GridSize) + gridFix;
                        
                        // 检测范围稍微提高点
                        collidersLen = Physics.OverlapBoxNonAlloc(curBoxCenter, checkBoxSize, colliders);

                        for (int k = 0; k < collidersLen; k++)
                        {
                            var l = colliders[k].GetComponent<VoxelPointLight>();
                            if (l != null)
                            {
                                sortedLights.Add(l);
                            }
                        }

                        // todo 灯光重要性排序 选出最亮的几个
                        sortedLights.Sort((a, b) =>
                        {
                            var distA = (a.transform.position - curBoxCenter).magnitude;
                            var distB = (b.transform.position - curBoxCenter).magnitude;
                            var attenA = (a.Range - distA) / a.Range;
                            var attenB = (b.Range - distB) / b.Range;
                        
                            var dt = a.Strength * attenA - b.Strength * attenB;
                            if (dt > 0)
                            {
                                return 1;
                            }
                            else 
                            {
                                return -1;
                            }
                        });
                        // TestLight(hLayer, IdxTex, i + (hLayer / 2) * halfGridCount, j + (hLayer % 2) * halfGridCount);
                        EncodeLight(sortedLights, IdxTex, i + (hLayer / 2) * halfGridCount, j + (hLayer % 2) * halfGridCount);
                    }
                }
                
            }

            IdxTex.Apply();

            foreach (var l in lights)
            {
                var c = l.gameObject.GetComponent<SphereCollider>();
                l.BakedPos = l.transform.position;
                l.BakedRange = l.Range;
                DestroyImmediate(c);
            }
        }

        private void TestLight(int layer, Texture2D tex, int x, int z)
        {
            tex.SetPixel(x, z, Color.white * layer/4.0f);
        }
        private void EncodeLight(List<VoxelPointLight> lights, Texture2D tex, int x, int z)
        {
            // 每个灯光编号需要占用8位  
            // 分4层 灯光上限是 7
            // RGBA64的格式 1024分辨率
            
            // R的 16位 2层1号 2层2号   // R的 16位 3层1号 3层2号
            // G的 16位 2层3号 2层4号   // G的 16位 3层3号 3层4号
            // B的 16位 2层5号 2层6号   // B的 16位 3层5号 3层6号
            // A的 16位 2层7号 2层灯数  // A的 16位 3层7号 3层灯数

            // R的 16位 0层1号 0层2号   // R的 16位 1层1号 1层2号 
            // G的 16位 0层3号 0层4号   // G的 16位 1层3号 1层4号
            // B的 16位 0层5号 0层6号   // B的 16位 1层5号 1层6号
            // A的 16位 0层7号 0层灯数  // A的 16位 1层7号 1层灯数

            var count = lights.Count;
            for (int i = 0; i < 8; i++)
            {
                tempInts[i] = i < count ? lights[i].Number : 0;
            }

            var r = Encode8BitTo16Bit(tempInts[0], tempInts[1]);
            var g = Encode8BitTo16Bit(tempInts[2], tempInts[3]);
            var b = Encode8BitTo16Bit(tempInts[4], tempInts[5]);
            var a = Encode8BitTo16Bit(tempInts[6], count);
            var color = new Color(r, g, b, a);
            // if (count > 0)
            // {
            //     if (count == 2)
            //     {
            //         var tes = 0;
            //     }
            //     var str = "";
            //     for (int i = 0; i < lights.Count; i++)
            //     {
            //         str += $"第{i}个灯编号为{lights[i].Number} \n";
            //     }
            //     Debug.Log($"Box{x} {z} 内有 {count} 个灯影响，\n" +
            //               str +
            //               $"写入 {color.r*65536} {color.g*65536} {color.b*65536} {color.a*65536}");
            // }
            tex.SetPixel(x, z, color);
        }

        private float Encode8BitTo16Bit(int idx1, int idx2)
        {
            var sum = idx1 + idx2 * 256f;
            return sum / 65536f;
        }

        public static string GetAssetPath()
        {

            var scene = EditorSceneManager.GetActiveScene();
            var path = $"{Path.GetDirectoryName(scene.path)}/{scene.name}";

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }

        private void CreateOrLoadTex(ref Texture2D tex)
        {
            var path = GetAssetPath() + "/VoxelPointInfo.asset";
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null || tex.texelSize.x != GridCount)
            {
                tex = new Texture2D(GridCount, GridCount, TextureFormat.RGBAFloat, false);
                tex.anisoLevel = 0;
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                AssetDatabase.CreateAsset(tex, path);
            }
        }

        public void ClearNonMgrVoexlPointLight()
        {
            var vps = FindObjectsOfType<VoxelPointLight>();
            foreach (var vp in vps)
            {
                if (!Lights.Contains(vp))
                {
                    if (vp.transform.IsChildOf(transform))
                    {
                        Lights.Add(vp);
                    }
                    else
                    {
                        DestroyImmediate(vp);
                    }
                }
            }
        }
        
#endif
    }
}
using UnityEditor;
#if UNITY_EDITOR
using System.Linq;
using UnityEditor.IMGUI.Controls;
#endif

using UnityEngine;

namespace Arts.TA.VoxelPointLight
{
    [ExecuteAlways]
    public class VoxelPointLight : MonoBehaviour
    {
        public Color LightColor = Color.white;
        public float Range = 5;
        public float Strength = 1;
        
        [HideInInspector]
        public int Number;
        
#if UNITY_EDITOR
        [HideInInspector]
        public bool isSelected;
        [HideInInspector]
        public bool isEditing;
        
        [HideInInspector]
        public float BakedRange;
        [HideInInspector]
        public Vector3 BakedPos;

        public bool IsDirty
        {
            get
            {
                return (BakedRange != Range || BakedPos != transform.position);
            }
        }
        
        private SphereBoundsHandle sphereBoundsHandle = new SphereBoundsHandle();
        
        private void OnSelectChange()
        {
            if (isSelected)
            {
                SceneView.duringSceneGui += DrawRange;
            }
            else
            {
                isEditing = false;
                SceneView.duringSceneGui -= DrawRange;
            }
            if (IsDirty)
            {
                Bake();
            }
        }

        private void DrawRange(SceneView sceneview)
        {
            if (this == null)
            {
                SceneView.duringSceneGui -= DrawRange;
                return;
            }

            sphereBoundsHandle.center = transform.position;
            sphereBoundsHandle.radius = Range;
            sphereBoundsHandle.wireframeColor = LightColor;
            sphereBoundsHandle.DrawHandle();
            Range = sphereBoundsHandle.radius;
            
            var edited = sphereBoundsHandle.center != BakedPos || sphereBoundsHandle.radius != BakedRange;
            if (edited && !isEditing)
            {
                isEditing = true;
                Bake();
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.DrawIcon(transform.position,  "Packages/com.unity.render-pipelines.universal/Runtime/Custom/VoxelPointLights/Editor/Gizmos/VoxelLight.png", false, this.LightColor);

            if (Selection.objects.Contains(this.gameObject))
            {
                Gizmos.color = this.LightColor;
                Gizmos.DrawWireSphere(transform.position, Range);
            }
        }

        private void Update()
        {
            var selected = Selection.activeObject == this.gameObject;
            if(isSelected != selected) 
            {
                isSelected = selected;
                OnSelectChange();
            }
        }

        private void OnEnable()
        {
            if (transform.parent)
            {
                var p = transform.parent.GetComponent<VoxelPointLightMgr>();
                if (p != null)
                {
                    p.ClearNonMgrVoexlPointLight();
                }
            }
        }

        private void Bake()
        {
            FindObjectOfType<VoxelPointLightMgr>().Bake();
        }
#endif
    }
}
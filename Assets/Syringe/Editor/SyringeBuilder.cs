using UnityEngine;
using UnityEditor;
public static class SyringeBuilder {
    const string Root="Assets/Syringe/";
    [MenuItem("Tools/Dead Signal/Build Syringe Prefab")]
    public static void Build() {
        string[] names={"Barrel","Hub","Needle","Plunger","Piston","Liquid"};
        Color[] colors={new Color(.65f,.72f,.68f,.28f),new Color(.34f,.43f,.39f),new Color(.55f,.59f,.61f),new Color(.67f,.68f,.60f),new Color(.12f,.14f,.13f),new Color(.38f,.48f,.19f)};
        GameObject root=new GameObject("Syringe");
        try {
            var anim=root.AddComponent<SyringeAnimation>(); root.AddComponent<SyringePickup>();
            bool urp=Shader.Find("Universal Render Pipeline/Lit")!=null;
            var shader=Shader.Find(urp?"Universal Render Pipeline/Lit":"Standard");
            if(!shader) throw new System.Exception("Compatible Lit shader not found.");
            for(int i=0;i<names.Length;i++) {
                string name=names[i], path=Root+"Models/"+name+".obj";
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(!model) throw new System.Exception("Missing "+path);
                var source=model.GetComponentInChildren<MeshFilter>();
                if(!source) throw new System.Exception("Mesh missing: "+path);
                var part=new GameObject(name); part.transform.SetParent(root.transform,false);
                part.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;
                string matPath=Root+name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(!mat){mat=new Material(shader); AssetDatabase.CreateAsset(mat,matPath);}
                mat.shader=shader; mat.color=colors[i];
                mat.SetFloat("_Metallic",i==2?.65f:0f);mat.SetFloat("_Smoothness",i==2?.35f:.18f);mat.SetFloat("_Glossiness",i==2?.35f:.18f);
                if(i==0){
                    if(urp){mat.SetFloat("_Surface",1);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");}
                    else {mat.SetFloat("_Mode",3);mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");}
                    mat.SetOverrideTag("RenderType","Transparent");mat.SetInt("_SrcBlend",(int)(urp?UnityEngine.Rendering.BlendMode.SrcAlpha:UnityEngine.Rendering.BlendMode.One));mat.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);mat.SetInt("_ZWrite",0);mat.renderQueue=3000;
                }
                EditorUtility.SetDirty(mat);part.AddComponent<MeshRenderer>().sharedMaterial=mat;
                if(i==3)anim.plunger=part.transform;if(i==4)anim.piston=part.transform;if(i==5)anim.liquid=part.transform;
            }
            var col=root.AddComponent<BoxCollider>();col.center=new Vector3(0,.091f,0);col.size=new Vector3(.032f,.182f,.032f);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"Syringe.prefab");AssetDatabase.SaveAssets();Selection.activeObject=prefab;
        }finally{Object.DestroyImmediate(root);}
    }
}

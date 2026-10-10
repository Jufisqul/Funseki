using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Funseki.EditorTools
{
    public static class KitchenMarketSetup
    {
        static Transform parent;
        static Material steel, iron, teal, clay, saffron, red, wood, cream, green;
        const string Dir = "Assets/_Project/Art/KitchenMarket";
        static Material Mat(string name, Color color, float metallic = 0)
        {
            var path = Dir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", metallic > 0 ? .38f : .15f);
            EditorUtility.SetDirty(m); return m;
        }
        static GameObject Shape(string name, PrimitiveType type, Vector3 p, Vector3 scale, Material m, bool solid = false)
        {
            var g = GameObject.CreatePrimitive(type); g.name = name; g.transform.SetParent(parent, false);
            g.transform.position = p; g.transform.localScale = scale; g.GetComponent<Renderer>().sharedMaterial = m;
            if (!solid) Object.DestroyImmediate(g.GetComponent<Collider>());
            g.isStatic = true; return g;
        }
        static GameObject Box(string n, float x,float y,float z,float w,float h,float d,Material m,bool solid=false) => Shape(n,PrimitiveType.Cube,new Vector3(x,y,z),new Vector3(w,h,d),m,solid);
        static GameObject Round(string n,float x,float y,float z,float radius,float h,Material m,bool solid=false) => Shape(n,PrimitiveType.Cylinder,new Vector3(x,y,z),new Vector3(radius*2,h/2,radius*2),m,solid);
        static void Label(string text,float x,float y,float z,float size, Color color, float yaw=0)
        {
            var g=new GameObject("Sign_"+text); g.transform.SetParent(parent,false);g.transform.position=new Vector3(x,y,z);g.transform.eulerAngles=new Vector3(0,yaw,0);
            var t=g.AddComponent<TextMesh>();t.text=text;t.fontSize=64;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;
        }
        static void Table(float x,float z,float w,float d)
        {
            Box("Steel worktop",x,1.02f,z,w,.12f,d,steel,true);
            foreach(var dx in new[]{-w/2+.12f,w/2-.12f}) foreach(var dz in new[]{-d/2+.12f,d/2-.12f}) Box("Table leg",x+dx,.5f,z+dz,.08f,1,.08f,iron);
            Box("Lower shelf",x,.25f,z,w-.15f,.06f,d-.15f,steel);
        }
        static void Pot(float x,float y,float z,float r,Material food)
        {
            Round("Stockpot",x,y,z,r,.45f,steel);Round("Curry surface",x,y+.23f,z,r*.87f,.015f,food);
            Round("Pot rim",x,y+.24f,z,r,.04f,iron);
            // A smaller food disc sits above the rim, keeping the contents visible.
            Round("Food",x,y+.265f,z,r*.86f,.02f,food);
            foreach(var dx in new[]{-r-.06f,r+.06f}) Box("Pot handle",x+dx,y+.1f,z,.17f,.07f,.13f,iron);
            Box("Ladle shaft",x+.1f,y+.5f,z,.025f,.65f,.025f,steel).transform.Rotate(0,0,-22);
        }
        [MenuItem("Tools/Funseki/Kitchen Market/Apply")]
        public static void Apply()
        {
            var s=SceneManager.GetSceneByPath("Assets/_Project/School/Scenes/School_Greybox.unity");
            if(!s.IsValid()||!s.isLoaded)s=EditorSceneManager.OpenScene("Assets/_Project/School/Scenes/School_Greybox.unity",OpenSceneMode.Additive);
            var roots=s.GetRootGameObjects();
            var existing=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t=>t.name=="Kitchen_Market_Interior");
            if(existing) { Selection.activeGameObject=existing.gameObject; Debug.Log("[KitchenMarket] Interior already exists; hand edits preserved."); return; }
            System.IO.Directory.CreateDirectory(Dir);AssetDatabase.Refresh();
            steel=Mat("BrushedSteel",new Color(.56f,.61f,.61f),.75f);iron=Mat("BlackenedIron",new Color(.07f,.085f,.08f),.55f);
            teal=Mat("PaintedTeal",new Color(.025f,.34f,.30f));clay=Mat("Terracotta",new Color(.56f,.22f,.105f));
            saffron=Mat("Turmeric",new Color(.96f,.56f,.035f));red=Mat("Chilli",new Color(.65f,.08f,.035f));wood=Mat("DarkWood",new Color(.25f,.13f,.06f));cream=Mat("SackCanvas",new Color(.72f,.64f,.44f));green=Mat("Herbs",new Color(.16f,.36f,.035f));
            var props=roots.FirstOrDefault(r=>r.name=="School_Props");if(!props){props=new GameObject("School_Props");SceneManager.MoveGameObjectToScene(props,s);}
            var g=new GameObject("Kitchen_Market_Interior");g.transform.SetParent(props.transform,false);parent=g.transform;Undo.RegisterCreatedObjectUndo(g,"Furnish kitchen market");
            // Preserve the old placeholder furniture as inactive objects for easy restoration.
            var furniture=roots.FirstOrDefault(r=>r.name=="School_Furniture");
            if(furniture){var zone=furniture.transform.Find("Floor_1/Zone_kitchen");if(zone){Undo.RecordObject(zone.gameObject,"Replace kitchen placeholders");zone.gameObject.SetActive(false);}}
            // Real serving opening: replace two wall modules with sill and lintel.
            var school=roots.First(r=>r.name=="School");
            foreach(var r in school.GetComponentsInChildren<Renderer>(true))
            {var b=r.bounds;if(r.name=="PF_Kit_Wall"&&Mathf.Abs(b.center.z+22.1f)<.12f&&b.center.x>69&&b.center.x<73){Undo.RecordObject(r.gameObject,"Open serving hatch");r.gameObject.SetActive(false);}}
            var wallMaterial=school.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="PF_Kit_Wall").sharedMaterial;
            Box("Hatch lower wall",71,.5f,-22.1f,4,1,.2f,wallMaterial,true);Box("Hatch lintel",71,3.55f,-22.1f,4,1.3f,.2f,wallMaterial,true);
            foreach(var x in new[]{69.06f,72.94f})Box("Teal hatch frame",x,1.97f,-22.1f,.12f,1.94f,.3f,teal);
            Box("Serving counter",71,1.08f,-22.1f,4.3f,.16f,1.05f,steel,true);
            Box("Menu board",71,3.22f,-22.1f,3.6f,.65f,.3f,teal);
            Label("MASALA KITCHEN",71,3.27f,-22.27f,.065f,new Color(1,.76f,.24f));
            Label("THALI  /  CHAI  /  ROTI",71,3.04f,-22.27f,.032f,Color.white);
            for(int i=0;i<5;i++){Round("Thali plate",69.6f+i*.65f,1.19f,-22.12f,.23f,.025f,steel);Round("Curry bowl",69.6f+i*.65f,1.25f,-22.25f,.09f,.1f,saffron);}
            // Hot line against the east wall.
            Box("Range oven",72.85f,.48f,-26.2f,1.45f,.95f,3.8f,iron,true);Box("Range top",72.85f,1,-26.2f,1.5f,.1f,3.9f,steel);
            for(int i=0;i<3;i++){float z=-24.95f-i*1.2f;Round("Gas burner",72.85f,1.09f,z,.39f,.07f,iron);Pot(72.85f,1.38f,z,.36f,i%2==0?saffron:red);Box("Oven door",72.08f,.52f,z,.04f,.56f,.9f,steel);Round("Range dial",72.04f,.89f,z,.07f,.06f,iron).transform.Rotate(0,0,90);}
            Box("Extractor hood",72.65f,2.8f,-26.2f,2.1f,.4f,4.1f,steel);Box("Extractor duct",73.35f,3.45f,-26.2f,.5f,1,.65f,steel);
            Round("Tandoor clay body",72.7f,.67f,-30.1f,.66f,1.25f,clay,true);Round("Tandoor black opening",72.7f,1.31f,-30.1f,.42f,.04f,iron);Round("Tandoor inner glow",72.7f,1.335f,-30.1f,.26f,.01f,red);
            for(int i=0;i<3;i++)Box("Tandoor skewer",72.45f+i*.2f,1.65f,-30.1f,.02f,.9f,.02f,steel).transform.Rotate(0,0,12);
            // Prep island and low kneading station.
            Table(69.15f,-27.8f,2.7f,1.3f);Box("Chopping board",69.15f,1.11f,-27.8f,1.1f,.05f,.65f,wood);
            for(int i=0;i<7;i++)Shape("Vegetable",PrimitiveType.Sphere,new Vector3(68.8f+i*.11f,1.2f,-27.7f),new Vector3(.14f,.14f,.14f),i%2==0?red:green);
            Box("Knife",69.5f,1.16f,-28,.35f,.015f,.09f,steel);Box("Knife handle",69.75f,1.16f,-28,.2f,.03f,.06f,iron);
            Table(64,-28.3f,2.2f,1.2f);Pot(64,1.35f,-28.3f,.3f,cream);
            Box("Low dough platform",69.4f,.25f,-30.55f,2.3f,.5f,1.2f,wood,true);
            Round("Wide kneading basin",69.4f,.57f,-30.55f,.5f,.14f,steel);Round("Dough",69.4f,.66f,-30.55f,.43f,.03f,cream);
            Box("Foot operated mixer pedal",70.65f,.08f,-30.5f,.3f,.12f,.55f,iron,true);
            // Washing and cold storage.
            Box("Sink cabinet",63,.47f,-25.1f,1.2f,.94f,2.4f,teal,true);Box("Sink top",63,1,-25.1f,1.25f,.1f,2.5f,steel);
            foreach(var z in new[]{-24.5f,-25.7f}){Box("Basin recess",63,1.06f,z,.84f,.015f,.8f,iron);Box("Basin bottom",63,1.075f,z,.64f,.01f,.6f,steel);Box("Tap upright",62.65f,1.29f,z,.06f,.4f,.06f,steel);Box("Tap spout",62.8f,1.48f,z,.36f,.06f,.06f,steel);}
            Box("Refrigerator",63.15f,1.15f,-30.3f,1.4f,2.3f,1.3f,teal,true);Box("Fridge door",63.15f,1.2f,-29.63f,1.28f,2.1f,.06f,steel);Box("Fridge handle",63.67f,1.3f,-29.52f,.06f,.7f,.06f,iron);
            // Pantry shelving with steel spice tins, sacks and produce crates.
            for(int shelf=0;shelf<3;shelf++)
            {float y=.45f+shelf*.68f;Box("Pantry shelf",64.6f,y,-23.1f,3.2f,.08f,.65f,wood);for(int i=0;i<8;i++){float x=63.25f+i*.37f;Round("Spice tin",x,y+.18f,-23.1f,.13f,.28f,steel);Round("Spice tin lid",x,y+.33f,-23.1f,.14f,.04f,i%3==0?saffron:i%3==1?red:green);}}
            foreach(var x in new[]{63.05f,66.15f})Box("Pantry upright",x,1.23f,-23.1f,.08f,2.4f,.65f,iron);
            for(int i=0;i<3;i++){Shape("Rice sack",PrimitiveType.Capsule,new Vector3(64.2f+i*.62f,.48f,-26.8f),new Vector3(.53f,.48f,.53f),cream);Label("RICE",64.2f+i*.62f,.55f,-26.51f,.055f,new Color(.3f,.12f,.02f));}
            for(int i=0;i<2;i++){Box("Produce crate",65.15f,.25f+i*.47f,-30.9f,1,.43f,.8f,wood,true);for(int j=0;j<5;j++)Shape("Crate produce",PrimitiveType.Sphere,new Vector3(64.8f+j*.17f,.48f+i*.47f,-30.9f),new Vector3(.17f,.18f,.17f),i==0?green:red);}
            // Marigold garlands, striped awning and chai corner.
            for(int i=0;i<28;i++)Shape("Marigold garland",PrimitiveType.Sphere,new Vector3(69.1f+i*.14f,2.88f-.19f*Mathf.Sin(i*.23f),-21.91f),Vector3.one*.12f,i%3==0?red:saffron);
            for(int i=0;i<14;i++)Box("Striped stall awning",69.05f+i*.3f,2.97f,-22.85f,.3f,.07f,1.05f,i%2==0?saffron:cream).transform.Rotate(-10,0,0);
            Table(70.9f,-23.75f,2.2f,.7f);Pot(70.35f,1.27f,-23.75f,.2f,cream);
            for(int i=0;i<6;i++)Round("Chai cup",70.9f+i*.13f,1.16f,-23.75f,.045f,.15f,clay);
            foreach(var x in new[]{65.5f,70.5f}){Round("Pendant shade",x,3.35f,-27,.3f,.15f,iron);var lamp=new GameObject("Warm kitchen light");lamp.transform.SetParent(parent,false);lamp.transform.position=new Vector3(x,3.15f,-27);var l=lamp.AddComponent<Light>();l.type=LightType.Point;l.color=new Color(1,.75f,.42f);l.intensity=2;l.range=7;}
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);SceneManager.SetActiveScene(s);
            Selection.activeGameObject=g;SceneView.lastActiveSceneView?.LookAt(new Vector3(68,1.4f,-27),Quaternion.Euler(22,135,0),12);
            Debug.Log("[KitchenMarket] Saved equipment, pantry, decor and 4m serving hatch; "+g.GetComponentsInChildren<Renderer>().Length+" renderers.");
        }
        [MenuItem("Tools/Funseki/Kitchen Market/Inspect")]
        public static void Inspect()
        {
            var s = SceneManager.GetSceneByPath("Assets/_Project/School/Scenes/School_Greybox.unity");
            if (!s.IsValid() || !s.isLoaded) s = EditorSceneManager.OpenScene("Assets/_Project/School/Scenes/School_Greybox.unity", OpenSceneMode.Additive);
            foreach (var root in s.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Zone_kitchen")
                {
                    Debug.Log("KITCHEN " + root.name + " position=" + t.position);
                    foreach (Transform c in t) Debug.Log(c.name + " p=" + c.position + " scale=" + c.lossyScale);
                    if (root.name == "School") foreach(var r in t.GetComponentsInChildren<Renderer>(true)) if(r.name.Contains("Wall")) Debug.Log("KWALL " + r.name + " " + r.bounds);
                }
        }
        [MenuItem("Tools/Funseki/Kitchen Market/Fix Sign")]
        public static void FixSign()
        {
            var s=SceneManager.GetSceneByPath("Assets/_Project/School/Scenes/School_Greybox.unity");
            parent=s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="Kitchen_Market_Interior");
            teal=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/PaintedTeal.mat");
            foreach(var t in parent.GetComponentsInChildren<TextMesh>()) if(t.text.Contains("MASALA")||t.text.Contains("THALI"))Object.DestroyImmediate(t.gameObject);
            var board=parent.Find("Menu board");board.position=new Vector3(71,3.22f,-22.1f);board.localScale=new Vector3(3.6f,.65f,.3f);
            Label("MASALA KITCHEN",71,3.27f,-22.27f,.065f,new Color(1,.76f,.24f));
            Label("THALI  /  CHAI  /  ROTI",71,3.04f,-22.27f,.032f,Color.white);
            EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);
        }
    }
}


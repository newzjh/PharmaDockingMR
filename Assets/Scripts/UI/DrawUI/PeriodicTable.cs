using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class ElementData
{
    public string name;
    public string category;
    public string spectral_img;
    public int xpos;
    public int ypos;
    public string named_by;
    public float density;
    public string color;
    public float molar_heat;
    public string symbol;
    public string discovered_by;
    public string appearance;
    public float atomic_mass;
    public float melt;
    public string number;
    public string source;
    public int period;
    public string phase;
    public string summary;
    public int boil;
}

[System.Serializable]
class ElementsData
{
    public List<ElementData> elements;

    public static ElementsData FromJSON(string json)
    {
        return JsonUtility.FromJson<ElementsData>(json);
    }
}

public class PeriodicTable : BasePanelEx<PeriodicTable>
{
    public float ElementSeperationDistance = 0.2f;
    public float CollectionRadius=1.0f;

    public Material MatAlkaliMetal;
    public Material MatAlkalineEarthMetal;
    public Material MatTransitionMetal;
    public Material MatMetalloid;
    public Material MatDiatomicNonmetal;
    public Material MatPolyatomicNonmetal;
    public Material MatPostTransitionMetal;
    public Material MatNobleGas;
    public Material MatActinide;
    public Material MatLanthanide;


	// Use this for initialization
    public override void Awake()
    {

        Dictionary<string, Color> typeColors = new Dictionary<string, Color>()
        {
            { "alkali metal", new Color(137.0f/255.0f,122.0f/255.0f,173.0f/255.0f)  },
            { "alkaline earth metal", new Color(191.0f/255.0f,133.0f/255.0f,191.0f/255.0f) },
            { "transition metal", new Color(189.0f/255.0f,255.0f/255.0f,255.0f/255.0f) },
            { "metalloid", new Color(228.0f/255.0f,135.0f/255.0f,71.0f/255.0f)  },
            { "diatomic nonmetal", new Color(126.0f/255.0f,180.0f/255.0f,206.0f/255.0f)  },
            { "polyatomic nonmetal", new Color(215.0f/255.0f,197.0f/255.0f,114.0f/255.0f)   },
            { "post-transition metal", new Color(94.0f/255.0f,180.0f/255.0f,133.0f/255.0f) },
            { "noble gas", new Color(1.0f,0.5f,1.0f) },
            { "actinide", new Color(161.0f/255.0f,196.0f/255.0f,180.0f/255.0f) },
            { "lanthanide", new Color(144.0f/255.0f,177.0f/255.0f,161.0f/255.0f)  },
        };

        Dictionary<string, Material> typeMaterials = new Dictionary<string, Material>()
        {
            { "alkali metal", MatAlkaliMetal },
            { "alkaline earth metal", MatAlkalineEarthMetal },
            { "transition metal", MatTransitionMetal },
            { "metalloid", MatMetalloid },
            { "diatomic nonmetal", MatDiatomicNonmetal },
            { "polyatomic nonmetal", MatPolyatomicNonmetal },
            { "post-transition metal", MatPostTransitionMetal },
            { "noble gas", MatNobleGas },
            { "actinide", MatActinide },
            { "lanthanide", MatLanthanide },
        };

        // Parse the elements out of the json file
        TextAsset asset = Resources.Load<TextAsset>("PeriodicTable");
        List<ElementData> elements = ElementsData.FromJSON(asset.text).elements;

        Transform tTemplate=this.transform.Find("Template");
        if (tTemplate == null)
            return;

        // Insantiate the element prefabs in their correct locations and with correct text
        foreach (ElementData element in elements)
        {
            GameObject newElement = GameObject.Instantiate<GameObject>(tTemplate.gameObject);
            newElement.name = element.symbol;
            newElement.transform.parent = this.transform;
            RectTransform rt=newElement.GetComponent<RectTransform>();
            //rt.sizeDelta = new Vector3(60, 60);
            Image img=newElement.GetComponent<Image>();
            if (img!=null)
            {
                string key = element.category.Trim();
                //if (typeColors.ContainsKey(key))
                //{
                //    Color col = typeColors[key];
                //    col.a = 0.7f;
                //    img.color = col;
                //}
                if (typeMaterials.ContainsKey(key))
                {
                    img.material = typeMaterials[key];
                }
            }
            //newElement.AddComponent<Button>();
            newElement.transform.localScale = Vector3.one;
            Vector3 lpos = new Vector3(element.xpos * ElementSeperationDistance - ElementSeperationDistance * 18 / 2, ElementSeperationDistance * 9 - element.ypos * ElementSeperationDistance, CollectionRadius);
            lpos.x *= 320.0f;
            lpos.y *= 320.0f;
            lpos.y -= 260;
            newElement.transform.localPosition = lpos;
            newElement.transform.localRotation = Quaternion.identity;

            Text[] texts = newElement.GetComponentsInChildren<Text>();
            if (texts.Length == 3)
            {
                texts[0].text = element.number;
                texts[1].text = element.symbol;
                texts[2].text = element.name;
            }
        }

        GameObject.Destroy(tTemplate.gameObject);

        base.Awake();
	}

    public override void OnClick(GameObject sender)
    {
        Debug.Log(sender.name);
    }

}

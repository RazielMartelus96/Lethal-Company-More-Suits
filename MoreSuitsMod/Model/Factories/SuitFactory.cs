using System;
using System.Collections.Generic;
using System.IO;
using MoreSuitsMod.Model.Suit;
using UnityEngine;

namespace MoreSuitsMod.Model.Factories;

public class SuitFactory : MonoBehaviour, ISuitFactory
{
    public List<string> SuitFolderPaths { get; set; }
    private List<string> _texturePaths = [];
    private List<string> _assetPaths = [];
    private Dictionary<string, Material> _customMaterialCache = [];
    public UnlockableItem OriginalSuit { get; set; }
    
    public List<ISuit> Create()
    {
        List<ISuit> suits = new List<ISuit>();
        SetPaths();
        SortPaths();
        foreach (Material material in InitMaterialsCache())
        {
            _customMaterialCache.Add(material.name, material);
        }

        foreach (string texturePath in _texturePaths)
        {
            var suit = InitSuitFromPath(texturePath);
            suit = HandleSuitModifications(suit, texturePath);
            suits.Add(suit);
        }

        return suits;
    }


    private (List<string> texturePaths, List<string> assetPaths) InitPaths()
    {
        List<string> texturePaths = [];
        List<string> assetPaths = [];

        foreach (var suitsFolderPath in SuitFolderPaths)
        {
            if (suitsFolderPath == "") continue;
            var pngFiles = Directory.GetFiles(suitsFolderPath, "*.png");
            var bundleFiles = Directory.GetFiles(suitsFolderPath, "*.matbundle");
            texturePaths.AddRange(pngFiles);
            assetPaths.AddRange(bundleFiles);
        }

        return (texturePaths, assetPaths);
    }

    private void SetPaths()
    {
        var paths = InitPaths();
        _texturePaths = paths.texturePaths;
        _assetPaths = paths.assetPaths;
    }

    private void SortPaths()
    {
        _texturePaths.Sort();
        _assetPaths.Sort();
    }

    private List<Material> InitMaterialsCache()
    {
        try
        {
            List<Material> materials = [];
            foreach (var assetPath in _assetPaths)
            {
                AssetBundle assetBundle = AssetBundle.LoadFromFile(assetPath);
                UnityEngine.Object[] assets = assetBundle.LoadAllAssets();
                foreach (var asset in assets)
                {
                    if (asset is Material material)
                    {
                        materials.Add(material);
                    }
                }
            }

            return materials;
        }
        catch (Exception e)
        {
            //TODO add custom exception here.
            throw new Exception($"Failed to load materials from {_assetPaths}", e);
        }
    }

    private ISuit InitSuitFromPath(string texturePath)
    {
        ISuit suit = null;

        suit.UnlockableName = Path.GetFileNameWithoutExtension(texturePath);
        suit.SuitMaterial = InitMaterialFromPath(texturePath);

        return suit;
    }

    private Material InitMaterialFromPath(string texturePath)
    {
        Material material = Path
            .GetFileNameWithoutExtension(texturePath)
            .ToLower() == "default"
            ? OriginalSuit.suitMaterial
            : Instantiate(JsonUtility.FromJson<UnlockableItem>(JsonUtility.ToJson(OriginalSuit)).suitMaterial);
        material.mainTexture = InitSuitTextureFromPath(texturePath);
        return material;
    }

    private Texture2D InitSuitTextureFromPath(string texturePath)
    {
        byte[] fileData = File.ReadAllBytes(texturePath);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(fileData);

        texture.Apply(true, true);
        return texture;
    }

    private ISuit HandleSuitModifications(ISuit suit, string texturePath)
    {
        {
            string advancedJsonPath = Path.Combine(Path.GetDirectoryName(texturePath), "advanced",
                suit.UnlockableName + ".json");


            if (!File.Exists(advancedJsonPath)) return suit;


            foreach (string line in File.ReadAllLines(advancedJsonPath))
            {
                string[] keyValue = line.Trim().Split(':');
                if (keyValue.Length != 2) continue;

                string keyData = keyValue[0].Trim('"', ' ', ',');
                string valueData = keyValue[1].Trim('"', ' ', ',');

                if (valueData.EndsWith(".png"))
                {
                    LoadAdvancedTexture(texturePath, valueData, keyData, suit.SuitMaterial);
                }
                else
                {
                    switch (keyData)
                    {
                        case "PRICE" when int.TryParse(valueData, out int price):
                            //TODO replace logic here with some sort of way to add price to the suit.
                            //AddSuitToShop(suit, price);
                            break;

                        case "KEYWORD":
                            suit.SuitMaterial.EnableKeyword(valueData);
                            break;

                        case "DISABLEKEYWORD":
                            suit.SuitMaterial.DisableKeyword(valueData);
                            break;

                        case "SHADERPASS":
                            suit.SuitMaterial.SetShaderPassEnabled(valueData, true);
                            break;

                        case "DISABLESHADERPASS":
                            suit.SuitMaterial.SetShaderPassEnabled(valueData, false);
                            break;

                        case "SHADER":
                            Shader shader = Shader.Find(valueData);
                            suit.SuitMaterial.shader = shader;
                            break;

                        case "MATERIAL":
                            ApplyCustomMaterial(valueData, suit.SuitMaterial.mainTexture);
                            break;

                        default:
                            ApplyNumericOrVectorValue(keyData, valueData, suit.SuitMaterial);
                            break;
                    }
                }
            }
        }
        return suit;
    }

    void LoadAdvancedTexture(string baseTexturePath, string textureFileName, string textureKey, Material material)
        {
            string texturePath = Path.Combine(Path.GetDirectoryName(baseTexturePath), "advanced", textureFileName);
            byte[] textureData = File.ReadAllBytes(texturePath);
            Texture2D advancedTexture = new Texture2D(2, 2);
            advancedTexture.LoadImage(textureData);
            advancedTexture.Apply(true, true);
            material.SetTexture(textureKey, advancedTexture);
        }

        //TODO add this to the UnlockableSuitFactory later
        /**
        void AddSuitToShop(Suit suit, int price)
        {
            try
            {
                if (!UnlockAll)
                    suit = AddToRotatingShop(suit, price, __instance.unlockablesList.unlockables.Count);
            }
            catch (Exception ex)
            {
                Debug.Log("Failed to add suit to rotating shop. Error: " + ex);
            }
        }
        **/ 
        Material ApplyCustomMaterial(string materialName, Texture mainTexture)
        { 
            Material customMaterial = Instantiate(_customMaterialCache[materialName]);
            customMaterial.mainTexture = mainTexture;
            return customMaterial;
            
        }
        

        void ApplyNumericOrVectorValue(string key, string value, Material material)
        {
            if (float.TryParse(value, out float floatValue))
            {
                material.SetFloat(key, floatValue);
            }
            else if (TryParseVector4(value, out Vector4 vectorValue))
            {
                material.SetVector(key, vectorValue);
            }
    }
        public static bool TryParseVector4(string input, out Vector4 vector)
        {
            vector = Vector4.zero;

            string[] components = input.Split(',');

            if (components.Length == 4)
            {
                if (float.TryParse(components[0], out float x) &&
                    float.TryParse(components[1], out float y) &&
                    float.TryParse(components[2], out float z) &&
                    float.TryParse(components[3], out float w))
                {
                    vector = new Vector4(x, y, z, w);
                    return true;
                }
            }

            return false;
        }
        
}



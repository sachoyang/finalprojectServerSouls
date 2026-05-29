using System.IO;
using UnityEditor;
using UnityEngine;

public static class MagentaToTransparent
{
    [MenuItem("Tools/Image/Convert Magenta To Transparent")]
    public static void ConvertSelectedTexture()
    {
        Texture2D sourceTexture = Selection.activeObject as Texture2D;

        if (sourceTexture == null)
        {
            Debug.LogWarning("투명화할 Texture2D를 Project 창에서 선택해주세요.");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(sourceTexture);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
        {
            Debug.LogWarning("TextureImporter를 찾을 수 없습니다.");
            return;
        }

        bool previousReadable = importer.isReadable;
        importer.isReadable = true;
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.SaveAndReimport();

        Texture2D readableTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        Texture2D newTexture = new Texture2D(readableTexture.width, readableTexture.height, TextureFormat.RGBA32, false);

        Color32[] pixels = readableTexture.GetPixels32();

        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];

            bool isMagenta =
                pixel.r > 240 &&
                pixel.g < 20 &&
                pixel.b > 240;

            if (isMagenta)
                pixels[i] = new Color32(0, 0, 0, 0);
        }

        newTexture.SetPixels32(pixels);
        newTexture.Apply();

        byte[] pngData = newTexture.EncodeToPNG();
        File.WriteAllBytes(assetPath, pngData);

        importer.isReadable = previousReadable;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.SaveAndReimport();

        AssetDatabase.Refresh();

        Debug.Log("마젠타 배경을 투명화했습니다: " + assetPath);
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ImageList", menuName = "ScriptableObjects/ImageList", order = 1)]
public class ImageList : ScriptableObject
{
    public List<Texture2D> images = new List<Texture2D>();
}

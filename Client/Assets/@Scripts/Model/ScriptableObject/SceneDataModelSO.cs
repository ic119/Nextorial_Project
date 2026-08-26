using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SceneDataModelSO", menuName = "ScriptableObjectAssets/SceneDataModel")]
public class SceneDataModelSO : ScriptableObject
{
    public List<SceneDataModel> sceneDataModels;
}
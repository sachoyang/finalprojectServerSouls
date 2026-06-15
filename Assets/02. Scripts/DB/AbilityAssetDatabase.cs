using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AbilityAssetDatabase", menuName = "ServerSouls/Ability Asset Database")]
public class AbilityAssetDatabase : ScriptableObject
{
    [Header("에셋 등록소 (여러 에셋을 한 번에 드래그 앤 드롭하세요!)")]
    public List<Sprite> icons = new List<Sprite>();
    public List<AnimationClip> animations = new List<AnimationClip>();
    public List<GameObject> prefabs = new List<GameObject>(); 
    public List<AudioClip> sounds = new List<AudioClip>();

    [Space(20)]
    [Header("📥 FBX 애니메이션 자동 추출기")]
    [Tooltip("여기에 FBX 파일을 드래그해서 넣으면, 내부의 애니메이션만 쏙 뽑아서 자동으로 위에 등록합니다!")]
    public List<GameObject> dropFbxHere = new List<GameObject>();

    // 🔍 이름(Key)으로 에셋을 직접 찾아주는 헬퍼 함수들
    public Sprite GetIcon(string key) => icons.Find(x => x != null && x.name == key);
    public AnimationClip GetAnim(string key) => animations.Find(x => x != null && x.name == key);
    public GameObject GetPrefab(string key) => prefabs.Find(x => x != null && x.name == key);
    public AudioClip GetSound(string key) => sounds.Find(x => x != null && x.name == key);

    // ==========================================
    // 유니티 에디터에서 값이 바뀔 때마다 실행되는 마법의 함수
    // ==========================================
#if UNITY_EDITOR
    private void OnValidate()
    {
        // FBX 추출기에 파일이 들어왔다면?
        if (dropFbxHere != null && dropFbxHere.Count > 0)
        {
            foreach (GameObject fbx in dropFbxHere)
            {
                if (fbx == null) continue;

                // 1. 해당 FBX 파일의 실제 프로젝트 내 경로를 가져옵니다.
                string path = UnityEditor.AssetDatabase.GetAssetPath(fbx);
                if (string.IsNullOrEmpty(path)) continue;

                // 2. 경로 안에 숨어있는 모든 하위 에셋(Sub-asset)을 긁어옵니다.
                Object[] allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                
                foreach (Object asset in allAssets)
                {
                    // 3. 만약 그 에셋이 애니메이션 클립이고, 유니티 기본 프리뷰 클립이 아니라면
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        // animations 리스트에 중복이 없을 때만 추가!
                        if (!animations.Contains(clip))
                        {
                            animations.Add(clip);
                        }
                    }
                }
            }

            // 4. 애니메이션만 쏙 빼먹었으니, 수거함은 다시 깔끔하게 비워줍니다.
            dropFbxHere.Clear();
        }
    }
#endif
}
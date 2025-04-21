using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Unity.FPS.UI
{
    public class LoadSceneButton : MonoBehaviour
    {
        public string SceneName = "";

        void Update()
        {
            if (EventSystem.current.currentSelectedGameObject == gameObject
                && Input.GetButtonDown(GameConstants.k_ButtonNameSubmit))
            {
                LoadTargetScene();
            }
        }

        public void LoadTargetScene()
        {
            // Destroy all runtime emitters we manually created
            // Wwise3DEmitter.DestroyAllEmitters();

            // Optionally find and destroy anything tagged with AkGameObj or AkAudioListener
            foreach (var audioObj in GameObject.FindObjectsOfType<AkGameObj>())
            {
                if (audioObj != null && audioObj.gameObject != null)
                {
                    GameObject.Destroy(audioObj.gameObject);
                }
            }

            SceneManager.LoadScene(SceneName);
        }
    }
}
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManager : MonoBehaviour {
    public void ResetScene() {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Demo", LoadSceneMode.Single);
    }
}

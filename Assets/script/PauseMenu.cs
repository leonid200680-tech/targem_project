using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;

    [Header("Panels")]
    public GameObject pauseMenuPanel;
    public GameObject gameplayHUD;
    public GameObject mainMenuPanel;
    public GameObject settingsPanel; // <-- ÄÎÁÀÂÈËÈ ÏÀÍÅËÜ ÍÀÑÒÐÎÅÊ
    public AudioListener menuAudioListener;
    public GameObject videoBackground;

    private void Start()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Åñëè îòêðûòû íàñòðîéêè, Esc äîëæåí çàêðûâàòü èõ
            if (settingsPanel.activeSelf)
            {
                ClosePauseSettings();
                return;
            }

            if (GameIsPaused) Resume();
            else Pause();
        }
    }

    public void Resume()
    {
        pauseMenuPanel.SetActive(false);
        if (gameplayHUD != null) gameplayHUD.SetActive(true);

        Time.timeScale = 1f;
        GameIsPaused = false;
        AudioListener.pause = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Pause()
    {
        pauseMenuPanel.SetActive(true);
        if (gameplayHUD != null) gameplayHUD.SetActive(false);

        Time.timeScale = 0f;
        GameIsPaused = true;
        AudioListener.pause = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    // --- ÍÎÂÛÅ ÌÅÒÎÄÛ ÄËß ÍÀÑÒÐÎÅÊ ÈÇ ÏÀÓÇÛ ---

    public void OpenPauseSettings()
    {
        pauseMenuPanel.SetActive(false); // Ïðÿ÷åì ìåíþ ïàóçû
        settingsPanel.SetActive(true);   // Ïîêàçûâàåì íàñòðîéêè
        PauseMenu.GameIsPaused = true;
    }

    public void ClosePauseSettings()
    {
        settingsPanel.SetActive(false);  // Ïðÿ÷åì íàñòðîéêè
        pauseMenuPanel.SetActive(true);  // Âîçâðàùàåì ìåíþ ïàóçû
    }

    // ------------------------------------------

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        GameIsPaused = false;
        AudioListener.pause = false;

        pauseMenuPanel.SetActive(false);
        mainMenuPanel.SetActive(true);

        if (videoBackground != null) videoBackground.SetActive(true); 

        GameObject menuMusic = GameObject.Find("MenuMusic");
        if (menuMusic != null) menuMusic.GetComponent<AudioSource>().Play();
        if (menuAudioListener != null) menuAudioListener.enabled = true;
    }
}
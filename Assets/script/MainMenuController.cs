using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject gameplayHUD;
    public GameObject videoBackground;
    public GameObject pauseMenuPanel;

    [Header("Player")]
    public GameObject playerObject;

    [Header("Audio")]
    public AudioSource menuMusic;
    public AudioSource gameMusic;       // Музыка в игре
    public AudioSource swingSound;      // Звук качелей
    public AudioSource carouselSound;   // <--- НОВЫЙ СЛОТ: Звук карусели
    public AudioSource generatorSound;  // Звук генератора
    public AudioListener menuAudioListener;

    private bool isGameStarted = false;

    private void Start()
    {
        // 1. Выключаем игрока (чтобы его камера не мешала меню)
        if (playerObject != null) playerObject.SetActive(false);
        if (videoBackground != null) videoBackground.SetActive(true);

        // 2. Настраиваем панели
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
        gameplayHUD.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

        // 3. Звук
        AudioListener.pause = false;

        // Включаем музыку меню (если не привязана в инспекторе, ищем по имени)
        if (menuMusic == null)
        {
            GameObject musicObj = GameObject.Find("MenuMusic");
            if (musicObj != null) menuMusic = musicObj.GetComponent<AudioSource>();
        }
        if (menuMusic != null) menuMusic.Play();

        // На всякий случай выключаем игровые звуки при старте
        if (gameMusic != null) gameMusic.Stop();
        if (swingSound != null) swingSound.Stop();
        if (carouselSound != null) carouselSound.Stop(); // <--- ВЫКЛЮЧАЕМ КАРУСЕЛЬ
        if (generatorSound != null) generatorSound.Stop();

        // 4. Освобождаем мышку
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void Update()
    {
        if (!isGameStarted)
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    public void StartGame()
    {
        isGameStarted = true;

        mainMenuPanel.SetActive(false);
        if (videoBackground != null) videoBackground.SetActive(false);
        gameplayHUD.SetActive(true);

        // --- МУЗЫКА И ЗВУКИ ---
        if (menuMusic != null) menuMusic.Stop();       // Выключаем меню
        if (gameMusic != null) gameMusic.Play();       // Включаем музыку игры
        if (swingSound != null) swingSound.Play();     // Включаем качели
        if (carouselSound != null) carouselSound.Play(); // <--- ВКЛЮЧАЕМ КАРУСЕЛЬ
        if (generatorSound != null) generatorSound.Play(); // Включаем генератор
        // ----------------------

        if (playerObject != null) playerObject.SetActive(true);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void OpenSettings()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
        PauseMenu.GameIsPaused = false;
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);

        if (PauseMenu.GameIsPaused)
        {
            mainMenuPanel.SetActive(false);
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        }
        else
        {
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            mainMenuPanel.SetActive(true);
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
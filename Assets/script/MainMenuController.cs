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
    public GameObject playerObject; // Перетащи сюда Player

    [Header("Audio")]
    public AudioSource menuMusic;
    public AudioListener menuAudioListener;

    private bool isGameStarted = false; // Флаг, чтобы понимать, началась игра или нет

    private void Start()
    {
        // 1. Выключаем игрока СРАЗУ (чтобы его скрипты не мешали)
        if (playerObject != null) playerObject.SetActive(false);
        if (videoBackground != null) videoBackground.SetActive(true);
        // 2. Настраиваем панели
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
        gameplayHUD.SetActive(false);

        // 3. Включаем звук
        AudioListener.pause = false;

        GameObject musicObj = GameObject.Find("MenuMusic");
        if (musicObj != null)
        {
            AudioSource audioSource = musicObj.GetComponent<AudioSource>();
            if (audioSource != null) audioSource.Play();
        }
        if (menuAudioListener != null) menuAudioListener.enabled = false;
        if (playerObject != null) playerObject.SetActive(true);
        if (menuAudioListener != null) menuAudioListener.enabled = true;
        // 4. Освобождаем мышку
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    // Этот метод будет следить за мышкой, пока мы в меню
    private void Update()
    {
        if (!isGameStarted)
        {
            // Принудительно освобождаем мышку каждый кадр, пока не начали игру
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    public void StartGame()
    {
        isGameStarted = true; // Запоминаем, что игра началась

        mainMenuPanel.SetActive(false);
        if (videoBackground != null) videoBackground.SetActive(false);
        gameplayHUD.SetActive(true);

        if (menuMusic != null) menuMusic.Stop();

        // Включаем игрока и блокируем мышку для геймплея
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
            mainMenuPanel.SetActive(false); // На всякий случай прячем главное меню
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true); // Включаем паузу
        }
        else
        {
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false); // Прячем паузу
            mainMenuPanel.SetActive(true); // Включаем главное меню
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
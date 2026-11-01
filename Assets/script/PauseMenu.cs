using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;

    [Header("Panels")]
    public GameObject pauseMenuPanel;
    public GameObject gameplayHUD;
    public GameObject mainMenuPanel;
    public GameObject settingsPanel; // <-- ДОБАВИЛИ ПАНЕЛЬ НАСТРОЕК
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
            // Если открыты настройки, Esc должен закрывать их
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

    // --- НОВЫЕ МЕТОДЫ ДЛЯ НАСТРОЕК ИЗ ПАУЗЫ ---

    public void OpenPauseSettings()
    {
        pauseMenuPanel.SetActive(false); // Прячем меню паузы
        settingsPanel.SetActive(true);   // Показываем настройки
        PauseMenu.GameIsPaused = true;
    }

    public void ClosePauseSettings()
    {
        settingsPanel.SetActive(false);  // Прячем настройки
        pauseMenuPanel.SetActive(true);  // Возвращаем меню паузы
    }

    // ------------------------------------------

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        GameIsPaused = false;
        AudioListener.pause = false;

        // Меню и игра в одной сцене: перезагружаем её, чтобы следующая игра началась с нуля
        HideableCloset.IsPlayerHidden = false;
        PickupItem.stonesCollected = 0;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
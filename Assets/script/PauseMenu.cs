using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using System.Collections.Generic;

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

    // Видео-катсцены, поставленные на паузу (продолжаются после снятия паузы)
    private readonly List<VideoPlayer> pausedVideos = new List<VideoPlayer>();

    private void Start()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
    }

    private void Update()
    {
        if (BotInput.GetKeyDown(KeyCode.Escape))
        {
            // Карта и камеры закрываются по Esc, пауза при этом не открывается
            if (!GameIsPaused && EvacuationPlan.CloseOpen()) return;
            if (!GameIsPaused)
            {
                var tv = FindAnyObjectByType<InteractableTV>();
                if (tv != null && tv.IsViewing) { tv.TryExit(); return; }
            }

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
        PauseGranny.Hide();
        pauseMenuPanel.SetActive(false);
        if (gameplayHUD != null) gameplayHUD.SetActive(true);

        // Катсцена продолжается с того же места
        foreach (var v in pausedVideos) if (v != null && v.gameObject.activeInHierarchy) v.Play();
        pausedVideos.Clear();

        Time.timeScale = 1f;
        GameIsPaused = false;
        AudioListener.pause = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Pause()
    {
        pauseMenuPanel.SetActive(true);

        // Видео-катсцены живут внутри HUD: ставим их на паузу, а HUD не прячем,
        // иначе видео выключится и после паузы останется застывший кадр
        pausedVideos.Clear();
        if (gameplayHUD != null)
            foreach (var v in gameplayHUD.GetComponentsInChildren<VideoPlayer>())
                if (v.isPlaying) { v.Pause(); pausedVideos.Add(v); }
        if (gameplayHUD != null && pausedVideos.Count == 0) gameplayHUD.SetActive(false);

        Time.timeScale = 0f;
        GameIsPaused = true;
        AudioListener.pause = true;
        PauseGranny.TryShow();
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
        OutdoorSound.EndingSilence = false;
        PauseGranny.Hide();
        PauseGranny.Shown = 0;
        HideableCloset.FaceUsed = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
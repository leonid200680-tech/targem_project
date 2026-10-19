using UnityEngine;
using UnityEngine.SceneManagement; // For loading scenes

public class SimplePauseMenu : MonoBehaviour
{
    public GameObject pauseMenu; // Reference to the pause menu UI element
    private bool isPaused = false; // Tracks whether the game is paused

    void Update()
    {
        // Toggle pause when Escape is pressed
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    // Call this method to pause the game
    public void PauseGame()
    {
        pauseMenu.SetActive(true); // Show the pause menu
        Time.timeScale = 0; // Stop game time
        isPaused = true; // Set paused state
        Cursor.visible = true; // Show cursor
        Cursor.lockState = CursorLockMode.None; // Unlock cursor
    }

    // Call this method to resume the game
    public void ResumeGame()
    {
        pauseMenu.SetActive(false); // Hide the pause menu
        Time.timeScale = 1; // Resume game time
        isPaused = false; // Clear paused state
        Cursor.visible = false; // Hide cursor
        Cursor.lockState = CursorLockMode.Locked; // Lock cursor
    }

    // Call this method to quit to the desktop
    public void QuitToDesktop()
    {
        Time.timeScale = 1; // Resume game time
        Application.Quit(); // Quit the application
    }
}

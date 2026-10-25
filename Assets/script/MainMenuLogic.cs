using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuLogic : MonoBehaviour
{
    private GameObject mainMenu;
    private GameObject optionsMenu;
    private GameObject loading;
    public GameObject controls;
    public GameObject graphics;
    public GameObject Audio;
    public AudioSource buttonSound;

    // Start is called before the first frame update
    void Start()
    {
        // Find references to canvas objects
        mainMenu = GameObject.Find("MainMenuCanvas");
        optionsMenu = GameObject.Find("OptionsCanvas");
        loading = GameObject.Find("LoadingCanvas");

        // Enable main menu canvas and disable others
        mainMenu.GetComponent<Canvas>().enabled = true;
        optionsMenu.GetComponent<Canvas>().enabled = false;
        loading.GetComponent<Canvas>().enabled = false;

        // Disable controls, graphics, and audio game objects
        controls.SetActive(false);
        graphics.SetActive(false);
        Audio.SetActive(false);
    }

    // Method to handle start button click
    public void StartButton()
    {
        // Enable loading canvas, disable main menu, play button sound, and load starting scene
        loading.GetComponent<Canvas>().enabled = true;
        mainMenu.GetComponent<Canvas>().enabled = false;
        buttonSound.Play();
        SceneManager.LoadScene("mainScene");
    }

    // Method to handle options button click
    public void OptionsButton()
    {
        // Play button sound, disable main menu, enable options menu, and show audio settings
        buttonSound.Play();
        mainMenu.GetComponent<Canvas>().enabled = false;
        optionsMenu.GetComponent<Canvas>().enabled = true;
        controls.SetActive(false);
        graphics.SetActive(false);
        Audio.SetActive(true);
    }

    // Method to handle controls button click
    public void ControlsButton()
    {
        // Show controls settings and play button sound
        controls.SetActive(true);
        graphics.SetActive(false);
        Audio.SetActive(false);
        buttonSound.Play();
    }

    // Method to handle graphics button click
    public void GraphicsButton()
    {
        // Show graphics settings and play button sound
        controls.SetActive(false);
        graphics.SetActive(true);
        Audio.SetActive(false);
        buttonSound.Play();
    }

    // Method to handle audio button click
    public void AudioButton()
    {
        // Show audio settings and play button sound
        controls.SetActive(false);
        graphics.SetActive(false);
        Audio.SetActive(true);
        buttonSound.Play();
    }

    // Method to handle credits button click
    

    // Method to handle exit game button click
    public void ExitGameButton()
    {
        // Play button sound, quit application, and log message
        buttonSound.Play();
        Application.Quit();
        Debug.Log("App Has Exited");
    }

    // Method to handle return to main menu button click
    public void ReturnToMainMenuButton()
    {
        // Play button sound, enable main menu, disable options and credits menu
        buttonSound.Play();
        mainMenu.GetComponent<Canvas>().enabled = true;
        optionsMenu.GetComponent<Canvas>().enabled = false;
    }

    // Update is called once per frame
    void Update()
    {

    }
}
